"""生成 SL QuickMsg 程序图标（assets/app.ico + 预览 PNG）。

用代码绘制，保证 16/24/32/48/256 多尺寸都清晰。
"""

from PIL import Image, ImageDraw

SIZE = 1024
BG_TOP = (11, 15, 22, 255)
BG_BOTTOM = (23, 32, 43, 255)
SECTOR = (35, 44, 58, 255)
SECTOR_ACTIVE = (44, 140, 240, 255)
HUB = (10, 13, 18, 255)
RING = (58, 70, 86, 255)


def rounded_gradient(size: int) -> Image.Image:
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    gradient = Image.new("RGBA", (1, size))
    for y in range(size):
        t = y / (size - 1)
        gradient.putpixel((0, y), tuple(
            int(BG_TOP[i] + (BG_BOTTOM[i] - BG_TOP[i]) * t) for i in range(4)
        ))
    gradient = gradient.resize((size, size))

    mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, size - 1, size - 1], radius=int(size * 0.22), fill=255)
    img.paste(gradient, (0, 0), mask)
    return img


def wheel_layer(size: int) -> Image.Image:
    outer = size * 0.40
    inner = size * 0.16
    center = size / 2

    layer = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(layer)

    gap = 3.0
    for i in range(4):
        start = -90 + i * 90 + gap / 2
        end = start + 90 - gap
        color = SECTOR_ACTIVE if i == 1 else SECTOR
        draw.pieslice(
            [center - outer, center - outer, center + outer, center + outer],
            start=start, end=end, fill=color,
        )

    # 挖掉中心，形成圆环
    mask = Image.new("L", (size, size), 0)
    md = ImageDraw.Draw(mask)
    md.ellipse([center - outer, center - outer, center + outer, center + outer], fill=255)
    md.ellipse([center - inner, center - inner, center + inner, center + inner], fill=0)
    layer.putalpha(Image.composite(layer.getchannel("A"), Image.new("L", (size, size), 0), mask))
    return layer


def build() -> Image.Image:
    img = rounded_gradient(SIZE)
    img.alpha_composite(wheel_layer(SIZE))

    draw = ImageDraw.Draw(img)
    center = SIZE / 2
    inner = SIZE * 0.16
    outer = SIZE * 0.40

    draw.ellipse([center - inner, center - inner, center + inner, center + inner], fill=HUB)
    draw.ellipse(
        [center - inner, center - inner, center + inner, center + inner],
        outline=RING, width=int(SIZE * 0.012),
    )
    draw.ellipse(
        [center - outer, center - outer, center + outer, center + outer],
        outline=(70, 86, 106, 160), width=int(SIZE * 0.008),
    )
    return img


def main() -> None:
    import os

    out_dir = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "assets")
    os.makedirs(out_dir, exist_ok=True)

    high = build()
    sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256]

    ico_path = os.path.join(out_dir, "app.ico")
    high.save(ico_path, format="ICO", sizes=[(s, s) for s in sizes])

    preview_path = os.path.join(out_dir, "icon-preview.png")
    high.resize((256, 256), Image.LANCZOS).save(preview_path)

    print("saved", ico_path)
    print("saved", preview_path)


if __name__ == "__main__":
    main()
