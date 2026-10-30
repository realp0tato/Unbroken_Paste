"""Extract each character in tools/art/<id>/character.png and render menu bar bounce frames.

Writes Resources/Characters/<id>/frame_XX@2x.png and a preview GIF to build/previews/.
"""
import json
import math
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageOps

HERE = Path(__file__).parent
ART = HERE / "art"
RESOURCES = HERE.parent / "Resources"
PREVIEWS = HERE.parent / "build" / "previews"
# Menu bar image drawn at @2x, 22pt tall. Width follows each character: its resting body plus 18%,
# so the menu bar adds only its usual spacing (the basic slime gets 16pt). Only the faint shadow edge
# and the flattest landing frames reach past it.
FRAME_HEIGHT = 44
WIDTH_MARGIN = 1.18
SS = 4  # supersampling factor
FRAME_COUNT = 48
GROUND = 0.28  # share of the cycle spent on the ground

# Squash (sy < 1) on the ground: landing impact, rebound, wobble, anticipation, take-off stretch.
GROUND_KEYS = [(0.0, 0.70), (0.30, 1.07), (0.55, 0.96), (0.80, 0.86), (1.0, 1.12)]


def _smooth(keys, g):
    for (g0, v0), (g1, v1) in zip(keys, keys[1:]):
        if g <= g1:
            s = (g - g0) / (g1 - g0)
            return v0 + (v1 - v0) * s * s * (3 - 2 * s)
    return keys[-1][1]


def bounce(t):
    """Return (lift 0..1, scale_x, scale_y) for cycle position t in [0, 1)."""
    if t < GROUND:
        lift, sy = 0.0, _smooth(GROUND_KEYS, t / GROUND)
    else:
        u = (t - GROUND) / (1 - GROUND)
        lift = 4 * u * (1 - u)
        sy = 1 + 0.12 * abs(1 - 2 * u) ** 1.6  # stretched while fast, round at the apex
    return lift, 1 / math.sqrt(sy) ** 1.6, sy


def extract(source):
    im = Image.open(source).convert("RGB")
    w, h = im.size
    lum = im.convert("L")
    dark = lum.point(lambda v: 255 if v < 120 else 0)
    cx0, cy0, cx1, cy1 = dark.getbbox()
    cx, cy, r = (cx0 + cx1) / 2, (cy0 + cy1) / 2, (cx1 - cx0) / 2 * 0.9

    circle = Image.new("L", (w, h), 0)
    ImageDraw.Draw(circle).ellipse((cx - r, cy - r, cx + r, cy + r), fill=255)
    navy_l, white_l = lum.getpixel((int(cx), int(cy0 + 4))), 245
    soft = lum.point(lambda v: max(0, min(255, int((v - navy_l) * 255 / (white_l - navy_l)))))
    white = ImageChops.multiply(soft, circle)

    solid = white.point(lambda v: 255 if v > 128 else 0)
    x0, y0, x1, y1 = solid.getbbox()
    pad = 2
    holes = Image.new("L", (w + 2 * pad, h + 2 * pad), 255)
    holes.paste(ImageChops.invert(solid), (pad, pad))
    ImageDraw.floodfill(holes, (0, 0), 0)
    filled = ImageChops.lighter(solid, holes.crop((pad, pad, w + pad, h + pad)))
    filled = filled.filter(ImageFilter.GaussianBlur(0.6))

    box = (x0 - 1, y0 - 1, x1 + 1, y1 + 1)
    up = lambda m: m.crop(box).resize(((box[2] - box[0]) * 4, (box[3] - box[1]) * 4), Image.LANCZOS)
    return up(filled), up(white)


def render(filled, white, lift, sx, sy, size, base_h, max_lift):
    """Template frame: black with alpha, the eyes cut out. The menu bar tints it."""
    cw, ch = size[0] * SS, size[1] * SS
    canvas = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
    aspect = filled.width / filled.height
    bh, bw = base_h * sy * SS, base_h * aspect * sx * SS
    ground = ch - 1 * SS
    bottom = ground - lift * max_lift * SS
    left, top = (cw - bw) / 2, bottom - bh
    body_f = filled.resize((max(1, round(bw)), max(1, round(bh))), Image.LANCZOS)
    body_w = white.resize(body_f.size, Image.LANCZOS)

    shadow = Image.new("L", (cw, ch), 0)
    sw = base_h * aspect * SS * (1.0 - 0.45 * lift)
    ImageDraw.Draw(shadow).ellipse((cw / 2 - sw / 2, ground - 1.2 * SS, cw / 2 + sw / 2, ground + 0.6 * SS),
                                   fill=int(70 * (1 - 0.6 * lift)))

    alpha = shadow.copy()
    alpha.paste(body_w, (round(left), round(top)), body_f)
    canvas.putalpha(alpha)
    return canvas.resize(size, Image.LANCZOS)


def drip_tips(filled):
    """Where the hanging drips end, as (x, y) shares of the character box: columns reaching clearly
    below the body's usual bottom edge, grouped into runs."""
    mask = filled.point(lambda v: 255 if v > 128 else 0)
    w, h = mask.size
    px = mask.load()
    bottoms = []
    for x in range(w):
        ys = [y for y in range(h - 1, -1, -1) if px[x, y]][:1]
        bottoms.append(ys[0] if ys else None)
    known = sorted(b for b in bottoms if b is not None)
    body_bottom = known[len(known) // 2]
    tips, run = [], []
    for x, b in enumerate(bottoms + [None]):
        if b is not None and b > body_bottom + 0.04 * h:
            run.append((x, b))
        elif run:
            tips.append((sum(x for x, _ in run) / len(run) / w, max(b for _, b in run) / h))
            run = []
    return tips


def render_drip(filled, white, t, size, base_h, tips):
    """Floating character that bobs in the air while drops form at its drips and fall to the ground."""
    cw, ch = size[0] * SS, size[1] * SS
    aspect = filled.width / filled.height
    breathe = 1 + 0.03 * math.sin(2 * math.pi * t * 2)
    bh, bw = base_h * breathe * SS, base_h * aspect / math.sqrt(breathe) * SS
    top = (6.5 + 1.5 * (1 - math.cos(2 * math.pi * t)) / 2) * SS  # upper middle of the bar, gentle 1.5px bob
    left = (cw - bw) / 2
    ground = ch - 1 * SS
    body_f = filled.resize((max(1, round(bw)), max(1, round(bh))), Image.LANCZOS)
    body_w = white.resize(body_f.size, Image.LANCZOS)

    alpha = Image.new("L", (cw, ch), 0)
    draw = ImageDraw.Draw(alpha)
    sw = base_h * aspect * SS * 0.55  # faint shadow on the ground, where the drops land
    draw.ellipse((cw / 2 - sw / 2, ground - 1.0 * SS, cw / 2 + sw / 2, ground + 0.5 * SS), fill=40)
    alpha.paste(body_w, (round(left), round(top)), body_f)

    r_max = 1.7 * SS
    for i, (tx, ty) in enumerate(tips):
        u = (t + i / max(1, len(tips)) + 0.15 * i) % 1.0
        x = left + tx * bw
        tip = top + ty * bh
        if u < 0.45:  # drop swells at the tip
            r = r_max * (u / 0.45) ** 0.7
            y = tip + r * 0.6
        elif u < 0.8:  # falls, speeding up
            s = (u - 0.45) / 0.35
            r = r_max
            y = tip + r * 0.6 + (ground - r - tip - r * 0.6) * s * s
        else:
            continue
        stretch = 1.0 + (0.35 if 0.45 <= u < 0.8 else 0.0)
        draw.ellipse((x - r, y - r * stretch, x + r, y + r), fill=255)

    canvas = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
    canvas.putalpha(alpha)
    return canvas.resize(size, Image.LANCZOS)


def render_stickman(t, size):
    """Original stick figure doing a chest-bounce dance, drawn from lines and circles (no source picture).
    Points to keep from the reference dance: the body turned about 45 degrees (shoulders and feet
    staggered, face turned right), one arm only moving (its hand slides down across the chest and back up
    four times per cycle while the other arm hangs), a big chest bounce at half the arm's speed (twice per
    1.5 s cycle: the torso is two segments joined at an unseen point on the chest, and on the beat that
    point kicks up and back so the torso bends there and the shoulders and head ride up with it), and the
    head tilted to the side. The figure faces left.
    Coordinates are in @2x pixels on a 44 px tall canvas; the active arm gets a thin cut-out edge where it
    crosses the body so it still reads in one colour."""
    cw, ch = size[0] * SS, size[1] * SS
    u = lambda v: v * SS
    pop = ((1 - math.cos(2 * math.pi * t * 2)) / 2) ** 0.8  # chest bounce, twice per cycle
    sweep = (1 - math.cos(2 * math.pi * t * 4)) / 2  # 0 = hand up by the far shoulder, 1 = down at the belly
    cx = size[0] / 2

    hip = (cx - 0.5, 29.5)
    # Unseen chest joint: kicks up and toward the figure's back (the right of the frame once mirrored);
    # the upper torso, shoulders and head ride up with it, so the torso bends at the chest.
    chest = (cx + 0.2 - 2.4 * pop, 22.5 - 3.0 * pop)
    neck = (cx + 0.6 - 0.6 * pop, 16.5 - 2.2 * pop)
    head = (cx + 0.2 - 0.4 * pop, 9.8 - 1.8 * pop)  # tipped toward the back shoulder, lifts with the chest
    tilt = math.radians(18 + 8 * pop)  # head tilted to the side

    body = Image.new("L", (cw, ch), 0)

    def line(target, a, b, w):
        dd = ImageDraw.Draw(target)
        dd.line([(u(a[0]), u(a[1])), (u(b[0]), u(b[1]))], fill=255, width=round(u(w)))
        for x, y in (a, b):
            dd.ellipse((u(x - w / 2), u(y - w / 2), u(x + w / 2), u(y + w / 2)), fill=255)

    def disc(target, c, r):
        ImageDraw.Draw(target).ellipse((u(c[0] - r), u(c[1] - r), u(c[0] + r), u(c[1] + r)), fill=255)

    # Legs: turned stance, front foot ahead, knees soft.
    line(body, hip, (cx + 2.2, 36.5), 2.6)
    line(body, (cx + 2.2, 36.5), (cx + 3.4, 42.5), 2.6)
    line(body, hip, (cx - 2.6, 36.0), 2.6)
    line(body, (cx - 2.6, 36.0), (cx - 3.8, 42.5), 2.6)
    # Torso: two segments that bend at the chest joint.
    line(body, hip, chest, 3.6)
    line(body, chest, neck, 3.6)
    # Back arm hangs (far shoulder, slightly behind).
    back_shoulder = (neck[0] - 2.4, neck[1] + 1.4)
    line(body, back_shoulder, (cx - 5.6, 23.6), 2.2)
    line(body, (cx - 5.6, 23.6), (cx - 6.4, 28.6 + 0.3 * pop), 2.2)
    # Head with two eyes cut out, turned right and tilted.
    line(body, neck, head, 2.4)  # short neck so the tilt shows
    disc(body, head, 5.4)
    eyes = Image.new("L", (cw, ch), 0)
    for dx in (-0.2, 2.6):
        ex = head[0] + 0.9 + dx * math.cos(tilt)
        ey = head[1] + 0.6 + dx * math.sin(tilt)
        ImageDraw.Draw(eyes).ellipse((u(ex - 0.7), u(ey - 1.2), u(ex + 0.7), u(ey + 1.2)), fill=255)
    body = ImageChops.subtract(body, eyes)

    # Active arm: front shoulder, elbow out to the side, hand sliding down across the chest and back.
    shoulder = (neck[0] + 2.2, neck[1] + 1.2)
    hand = (cx + 4.2 - 0.8 * pop - 5.4 * sweep, 18.2 - 1.8 * pop + 7.6 * sweep)
    elbow = (cx + 7.4 - 0.8 * sweep, 21.0 + 3.2 * sweep)
    arm = Image.new("L", (cw, ch), 0)
    edge = Image.new("L", (cw, ch), 0)
    for target, grow in ((edge, 1.0), (arm, 0.0)):
        line(target, shoulder, elbow, 2.2 + 2 * grow)
        line(target, elbow, hand, 2.2 + 2 * grow)
        disc(target, hand, 1.5 + grow)
    alpha = ImageChops.lighter(ImageChops.subtract(body, edge), arm)

    shadow = Image.new("L", (cw, ch), 0)
    ImageDraw.Draw(shadow).ellipse((u(cx - 6), u(42.2), u(cx + 6), u(43.6)), fill=50)
    alpha = ImageOps.mirror(ImageChops.lighter(alpha, shadow))  # face left
    canvas = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
    canvas.putalpha(alpha)
    return canvas.resize(size, Image.LANCZOS)


def drawn_badge(frame, size=96):
    """Copy badge for a drawn character: its frame in white on the navy circle the other badges use."""
    big = frame.getchannel("A").resize((frame.width * 8, frame.height * 8), Image.LANCZOS)
    side = size * 4
    badge = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    mask = Image.new("L", (side, side), 0)
    ImageDraw.Draw(mask).ellipse((2, 2, side - 3, side - 3), fill=255)
    badge.paste((39, 42, 74, 255), (0, 0), mask)
    scale = side * 0.78 / big.height
    figure = big.resize((round(big.width * scale), round(big.height * scale)), Image.LANCZOS)
    badge.paste((243, 244, 248, 255), ((side - figure.width) // 2, round(side * 0.1)), figure)
    return badge.resize((size, size), Image.LANCZOS)


def make_badge(source, size=96):
    """Full-colour round copy badge: the dark circle cropped square, outside made transparent."""
    im = Image.open(source).convert("RGBA")
    x0, y0, x1, y1 = im.convert("L").point(lambda v: 255 if v < 120 else 0).getbbox()
    side = max(x1 - x0, y1 - y0)
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    crop = im.crop((round(cx - side / 2), round(cy - side / 2), round(cx + side / 2), round(cy + side / 2)))
    crop = crop.resize((side * 4, side * 4), Image.LANCZOS)
    mask = Image.new("L", crop.size, 0)
    ImageDraw.Draw(mask).ellipse((2, 2, crop.width - 3, crop.height - 3), fill=255)
    crop.putalpha(ImageChops.multiply(crop.getchannel("A"), mask))
    return crop.resize((size, size), Image.LANCZOS)


def frame_width(filled, base_h):
    w = math.ceil(base_h * filled.width / filled.height * WIDTH_MARGIN)
    return w + w % 2


def build(entry):
    """`height` (px @2x, default 26) and `lift` (bounce height, default 13) tune each character's size;
    `motion` picks the animation: "bounce" (default), "drip", or "stickman" (a drawn figure that needs no
    picture); `width` (px @2x) sets the frame width for a drawn character."""
    character_id = entry["id"]
    out = RESOURCES / "Characters" / character_id
    out.mkdir(parents=True, exist_ok=True)
    if entry.get("motion") == "stickman":
        size = (entry.get("width", 30), FRAME_HEIGHT)
        frames = [render_stickman(i / FRAME_COUNT, size) for i in range(FRAME_COUNT)]
        badge = drawn_badge(frames[0])
    else:
        filled, white = extract(ART / character_id / "character.png")
        base_h, max_lift = entry.get("height", 26), entry.get("lift", 13)
        size = (frame_width(filled, base_h), FRAME_HEIGHT)
        if entry.get("motion") == "drip":
            frames = [render_drip(filled, white, i / FRAME_COUNT, size, base_h, drip_tips(filled)) for i in range(FRAME_COUNT)]
        else:
            frames = [render(filled, white, *bounce(i / FRAME_COUNT), size, base_h, max_lift) for i in range(FRAME_COUNT)]
        badge = make_badge(ART / character_id / "character.png")
    for old in out.glob("frame_*@2x.png"):
        old.unlink()
    for i, frame in enumerate(frames):
        frame.save(out / f"frame_{i:02d}@2x.png")
    badge.save(out / "badge.png")
    PREVIEWS.mkdir(parents=True, exist_ok=True)
    preview(frames, PREVIEWS / f"{character_id}.gif")
    return len(frames)


def preview(frames, path):
    """Light and dark menu bar side by side, 4x zoom."""
    scale, pages = 4, []
    for frame in frames:
        w, h = frame.size
        page = Image.new("RGB", (w * scale * 2 + 30, h * scale + 20), (255, 255, 255))
        alpha = frame.getchannel("A").resize((w * scale, h * scale), Image.NEAREST)
        for i, (bg, fg) in enumerate((((236, 236, 236), (0, 0, 0)), ((40, 40, 44), (255, 255, 255)))):
            tile = Image.new("RGB", alpha.size, bg)
            tile.paste(fg, (0, 0), alpha)
            page.paste(tile, (10 + i * (w * scale + 10), 10))
        pages.append(page)
    pages[0].save(path, save_all=True, append_images=pages[1:], duration=30, loop=0)


if __name__ == "__main__":
    for entry in json.loads((RESOURCES / "characters.json").read_text(encoding="utf-8")):
        if entry.get("motion") == "stickman" or (ART / entry["id"] / "character.png").exists():
            print(f"{entry['name']}: {build(entry)} frames → {RESOURCES / 'Characters' / entry['id']}")
