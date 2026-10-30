"""ClipBridge slime: a bouncing status icon with clipboard history and character switching.

macOS: top-right menu bar (rumps). Windows: bottom-right notification area (pystray).
"""
import json
import sys
import threading
import time
from pathlib import Path

from clipboard_watch import create_history, preview

HERE = Path(__file__).parent
FRAMES = HERE / "frames"
SETTINGS = Path.home() / ".clipbridge-slime.json"
MODES = [("idle", "대기"), ("busy", "분석 중"), ("cpu", "CPU 연동")]
HEADER = {"idle": "대기 중", "busy": "구조 분석 중", "cpu": "CPU 연동 중"}
CYCLE = {"idle": 1.5, "busy": 0.6}
COPY_REACTION = 1.2
POLL_EVERY = 0.3


def load_characters():
    entries = json.loads((HERE / "assets" / "characters.json").read_text(encoding="utf-8"))
    for e in entries:
        e["ready"] = not e.get("coming_soon") and (FRAMES / e["id"] / "mac").exists()
    return entries


def load_settings():
    try:
        return json.loads(SETTINGS.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return {}


def save_settings(**values):
    data = load_settings()
    data.update(values)
    try:
        SETTINGS.write_text(json.dumps(data, ensure_ascii=False), encoding="utf-8")
    except OSError:
        pass


class Animator:
    """Time-based playback: frame choice follows elapsed time, so speed changes never stutter."""

    def __init__(self, count):
        self.count, self.base, self.phase = count, "idle", 0.0
        self.fast_until, self.last = 0.0, time.monotonic()
        self.cpu, self.cpu_at = 0.0, 0.0

    def set_mode(self, mode):
        if mode == "done":
            self.fast_until = time.monotonic() + 2 * CYCLE["busy"]
        else:
            self.base = mode

    def react(self, seconds):
        self.fast_until = max(self.fast_until, time.monotonic() + seconds)

    def _cycle(self, now):
        if now < self.fast_until:
            return CYCLE["busy"]
        if self.base == "cpu":
            if now - self.cpu_at > 1:
                try:
                    import psutil
                    self.cpu = min(psutil.cpu_percent(None), 100) / 100
                except ImportError:
                    self.cpu = 0.0
                self.cpu_at = now
            return 1.8 - 1.3 * self.cpu
        return CYCLE[self.base]

    def frame(self):
        now = time.monotonic()
        self.phase = (self.phase + (now - self.last) / self._cycle(now)) % 1.0
        self.last = now
        return int(self.phase * self.count) % self.count


def initial_character(characters):
    saved = load_settings().get("character")
    ready = [c["id"] for c in characters if c["ready"]]
    return saved if saved in ready else ready[0]


def run_mac():
    import rumps
    from AppKit import NSImage

    characters = load_characters()
    cache = {}

    def images_for(cid):
        if cid not in cache:
            frames = []
            for path in sorted((FRAMES / cid / "mac").glob("frame_*@2x.png")):
                image = NSImage.alloc().initWithContentsOfFile_(str(path))
                image.setSize_((36, 22))
                image.setTemplate_(True)
                frames.append(image)
            cache[cid] = frames
        return cache[cid]

    class SlimeApp(rumps.App):
        def __init__(self):
            self.character = initial_character(characters)
            first = sorted((FRAMES / self.character / "mac").glob("frame_*@2x.png"))[0]
            super().__init__("ClipBridge", icon=str(first), template=True, quit_button="ClipBridge 종료")
            self.images = images_for(self.character)
            self.anim = Animator(len(self.images))
            self.history = create_history()
            self.react_on_copy = load_settings().get("react_on_copy", True)
            self.shown, self.next_poll = -1, 0.0

            self.header = rumps.MenuItem("ClipBridge · " + HEADER["idle"])
            self.state_menu = rumps.MenuItem("상태 변경")
            self.mode_items = {m: rumps.MenuItem(label, callback=self._mode_cb(m)) for m, label in MODES}
            self.state_menu.update(list(self.mode_items.values()) + [None, rumps.MenuItem("맞춤 완료 효과 보기", callback=self._mode_cb("done"))])
            self.clip_menu = rumps.MenuItem("클립보드")
            self.char_menu = rumps.MenuItem("캐릭터")
            self.char_items = {}
            for c in characters:
                item = rumps.MenuItem(c["name"], callback=self._char_cb(c["id"]) if c["ready"] else None)
                self.char_items[c["id"]] = item
            self.char_menu.update(list(self.char_items.values()))
            self.menu = [self.header, None, self.state_menu, self.clip_menu, self.char_menu, None]
            self._refresh_state()
            self._refresh_characters()
            self._refresh_clipboard()
            self.timer = rumps.Timer(self.tick, 1 / 60)
            self.timer.start()

        def _mode_cb(self, mode):
            return lambda _: self.switch(mode)

        def _char_cb(self, cid):
            return lambda _: self.pick_character(cid)

        def _copy_cb(self, index):
            def callback(_):
                self.history.recopy(index)
                self._refresh_clipboard()
            return callback

        def switch(self, mode):
            self.anim.set_mode(mode)
            self._refresh_state()
            if mode == "done":
                try:
                    rumps.notification("ClipBridge", "Word → 한글", "양식에 맞춰 붙였어요 (시뮬레이션)")
                except Exception:
                    pass  # unbundled Python may lack notification permission

        def pick_character(self, cid):
            self.character = cid
            self.images = images_for(cid)
            self.anim.count, self.shown = len(self.images), -1
            save_settings(character=cid)
            self._refresh_characters()

        def toggle_react(self, item):
            self.react_on_copy = not self.react_on_copy
            item.state = int(self.react_on_copy)
            save_settings(react_on_copy=self.react_on_copy)

        def clear_history(self, _):
            self.history.clear()
            self._refresh_clipboard()

        def _refresh_state(self):
            self.header.title = "ClipBridge · " + HEADER[self.anim.base]
            for mode, item in self.mode_items.items():
                item.state = int(mode == self.anim.base)

        def _refresh_characters(self):
            for cid, item in self.char_items.items():
                item.state = int(cid == self.character)

        def _refresh_clipboard(self):
            if len(self.clip_menu):  # rumps creates the submenu lazily; clear() fails before the first item
                self.clip_menu.clear()
            entries = []
            if self.history.items:
                entries.append(rumps.MenuItem("최근 복사 · 누르면 다시 복사"))
                entries += [rumps.MenuItem(f"{i + 1}. {preview(t)}", callback=self._copy_cb(i))
                            for i, t in enumerate(self.history.items)]
                entries += [None, rumps.MenuItem("기록 지우기", callback=self.clear_history)]
            else:
                entries.append(rumps.MenuItem("아직 복사한 내용이 없어요"))
            react = rumps.MenuItem("복사하면 슬라임 반응", callback=self.toggle_react)
            react.state = int(self.react_on_copy)
            entries += [None, react, rumps.MenuItem("기록은 저장하지 않고 메모리에만 있어요")]
            self.clip_menu.update(entries)

        def tick(self, _timer):
            if not hasattr(self, "_nsapp"):
                return
            index = self.anim.frame()
            if index != self.shown:
                self._nsapp.nsstatusitem.setImage_(self.images[index])
                self.shown = index
            now = time.monotonic()
            if now >= self.next_poll:
                self.next_poll = now + POLL_EVERY
                if self.history.poll():
                    if self.react_on_copy:
                        self.anim.react(COPY_REACTION)
                    self._refresh_clipboard()

    SlimeApp().run()


def run_windows():
    import pystray
    from PIL import Image

    characters = load_characters()
    cache = {}

    def images_for(cid):
        if cid not in cache:
            cache[cid] = [Image.open(p).copy() for p in sorted((FRAMES / cid / "win").glob("frame_*.png"))]
        return cache[cid]

    lock = threading.Lock()
    stop = threading.Event()
    state = {"character": initial_character(characters), "react": load_settings().get("react_on_copy", True)}
    images = {"list": images_for(state["character"])}
    anim = Animator(len(images["list"]))
    history = create_history()
    icon = pystray.Icon("ClipBridge", images["list"][0], "ClipBridge · " + HEADER["idle"])

    def refresh():
        icon.title = "ClipBridge · " + HEADER[anim.base]
        icon.update_menu()

    def set_mode(mode):
        def action(_icon, _item):
            anim.set_mode(mode)
            refresh()
            if mode == "done" and icon.HAS_NOTIFICATION:
                icon.notify("양식에 맞춰 붙였어요 (시뮬레이션)", "Word → 한글")
        return action

    def is_mode(mode):
        return lambda _item: anim.base == mode

    def pick_character(cid):
        def action(_icon, _item):
            with lock:
                state["character"] = cid
                images["list"] = images_for(cid)
                anim.count = len(images["list"])
            save_settings(character=cid)
            icon.update_menu()
        return action

    def is_character(cid):
        return lambda _item: state["character"] == cid

    def recopy(index):
        def action(_icon, _item):
            with lock:
                history.recopy(index)
            icon.update_menu()
        return action

    def clear_history(_icon, _item):
        with lock:
            history.clear()
        icon.update_menu()

    def toggle_react(_icon, _item):
        state["react"] = not state["react"]
        save_settings(react_on_copy=state["react"])

    def clipboard_items():
        with lock:
            texts = list(history.items)
        if texts:
            yield pystray.MenuItem("최근 복사 · 누르면 다시 복사", lambda: None, enabled=False)
            for i, text in enumerate(texts):
                yield pystray.MenuItem(f"{i + 1}. {preview(text)}", recopy(i))
            yield pystray.Menu.SEPARATOR
            yield pystray.MenuItem("기록 지우기", clear_history)
        else:
            yield pystray.MenuItem("아직 복사한 내용이 없어요", lambda: None, enabled=False)
        yield pystray.Menu.SEPARATOR
        yield pystray.MenuItem("복사하면 슬라임 반응", toggle_react, checked=lambda _item: state["react"])
        yield pystray.MenuItem("기록은 저장하지 않고 메모리에만 있어요", lambda: None, enabled=False)

    def quit_app(_icon, _item):
        stop.set()
        icon.stop()

    icon.menu = pystray.Menu(
        pystray.MenuItem(lambda _item: "ClipBridge · " + HEADER[anim.base], lambda: None, enabled=False),
        pystray.Menu.SEPARATOR,
        pystray.MenuItem("상태 변경", pystray.Menu(
            *[pystray.MenuItem(label, set_mode(m), checked=is_mode(m), radio=True) for m, label in MODES],
            pystray.Menu.SEPARATOR,
            pystray.MenuItem("맞춤 완료 효과 보기", set_mode("done")),
        )),
        pystray.MenuItem("클립보드", pystray.Menu(lambda: tuple(clipboard_items()))),
        pystray.MenuItem("캐릭터", pystray.Menu(
            *[pystray.MenuItem(c["name"], pick_character(c["id"]), checked=is_character(c["id"]),
                               radio=True, enabled=c["ready"]) for c in characters],
        )),
        pystray.Menu.SEPARATOR,
        pystray.MenuItem("ClipBridge 종료", quit_app),
    )

    def loop():
        shown, next_poll = -1, 0.0
        while not stop.is_set():
            with lock:
                index, frames = anim.frame(), images["list"]
            if index != shown or frames is not images.get("shown"):
                icon.icon, shown, images["shown"] = frames[index], index, frames
            now = time.monotonic()
            if now >= next_poll:
                next_poll = now + POLL_EVERY
                with lock:
                    copied = history.poll()
                if copied:
                    if state["react"]:
                        anim.react(COPY_REACTION)
                    icon.update_menu()
            time.sleep(1 / 30)

    def setup(i):
        i.visible = True
        threading.Thread(target=loop, daemon=True).start()

    icon.run(setup)


if __name__ == "__main__":
    if not (FRAMES / "slime" / "mac").exists():
        sys.exit("frames 폴더가 없어요. 먼저 python make_frames.py 를 실행하세요.")
    if sys.platform == "darwin":
        run_mac()
    elif sys.platform == "win32":
        run_windows()
    else:
        sys.exit("macOS 또는 Windows에서 실행하세요. 미리보기는 frames/slime/preview.gif 를 여세요.")
