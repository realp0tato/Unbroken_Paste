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


def render_dance(filled, white, t, size, base_h):
    """Slime turned about 45 degrees with one round little arm in front of its chest, doing a chest-bounce
    dance. It stands still on the ground (no hop): on every beat the chest pops out and settles, and the
    arm pumps up and down twice per cycle. Beats follow the reference dance: a quick pop about every
    0.19 s (8 per 1.5 s cycle, 4 per arm stroke). The turn is drawn by narrowing the body and sliding the
    eyes toward the side it faces; the frame is mirrored so it faces right with the arm on the left.
    An arm in front of the body gets a thin cut-out edge so it still reads in one colour."""
    cw, ch = size[0] * SS, size[1] * SS
    aspect = filled.width / filled.height
    eyes = ImageChops.subtract(filled, white)
    eye_box = eyes.point(lambda v: 255 if v > 128 else 0).getbbox() or (0, 0, eyes.width, int(eyes.height * 0.7))
    lift_eyes = 0.08  # turned head: eyes slide toward the facing side and up a little, freeing the chest
    eye_bottom = eye_box[3] / eyes.height - lift_eyes

    beat = (t * 8) % 1.0
    pop = math.exp(-5 * beat) * (1 - beat)  # sharp hit on the beat, then settles
    stroke = math.cos(2 * math.pi * t * 2)  # arm height: +1 up, -1 down, two strokes per cycle
    turn = 0.84  # width seen at about 45 degrees
    bh = base_h * (1 + 0.015 * pop) * SS
    bw = base_h * aspect * turn * SS
    ground = ch - 1 * SS
    left, top = (cw - bw) / 2 - 0.8 * SS, ground - bh  # feet stay on the ground
    body = Image.new("L", (cw, ch), 0)
    body.paste(255, (round(left), round(top)), filled.resize((max(1, round(bw)), max(1, round(bh))), Image.LANCZOS))

    # Chest: a soft round swell on the arm side that pops out on each beat.
    chest = top + bh * (eye_bottom + (1 - eye_bottom) * 0.42)
    cx, cr = left + bw * 0.76, bh * (0.17 + 0.07 * pop)
    draw = ImageDraw.Draw(body)
    draw.ellipse((cx - cr * 1.05 + 0.9 * pop * SS, chest - cr * 0.9, cx + cr * 1.05 + 0.9 * pop * SS, chest + cr * 0.9), fill=255)

    face = eyes.resize((max(1, round(bw * 0.86)), max(1, round(bh))), Image.LANCZOS)
    holes = Image.new("L", (cw, ch), 0)
    holes.paste(face, (round(left + bw * 0.07 - bw * 0.10), round(top - bh * lift_eyes)))
    body = ImageChops.subtract(body, holes)

    # Round little arm riding on the chest: shoulder moves out with the pop, hand pumps up and down.
    arm_w, hand_r, gap = 3.2 * SS, 2.6 * SS, 1.0 * SS
    shoulder = (left + bw * 0.88 + 0.9 * pop * SS, chest - 0.03 * bh)
    hand = (left + bw * 0.68 + 0.5 * pop * SS, chest + 0.02 * bh - 0.06 * bh * stroke)

    def arm(target, grow):
        d = ImageDraw.Draw(target)
        w = arm_w + 2 * grow
        d.line([shoulder, hand], fill=255, width=round(w))
        for x, y, r in ((*shoulder, w / 2), (*hand, hand_r + grow)):
            d.ellipse((x - r, y - r, x + r, y + r), fill=255)

    edge = Image.new("L", (cw, ch), 0)
    arm(edge, gap)
    limb = Image.new("L", (cw, ch), 0)
    arm(limb, 0)
    alpha = ImageChops.lighter(ImageChops.subtract(body, edge), limb)

    shadow = Image.new("L", (cw, ch), 0)
    sw = base_h * aspect * turn * SS
    ImageDraw.Draw(shadow).ellipse((cw / 2 - sw / 2, ground - 1.0 * SS, cw / 2 + sw / 2, ground + 0.5 * SS), fill=50)
    alpha = ImageOps.mirror(ImageChops.lighter(alpha, shadow))
    canvas = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
    canvas.putalpha(alpha)
    return canvas.resize(size, Image.LANCZOS)


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


def art_of(entry):
    """Source picture: `art` names another character's folder when a character reuses its drawing."""
    return ART / entry.get("art", entry["id"]) / "character.png"


def build(entry):
    """`height` (px @2x, default 26) and `lift` (bounce height, default 13) tune each character's size;
    `motion` picks the animation: "bounce" (default), "drip" or "dance"; `width` (px @2x) overrides the
    frame width when arms reach past the body."""
    character_id = entry["id"]
    filled, white = extract(art_of(entry))
    out = RESOURCES / "Characters" / character_id
    out.mkdir(parents=True, exist_ok=True)
    base_h, max_lift = entry.get("height", 26), entry.get("lift", 13)
    size = (entry.get("width") or frame_width(filled, base_h), FRAME_HEIGHT)
    if entry.get("motion") == "dance":
        frames = [render_dance(filled, white, i / FRAME_COUNT, size, base_h) for i in range(FRAME_COUNT)]
    elif entry.get("motion") == "drip":
        frames = [render_drip(filled, white, i / FRAME_COUNT, size, base_h, drip_tips(filled)) for i in range(FRAME_COUNT)]
    else:
        frames = [render(filled, white, *bounce(i / FRAME_COUNT), size, base_h, max_lift) for i in range(FRAME_COUNT)]
    for old in out.glob("frame_*@2x.png"):
        old.unlink()
    for i, frame in enumerate(frames):
        frame.save(out / f"frame_{i:02d}@2x.png")
    make_badge(art_of(entry)).save(out / "badge.png")
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
        if art_of(entry).exists():
            print(f"{entry['name']}: {build(entry)} frames → {RESOURCES / 'Characters' / entry['id']}")
