"""从当前入口图标和玩家提供的界面截图合成工坊宣传图，不修改游戏内 UI 资源。"""

import argparse
from pathlib import Path
import shutil
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont


def font(size, bold=False):
    return ImageFont.truetype("C:/Windows/Fonts/msyhbd.ttc" if bold else "C:/Windows/Fonts/msyh.ttc", size)


def tracked(draw, text, xy, size, color, spacing=2):
    x, y = xy
    face = ImageFont.truetype("C:/Windows/Fonts/seguisb.ttf", size)
    for letter in text:
        draw.text((x, y), letter, font=face, fill=color)
        x += draw.textlength(letter, font=face) + spacing


def cutout(screenshot, box):
    crop = screenshot.crop(box).convert("RGBA")
    data = np.asarray(crop).copy()
    background = np.array(screenshot.getpixel((700, 450))[:3], dtype=np.float32)
    distance = np.linalg.norm(data[:, :, :3].astype(np.float32) - background, axis=2)
    data[:, :, 3] = np.clip((distance - 13) * 18, 0, 255).astype(np.uint8)
    return Image.fromarray(data)


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--screenshot", type=Path, required=True)
    args = parser.parse_args()
    mod = Path(__file__).resolve().parents[1]
    game = mod.parent / "GameSource/asset/ExportedProject/Assets/Texture2D"
    output = mod / "about"
    # 当前入口没有图鉴专用按钮时使用首枚已注册精粹 Gem_C_Charcoal；原图直接保留。
    # Gem_C_Charcoal.prefab icon GUID -> Sprite/113_0.asset -> Texture2D/113_0.png。
    shutil.copyfile(game / "113_0.png", output / "icon.png")
    screenshot = Image.open(args.screenshot).convert("RGB")
    if screenshot.size != (1646, 878):
        raise ValueError("Use the supplied 1646x878 build detail screenshot")

    width, height = 1280, 720
    y, x = np.mgrid[0:height, 0:width]
    blue = np.exp(-((x - 1020) ** 2 / 430**2 + (y - 535) ** 2 / 380**2))
    gold = np.exp(-((x - 970) ** 2 / 340**2 + (y - 180) ** 2 / 300**2))
    bg = np.zeros((height, width, 3), dtype=np.float32)
    for c, base in enumerate((7, 17, 25)):
        bg[:, :, c] = base + blue * (5, 22, 28)[c] + gold * (12, 10, 4)[c]
    canvas = Image.fromarray(np.clip(bg, 0, 255).astype(np.uint8)).convert("RGBA")
    draw = ImageDraw.Draw(canvas)
    gold_color = (219, 193, 132)
    white = (237, 239, 228)
    muted = (151, 177, 183)

    # 微弱星图为宣传图装饰；前景图标与技能栏均来自当前游戏画面。
    nodes = [(733, 173), (844, 119), (970, 173), (1131, 129), (1195, 277), (1065, 329), (890, 294), (733, 173)]
    draw.line(nodes, fill=(54, 77, 83, 255), width=1)
    draw.line([(844, 119), (890, 294), (1131, 129)], fill=(40, 61, 68, 255), width=1)
    for index in range(36):
        px = 660 + (index * 127 + 47) % 555
        py = 58 + (index * 89 + 29) % 570
        r = 1 if index % 7 else 2
        draw.ellipse((px - r, py - r, px + r, py + r), fill=(99, 111, 104, 180))
    draw.line((52, 35, 1228, 35), fill=(75, 91, 94), width=1)
    draw.line((52, 685, 1228, 685), fill=(75, 91, 94), width=1)
    tracked(draw, "SHAPE OF DREAMS", (62, 64), 17, muted, 3)
    draw.text((58, 137), "BD 助手", font=font(125, True), fill=white)
    tracked(draw, "BUILD ASSISTANT", (65, 286), 25, gold_color, 3)
    draw.text((64, 358), "一键配装，轻松分享", font=font(34), fill=white)
    draw.line((66, 424, 155, 424), fill=gold_color, width=3)
    draw.text((64, 461), "星座 · 记忆 · 既视感", font=font(27), fill=(205, 222, 222))
    draw.text((64, 510), "工坊分享  /  拖放编辑构筑", font=font(21), fill=muted)
    draw.text((64, 600), "13 种语言", font=font(21, True), fill=gold_color)
    draw.text((204, 601), "跟随游戏语言切换", font=font(19), fill=muted)

    halo = Image.new("RGBA", canvas.size)
    halo_draw = ImageDraw.Draw(halo)
    halo_draw.ellipse((897, 67, 1160, 330), fill=(194, 170, 111, 32))
    canvas = Image.alpha_composite(canvas, halo.filter(ImageFilter.GaussianBlur(38)))
    draw = ImageDraw.Draw(canvas)
    draw.ellipse((922, 89, 1140, 307), outline=(109, 111, 87, 210), width=1)
    draw.arc((910, 77, 1152, 319), 207, 324, fill=gold_color, width=2)
    icon = Image.open(output / "icon.png").convert("RGBA").resize((205, 205), Image.Resampling.LANCZOS)
    canvas.alpha_composite(icon, (929, 95))
    for box, pos, size in [
        ((1052, 186, 1110, 241), (750, 181), 62),
        ((469, 327, 529, 384), (846, 273), 58),
        ((470, 187, 528, 242), (1166, 328), 49),
    ]:
        symbol = cutout(screenshot, box)
        symbol.thumbnail((size, size), Image.Resampling.LANCZOS)
        canvas.alpha_composite(symbol, pos)

    # 裁切只包含技能与精粹展示，不包含玩家名称、Steam ID、作品名或栏位编号。
    bar = cutout(screenshot, (448, 584, 1102, 779))
    bar = bar.resize((601, 179), Image.Resampling.LANCZOS)
    canvas.alpha_composite(bar, (618, 428))
    draw = ImageDraw.Draw(canvas)
    tracked(draw, "QWERD  /  SKILL BUILD", (646, 385), 16, muted, 1.3)
    tracked(draw, "PLAN. SHARE. APPLY.", (811, 635), 14, gold_color, 2)
    canvas.convert("RGB").save(output / "preview.png", optimize=True)
    for name in ("icon.png", "preview.png"):
        path = output / name
        print(f"{name}: {Image.open(path).size}, {path.stat().st_size:,} bytes")


if __name__ == "__main__":
    main()
