"""Extract the slime character from assets/character.png and render bounce frames."""
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw, ImageFilter

HERE = Path(__file__).parent
SOURCE = HERE / "assets" / "character.png"
OUT = HERE / "frames"
NAVY = (39, 42, 74)
SS = 4  # supersampling factor

# (lift 0..1, scale_x, scale_y): squash on landing, stretch on take-off and fall
BOUNCE = [
    (0.00, 1.24, 0.78), (0.00, 1.10, 0.90), (0.18, 0.88, 1.14), (0.48, 0.92, 1.09),
    (0.76, 0.97, 1.03), (0.93, 1.00, 1.00), (1.00, 1.02, 0.98), (0.93, 1.00, 1.00),
    (0.76, 0.97, 1.03), (0.48, 0.92, 1.09), (0.18, 0.90, 1.12), (0.00, 1.30, 0.70),
]


def extract():
    im = Image.open(SOURCE).convert("RGB")
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


def render(filled, white, lift, sx, sy, size, base_h, max_lift, style):
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

    pos = (round(left), round(top))
    if style == "template":
        alpha = shadow.copy()
        alpha.paste(body_w, pos, body_f)
        canvas.putalpha(alpha)
    else:
        outline = Image.new("L", (cw, ch), 0)
        outline.paste(body_f, pos)
        outline = outline.filter(ImageFilter.MaxFilter(2 * SS + 1))
        canvas.paste((*NAVY, 255), (0, 0), outline)
        under = Image.new("L", (cw, ch), 0)
        under.paste(body_f, pos)
        canvas.paste((*NAVY, 255), (0, 0), under)
        top_layer = Image.new("L", (cw, ch), 0)
        top_layer.paste(body_w, pos, body_f)
        canvas.paste((255, 255, 255, 255), (0, 0), top_layer)
    return canvas.resize(size, Image.LANCZOS)


def build():
    filled, white = extract()
    mac, win = OUT / "mac", OUT / "win"
    mac.mkdir(parents=True, exist_ok=True)
    win.mkdir(parents=True, exist_ok=True)
    # macOS menu bar: 36x22pt drawn at @2x; black + alpha = template image.
    mac_frames = [render(filled, white, *f, (72, 44), 26, 13, "template") for f in BOUNCE]
    # Windows tray: 32x32 colour icon, readable on light and dark taskbars.
    win_frames = [render(filled, white, *f, (32, 32), 17, 10, "color") for f in BOUNCE]
    for i, (m, wi) in enumerate(zip(mac_frames, win_frames)):
        m.save(mac / f"slime_{i:02d}@2x.png")
        wi.save(win / f"slime_{i:02d}.png")
    preview(mac_frames, win_frames)
    return len(mac_frames)


def preview(mac_frames, win_frames):
    scale, pages = 4, []
    for m, wi in zip(mac_frames, win_frames):
        page = Image.new("RGB", (72 * scale * 2 + 32 * scale + 40, 44 * scale + 20), (255, 255, 255))
        alpha = m.getchannel("A").resize((72 * scale, 44 * scale), Image.NEAREST)
        for i, (bg, fg) in enumerate((((236, 236, 236), (0, 0, 0)), ((40, 40, 44), (255, 255, 255)))):
            tile = Image.new("RGB", alpha.size, bg)
            tile.paste(fg, (0, 0), alpha)
            page.paste(tile, (10 + i * (72 * scale + 10), 10))
        tray = Image.new("RGBA", (32 * scale, 32 * scale), (32, 32, 36, 255))
        tray.alpha_composite(wi.resize(tray.size, Image.NEAREST))
        page.paste(tray.convert("RGB"), (30 + 72 * scale * 2, 10 + (44 - 32) * scale // 2))
        pages.append(page)
    pages[0].save(OUT / "preview.gif", save_all=True, append_images=pages[1:], duration=90, loop=0)


if __name__ == "__main__":
    print(f"{build()} frames written to {OUT}")
