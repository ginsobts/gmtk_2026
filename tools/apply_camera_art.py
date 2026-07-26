"""把新的相机外壳 / 快门手美术接入游戏。

相机外壳原图是不带 alpha 的 RGB，机身之外的背景和中间的取景屏都是纯黑。游戏需要这两处
真正透明：取景开口要透出场景，而且 UIManager.ResolveViewfinderMetrics 会在运行时从图片
中心做洪水填充来量取开口尺寸，靠的就是 alpha。

这里用连通域填充而不是「把所有黑色变透明」，否则机身内部的深色描边会被一起挖掉。
- 从四边出发填充 -> 机身外部背景
- 从中心出发填充 -> 取景屏
两者若连通说明机身有缺口，会报错提示。

用法：
    python tools/apply_camera_art.py <相机图> <手图>
"""

from __future__ import annotations

import argparse
import sys
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image

PROJECT = Path(__file__).resolve().parent.parent
OUT_DIR = PROJECT / "Assets" / "Resources" / "Art" / "Camera"
OUT_CAMERA = OUT_DIR / "digital_camera_overlay.png"
OUT_HAND = OUT_DIR / "camera_shutter_hand.png"

# 判定“可填充的黑”。取稍宽的阈值以吃掉抗锯齿边缘，机身描边是深橄榄色不会被误判。
BLACK_THRESHOLD = 40


def flood(mask: np.ndarray, seeds: list[tuple[int, int]]) -> np.ndarray:
    """在 mask(True=可通行) 上从 seeds 做 4 邻域洪水填充，返回到达区域。"""
    h, w = mask.shape
    out = np.zeros_like(mask)
    q: deque[tuple[int, int]] = deque()
    for y, x in seeds:
        if 0 <= y < h and 0 <= x < w and mask[y, x] and not out[y, x]:
            out[y, x] = True
            q.append((y, x))
    while q:
        y, x = q.popleft()
        for ny, nx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
            if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not out[ny, nx]:
                out[ny, nx] = True
                q.append((ny, nx))
    return out


def process_camera(src: Path) -> None:
    img = Image.open(src).convert("RGBA")
    rgb = np.array(img)[..., :3].astype(int)
    h, w = rgb.shape[:2]
    black = rgb.max(axis=2) <= BLACK_THRESHOLD

    border = [(0, x) for x in range(w)] + [(h - 1, x) for x in range(w)]
    border += [(y, 0) for y in range(h)] + [(y, w - 1) for y in range(h)]
    outside = flood(black, border)

    if outside[h // 2, w // 2]:
        print("错误：图片中心与外部背景连通，说明机身有缺口，取景开口无法独立量取。", file=sys.stderr)
        sys.exit(2)

    hole = flood(black, [(h // 2, w // 2)])

    transparent = outside | hole
    rgba = np.array(img)
    rgba[..., 3] = np.where(transparent, 0, 255)
    # 透明像素的 RGB 归零，避免缩放时黑边渗色。
    rgba[..., :3] = np.where(transparent[..., None], 0, rgba[..., :3])

    ys, xs = np.where(hole)
    hole_box = (xs.min(), ys.min(), xs.max(), ys.max())
    print(f"相机：{w}x{h}  外部背景 {outside.mean():.1%}  取景开口 {hole.mean():.1%}")
    print(f"      开口范围 x[{hole_box[0]}..{hole_box[2]}] y[{hole_box[1]}..{hole_box[3]}]"
          f"  尺寸 {hole_box[2]-hole_box[0]+1}x{hole_box[3]-hole_box[1]+1}")
    print(f"      机身宽高比 {w/h:.4f}")

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    Image.fromarray(rgba, "RGBA").save(OUT_CAMERA)
    print(f"      -> {OUT_CAMERA.relative_to(PROJECT)}")


def process_hand(src: Path) -> None:
    img = Image.open(src).convert("RGBA")
    rgba = np.array(img)
    alpha = rgba[..., 3]

    # 清掉零散的杂点：只保留与主体连通的最大区域。
    solid = alpha > 8
    h, w = solid.shape
    ys, xs = np.where(solid)
    if len(ys) == 0:
        print("错误：手部图片没有任何不透明像素。", file=sys.stderr)
        sys.exit(2)

    # 以重心附近的实心点为种子，保留主体。
    cy, cx = int(ys.mean()), int(xs.mean())
    if not solid[cy, cx]:
        cy, cx = int(ys[len(ys) // 2]), int(xs[len(xs) // 2])
    main = flood(solid, [(cy, cx)])

    removed = int((solid & ~main).sum())
    rgba[..., 3] = np.where(main, alpha, 0)
    rgba[..., :3] = np.where(main[..., None], rgba[..., :3], 0)

    ys, xs = np.where(main)
    cropped = Image.fromarray(rgba, "RGBA").crop((xs.min(), ys.min(), xs.max() + 1, ys.max() + 1))
    print(f"手部：{w}x{h} -> 裁剪后 {cropped.size}  清除杂点 {removed} 像素")

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    cropped.save(OUT_HAND)
    print(f"      -> {OUT_HAND.relative_to(PROJECT)}")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("camera", type=Path)
    parser.add_argument("hand", type=Path)
    args = parser.parse_args()

    process_camera(args.camera)
    process_hand(args.hand)
    return 0


if __name__ == "__main__":
    sys.exit(main())
