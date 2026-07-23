"""
把生成的立绘处理成干净的透明 PNG：
  - 从四条边界洪水填充，抠掉纯白/浅灰（含棋盘格）背景 -> 真正的 alpha 透明
  - 保留角色内部的浅色区域（衬衫等，不与边界连通）
  - 自动裁切到不透明包围盒（避免切歪 / 多余留白）
  - 可选：把高度归一化到目标像素高度（保持宽高比）

用法:
  python tools/process_art.py <src> <dst> [--height N] [--pad P]
"""
import sys
from collections import deque
from PIL import Image


def is_background_color(r, g, b):
    # 近白 或 低饱和度的浅灰（棋盘格的两种格子都算背景）
    mn, mx = min(r, g, b), max(r, g, b)
    return mx >= 200 and (mx - mn) <= 26


def remove_background(im):
    im = im.convert("RGBA")
    w, h = im.size
    px = im.load()
    bg = bytearray(w * h)  # 1 = 判定为背景
    q = deque()

    def consider(x, y):
        r, g, b, a = px[x, y]
        if a == 0 or is_background_color(r, g, b):
            q.append((x, y))

    for x in range(w):
        consider(x, 0); consider(x, h - 1)
    for y in range(h):
        consider(0, y); consider(w - 1, y)

    while q:
        x, y = q.popleft()
        i = y * w + x
        if bg[i]:
            continue
        r, g, b, a = px[x, y]
        if not (a == 0 or is_background_color(r, g, b)):
            continue
        bg[i] = 1
        if x > 0:     q.append((x - 1, y))
        if x < w - 1: q.append((x + 1, y))
        if y > 0:     q.append((x, y - 1))
        if y < h - 1: q.append((x, y + 1))

    for y in range(h):
        for x in range(w):
            if bg[y * w + x]:
                px[x, y] = (0, 0, 0, 0)
    return im


def autocrop(im, pad):
    bbox = im.getbbox()
    if not bbox:
        return im
    l, t, r, b = bbox
    w, h = im.size
    l = max(0, l - pad); t = max(0, t - pad)
    r = min(w, r + pad); b = min(h, b + pad)
    return im.crop((l, t, r, b))


def main():
    args = sys.argv[1:]
    if len(args) < 2:
        print("usage: process_art.py <src> <dst> [--height N] [--pad P]")
        sys.exit(1)
    src, dst = args[0], args[1]
    height = None
    pad = 2
    if "--height" in args:
        height = int(args[args.index("--height") + 1])
    if "--pad" in args:
        pad = int(args[args.index("--pad") + 1])

    im = Image.open(src)
    im = remove_background(im)
    im = autocrop(im, pad)
    if height:
        w, h = im.size
        neww = max(1, round(w * height / h))
        im = im.resize((neww, height), Image.LANCZOS)
    im.save(dst)
    print(f"saved {dst} size={im.size}")


if __name__ == "__main__":
    main()
