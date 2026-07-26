"""把 Noto Sans SC 裁剪成只含游戏用到的字形，输出到 Assets/Resources/Fonts/。

为什么需要它：UIManager 原本用 Font.CreateDynamicFontFromOSFont 取系统字体（微软雅黑等）。
WebGL 跑在浏览器沙箱里，拿不到任何系统字体，中文就会全部变成空白。把字体作为资源打进包
里即可解决，但完整中文字体有 17 MB，所以这里按实际用到的字符做子集，压到几百 KB。

依赖：pip install fonttools

用法：
    python tools/collect_glyphs.py      # 先扫描出需要的字符
    python tools/build_font_subset.py   # 再生成子集字体

新增中文文案后重跑这两步即可。
"""

from __future__ import annotations

import sys
import urllib.request
from pathlib import Path

from fontTools import subset
from fontTools.ttLib import TTFont
from fontTools.varLib import instancer

PROJECT = Path(__file__).resolve().parent.parent
SRC_DIR = PROJECT / "tools" / "fonts_src"
SRC_FONT = SRC_DIR / "NotoSansSC-VF.ttf"
GLYPHS = PROJECT / "tools" / "glyphs.txt"
OUT_DIR = PROJECT / "Assets" / "Resources" / "Fonts"
OUT_FONT = OUT_DIR / "GameFont.ttf"

SOURCE_URL = "https://github.com/google/fonts/raw/main/ofl/notosanssc/NotoSansSC%5Bwght%5D.ttf"
LICENSE_URL = "https://raw.githubusercontent.com/google/fonts/main/ofl/notosanssc/OFL.txt"

# 正文字重。Unity 动态字体可以自行合成粗体，因此只固化一个 Regular 即可。
WEIGHT = 400


def ensure_source() -> None:
    SRC_DIR.mkdir(parents=True, exist_ok=True)
    if not SRC_FONT.is_file():
        print(f"下载源字体 -> {SRC_FONT.name}")
        urllib.request.urlretrieve(SOURCE_URL, SRC_FONT)
    license_path = SRC_DIR / "OFL.txt"
    if not license_path.is_file():
        urllib.request.urlretrieve(LICENSE_URL, license_path)


def main() -> int:
    if not GLYPHS.is_file():
        print("缺少 tools/glyphs.txt，请先运行 python tools/collect_glyphs.py", file=sys.stderr)
        return 1

    ensure_source()

    text = GLYPHS.read_text(encoding="utf-8")
    unicodes = {ord(c) for c in text}
    print(f"目标字形数：{len(unicodes)}")

    font = TTFont(SRC_FONT)
    if "fvar" in font:
        print(f"固化可变字体到 wght={WEIGHT}")
        font = instancer.instantiateVariableFont(font, {"wght": WEIGHT}, inplace=True)

    options = subset.Options()
    options.layout_features = ["*"]
    options.name_IDs = ["*"]
    options.name_legacy = True
    options.notdef_outline = True
    options.recalc_bounds = True
    options.drop_tables = ["DSIG"]

    subsetter = subset.Subsetter(options=options)
    subsetter.populate(unicodes=unicodes)
    subsetter.subset(font)

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    font.save(OUT_FONT)
    font.close()

    size_kb = OUT_FONT.stat().st_size / 1024
    print(f"已生成 {OUT_FONT.relative_to(PROJECT)}（{size_kb:.0f} KB）")
    return 0


if __name__ == "__main__":
    sys.exit(main())
