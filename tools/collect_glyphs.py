"""收集游戏运行时会显示的全部字符，用于字体子集化。

来源：
1. Assets/Resources/GameData/ 下所有 .txt 配置（对话、UI 文案、角色名等）。
2. Assets/Scripts/ 下 .cs 文件里的字符串字面量（代码内兜底文案，例如 Loc 的内置默认值）。
   只取字面量，注释里的中文不会显示，无需占用字形。

输出一份去重后的字符集，供 tools/build_font_subset.py 使用。
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

PROJECT = Path(__file__).resolve().parent.parent
GAMEDATA = PROJECT / "Assets" / "Resources" / "GameData"
SCRIPTS = PROJECT / "Assets" / "Scripts"
OUTPUT = PROJECT / "tools" / "glyphs.txt"

# C# 普通字符串与逐字字符串字面量。
STRING_LITERAL = re.compile(r'@"(?:[^"]|"")*"|"(?:\\.|[^"\\])*"', re.DOTALL)


def read(path: Path) -> str:
    for encoding in ("utf-8-sig", "utf-8", "gb18030"):
        try:
            return path.read_text(encoding=encoding)
        except UnicodeDecodeError:
            continue
    return ""


def collect() -> set[str]:
    chars: set[str] = set()

    data_files = sorted(GAMEDATA.rglob("*.txt")) if GAMEDATA.is_dir() else []
    for path in data_files:
        chars.update(read(path))

    cs_files = sorted(SCRIPTS.rglob("*.cs")) if SCRIPTS.is_dir() else []
    for path in cs_files:
        for literal in STRING_LITERAL.findall(read(path)):
            chars.update(literal)

    print(f"扫描配置表 {len(data_files)} 个、脚本 {len(cs_files)} 个")
    return chars


def main() -> int:
    chars = collect()

    # 保证基础可见 ASCII 始终存在，避免子集后英文/数字/标点缺字。
    chars.update(chr(c) for c in range(0x20, 0x7F))
    # 常用中英标点与符号，配表以后新增文案时不至于立刻缺字。
    chars.update("　、。〈〉《》「」『』【】〔〕・‐–—‘’“”…※！＃＄％＆（）＊＋，－．／：；＜＝＞？＠［］＾＿｛｜｝～°±×÷→←↑↓★☆♪✓✕")

    chars = {c for c in chars if c not in "\r\n\t"}
    ordered = sorted(chars)

    cjk = [c for c in ordered if "\u4e00" <= c <= "\u9fff"]
    print(f"共 {len(ordered)} 个字符，其中汉字 {len(cjk)} 个")

    OUTPUT.write_text("".join(ordered), encoding="utf-8")
    print(f"已写入 {OUTPUT.relative_to(PROJECT)}")

    codepoints = ",".join(f"U+{ord(c):04X}" for c in ordered)
    (PROJECT / "tools" / "glyphs_codepoints.json").write_text(
        json.dumps({"count": len(ordered), "unicodes": codepoints}, ensure_ascii=True, indent=2),
        encoding="utf-8",
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
