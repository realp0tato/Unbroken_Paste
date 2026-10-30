"""ClipBridge slime: a RunCat-style bouncing status icon.

macOS: top-right menu bar (rumps). Windows: bottom-right notification area (pystray).
"""
import sys
import threading
import time
from pathlib import Path

FRAMES = Path(__file__).parent / "frames"
SPEEDS = {"idle": 0.11, "busy": 0.045}
STATUS = {"idle": "대기 중 · 천천히 통통", "busy": "구조 분석 중 · 빠르게 통통", "done": "양식에 맞춤 완료!", "cpu": "CPU 연동 (RunCat 모드)"}


class Animator:
    def __init__(self, count, show):
        self.count, self.show = count, show
        self.mode, self.index, self.burst = "idle", 0, 0

    def set_mode(self, mode):
        if mode == "done":
            self.burst = self.count * 2  # two quick hops, then back to idle
        self.mode = "busy" if mode == "busy" else ("cpu" if mode == "cpu" else "idle")

    def delay(self):
        if self.burst:
            self.burst -= 1
            return SPEEDS["busy"]
        if self.mode == "cpu":
            try:
                import psutil
                return 0.16 - 0.13 * min(psutil.cpu_percent(None), 100) / 100
            except ImportError:
                return SPEEDS["idle"]
        return SPEEDS[self.mode]

    def tick(self):
        self.show(self.index)
        self.index = (self.index + 1) % self.count
        return self.delay()


def run_mac():
    import rumps
    from AppKit import NSImage

    images = []
    for path in sorted((FRAMES / "mac").glob("slime_*@2x.png")):
        image = NSImage.alloc().initWithContentsOfFile_(str(path))
        image.setSize_((36, 22))
        image.setTemplate_(True)
        images.append(image)

    class SlimeApp(rumps.App):
        def __init__(self):
            super().__init__("ClipBridge", icon=str(sorted((FRAMES / "mac").glob("slime_*@2x.png"))[0]),
                             template=True, quit_button="ClipBridge 종료")
            self.status = rumps.MenuItem("상태: " + STATUS["idle"])
            self.menu = [self.status, None,
                         rumps.MenuItem("대기 (천천히)", callback=lambda _: self.switch("idle")),
                         rumps.MenuItem("분석 중 (빠르게)", callback=lambda _: self.switch("busy")),
                         rumps.MenuItem("맞춤 완료 (신나게)", callback=lambda _: self.switch("done")),
                         rumps.MenuItem("CPU 연동 (RunCat 모드)", callback=lambda _: self.switch("cpu")), None]
            self.anim = Animator(len(images), self.show)
            self.next_at = 0.0
            self.timer = rumps.Timer(self.step, 0.02)
            self.timer.start()

        def show(self, i):
            self._nsapp.nsstatusitem.setImage_(images[i])

        def step(self, _timer):
            now = time.monotonic()
            if not hasattr(self, "_nsapp") or now < self.next_at:
                return
            self.next_at = now + self.anim.tick()

        def switch(self, mode):
            self.anim.set_mode(mode)
            self.status.title = "상태: " + STATUS[mode]
            if mode == "done":
                try:
                    rumps.notification("ClipBridge", "Word → 한글", "양식에 맞춰 붙였어요 (시뮬레이션)")
                except Exception:
                    pass  # unbundled Python may lack notification permission

    SlimeApp().run()


def run_windows():
    import pystray
    from PIL import Image

    images = [Image.open(p).copy() for p in sorted((FRAMES / "win").glob("slime_*.png"))]
    stop = threading.Event()
    state = {"status": STATUS["idle"]}
    icon = pystray.Icon("ClipBridge", images[0], "ClipBridge · " + STATUS["idle"])
    anim = Animator(len(images), lambda i: setattr(icon, "icon", images[i]))

    def switch(mode):
        def handler(_icon, _item):
            anim.set_mode(mode)
            state["status"] = STATUS[mode]
            icon.title = "ClipBridge · " + STATUS[mode]
            icon.update_menu()
            if mode == "done" and icon.HAS_NOTIFICATION:
                icon.notify("양식에 맞춰 붙였어요 (시뮬레이션)", "Word → 한글")
        return handler

    def quit_app(_icon, _item):
        stop.set()
        icon.stop()

    icon.menu = pystray.Menu(
        pystray.MenuItem(lambda _: "상태: " + state["status"], lambda: None, enabled=False),
        pystray.Menu.SEPARATOR,
        pystray.MenuItem("대기 (천천히)", switch("idle"), default=True),
        pystray.MenuItem("분석 중 (빠르게)", switch("busy")),
        pystray.MenuItem("맞춤 완료 (신나게)", switch("done")),
        pystray.MenuItem("CPU 연동 (RunCat 모드)", switch("cpu")),
        pystray.Menu.SEPARATOR,
        pystray.MenuItem("ClipBridge 종료", quit_app),
    )

    def loop():
        while not stop.is_set():
            time.sleep(anim.tick())

    def setup(i):
        i.visible = True
        threading.Thread(target=loop, daemon=True).start()

    icon.run(setup)


if __name__ == "__main__":
    if not (FRAMES / "mac").exists():
        sys.exit("frames 폴더가 없어요. 먼저 python make_frames.py 를 실행하세요.")
    if sys.platform == "darwin":
        run_mac()
    elif sys.platform == "win32":
        run_windows()
    else:
        sys.exit("macOS 또는 Windows에서 실행하세요. 미리보기는 frames/preview.gif 를 여세요.")
