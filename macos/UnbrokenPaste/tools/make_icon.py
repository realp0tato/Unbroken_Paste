"""Render the app icon from tools/art/slime/character.png: the white slime on a navy macOS icon plate.

Writes Resources/AppIcon.icns (app, DMG file and DMG volume icon) and a preview to build/previews/.
The source art is small, so the slime outline is traced from it and redrawn smooth at 1024px.
"""
from PIL import Image, ImageChops, ImageDraw, ImageFilter

from make_frames import ART, PREVIEWS, RESOURCES, extract

SIZE = 1024
PLATE = (100, 100, 924, 924)  # Apple icon grid: 824pt plate with room for the shadow
RADIUS = 185
NAVY = (39, 42, 74)  # circle colour of the source art
WHITE = (243, 244, 248)
SLIME_HEIGHT = 0.5  # share of the plate height


def smooth(mask, size):
    """Upscale a soft mask and re-threshold it so the edge is crisp, then antialias by downsampling."""
    big = mask.resize((size[0] * 4, size[1] * 4), Image.BICUBIC).filter(ImageFilter.GaussianBlur(10))
    return big.point(lambda v: 255 if v > 128 else 0).resize(size, Image.LANCZOS)


def make_icon(source):
    _, white = extract(source)
    plate_h = PLATE[3] - PLATE[1]
    h = round(plate_h * SLIME_HEIGHT)
    w = round(white.width * h / white.height)
    slime = smooth(white, (w, h))

    plate = Image.new("L", (SIZE, SIZE), 0)
    ImageDraw.Draw(plate).rounded_rectangle(PLATE, RADIUS, fill=255)
    shadow = plate.filter(ImageFilter.GaussianBlur(14)).point(lambda v: v * 0.35)
    shadow = ImageChops.offset(shadow, 0, 10)

    icon = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    icon.paste((0, 0, 0, 255), (0, 0), shadow)
    icon.paste(NAVY + (255,), (0, 0), plate)
    cx, cy = (PLATE[0] + PLATE[2]) / 2, (PLATE[1] + PLATE[3]) / 2
    icon.paste(WHITE + (255,), (round(cx - w / 2), round(cy - h / 2)), slime)
    return icon


if __name__ == "__main__":
    icon = make_icon(ART / "slime" / "character.png")
    icon.save(RESOURCES / "AppIcon.icns")  # Pillow writes every size from 16 to 1024
    PREVIEWS.mkdir(parents=True, exist_ok=True)
    icon.save(PREVIEWS / "AppIcon.png")
    print(f"AppIcon → {RESOURCES / 'AppIcon.icns'}")
