"""Extract each character in tools/art/<id>/character.png and render menu bar bounce frames.

Writes Resources/Characters/<id>/frame_XX@2x.png and a preview GIF to build/previews/.
"""
import json
import math
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw, ImageFilter

HERE = Path(__file__).parent
ART = HERE / "art"
RESOURCES = HERE.parent / "Resources"
PREVIEWS = HERE.parent / "build" / "previews"
# 20x22pt menu bar image drawn at @2x: exactly as wide as the slime at its widest, so the menu bar adds only its usual spacing.
FRAME_SIZE = (40, 44)
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


def build(character_id):
    filled, white = extract(ART / character_id / "character.png")
    out = RESOURCES / "Characters" / character_id
    out.mkdir(parents=True, exist_ok=True)
    frames = [render(filled, white, *bounce(i / FRAME_COUNT), FRAME_SIZE, 26, 13) for i in range(FRAME_COUNT)]
    for i, frame in enumerate(frames):
        frame.save(out / f"frame_{i:02d}@2x.png")
    make_badge(ART / character_id / "character.png").save(out / "badge.png")
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
        if (ART / entry["id"] / "character.png").exists():
            print(f"{entry['name']}: {build(entry['id'])} frames → {RESOURCES / 'Characters' / entry['id']}")
