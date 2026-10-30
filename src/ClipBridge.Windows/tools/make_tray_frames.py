"""Windows 트레이용 캐릭터 프레임을 macOS 메뉴 막대 프레임에서 만든다 (#44).

macOS 프레임(macos/UnbrokenPaste/Resources/Characters/<id>/frame_XX@2x.png)은 메뉴 막대가 색을 입히는
검은 실루엣(템플릿)이다. Windows 작업 표시줄은 색을 입히지 않으므로, 기본 슬라임 Windows 프레임처럼
흰 몸통·남색 테두리·남색 눈으로 칠해 32x32로 만든다. 움직임(통통 튀기, 녹는 슬라임의 물방울)은 macOS 프레임을 그대로 따른다.

저장소 루트에서: python3 src/ClipBridge.Windows/tools/make_tray_frames.py [캐릭터 id ...]
→ src/ClipBridge.Windows/Resources/Characters/<id>/frame_XX.png, badge.png
기본 슬라임(slime)은 손으로 만든 Windows 프레임을 쓰므로 이름을 직접 줄 때만 만든다.
"""
import shutil
import sys
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[3]
MAC = ROOT / "macos" / "UnbrokenPaste" / "Resources" / "Characters"
OUT = ROOT / "src" / "ClipBridge.Windows" / "Resources" / "Characters"

SIZE = 32
SCALE = 30 / 44  # macOS 프레임 높이 44px → 아이콘 32px 안에 바닥을 1px 띄워 둔다
SS = 4  # 내부 확대 배율
BODY = (250, 250, 250, 255)
INK = (36, 39, 84, 255)  # 기본 슬라임 Windows 프레임의 테두리·눈 색
OUTLINE = 1.3  # 최종 크기 기준 테두리 두께(px)


def fill_holes(mask):
    w, h = mask.size
    pad = Image.new("L", (w + 2, h + 2), 0)
    pad.paste(mask, (1, 1))
    outside = pad.copy()
    ImageDraw.floodfill(outside, (0, 0), 128)
    holes = outside.point(lambda v: 255 if v == 0 else 0).crop((1, 1, w + 1, h + 1))
    return ImageChops.lighter(mask, holes)


def convert(frame):
    alpha = frame.convert("RGBA").getchannel("A")
    k = SCALE * SS
    scaled = alpha.resize((round(alpha.width * k), round(alpha.height * k)), Image.LANCZOS)
    # 테두리가 잘리지 않게 둘레를 넓혀 그린다
    margin = round(OUTLINE * SS) + 2
    big = Image.new("L", (scaled.width + 2 * margin, scaled.height + 2 * margin), 0)
    big.paste(scaled, (margin, margin))
    solid = big.point(lambda v: 255 if v > 110 else 0)
    shape = fill_holes(solid)
    eyes = ImageChops.subtract(shape, solid)
    # 그림자처럼 옅은 부분은 남색을 옅게 남긴다
    faint = ImageChops.subtract(big, shape.filter(ImageFilter.MaxFilter(3))).point(lambda v: int(v * 0.8) if v < 110 else 0)

    radius = max(1, round(OUTLINE * SS))
    ring = shape.filter(ImageFilter.MaxFilter(2 * radius + 1))
    canvas = Image.new("RGBA", big.size, (0, 0, 0, 0))
    canvas.paste(Image.new("RGBA", big.size, INK[:3] + (0,)), (0, 0))
    canvas.putalpha(faint)
    canvas.paste(INK, (0, 0), ring.filter(ImageFilter.GaussianBlur(SS * 0.3)))
    canvas.paste(BODY, (0, 0), ImageChops.subtract(shape, eyes).filter(ImageFilter.GaussianBlur(SS * 0.25)))
    canvas.paste(INK, (0, 0), eyes.filter(ImageFilter.GaussianBlur(SS * 0.25)))

    small = canvas.convert("RGBa").resize((round(big.width / SS), round(big.height / SS)), Image.LANCZOS).convert("RGBA")
    out = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    # 바닥선(원래 프레임 아래 끝)을 아이콘 아래에서 1px 위에 맞춘다. 넓힌 둘레만큼 위치를 보정한다
    edge = round(margin / SS)
    out.alpha_composite(small, ((SIZE - small.width) // 2, SIZE - 1 - small.height + edge))
    return out


def build(character):
    source = MAC / character
    frames = sorted(source.glob("frame_*@2x.png"))
    if not frames:
        raise SystemExit(f"{source}에 프레임이 없습니다")
    target = OUT / character
    target.mkdir(parents=True, exist_ok=True)
    for old in target.glob("frame_*.png"):
        old.unlink()
    for path in frames:
        convert(Image.open(path)).save(target / path.name.replace("@2x", ""))
    shutil.copyfile(source / "badge.png", target / "badge.png")
    print(f"{character}: 프레임 {len(frames)}개 → {target}")


if __name__ == "__main__":
    for name in sys.argv[1:] or ["cat", "drip", "cloud", "bunny"]:
        build(name)
