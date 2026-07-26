"""修复场景里内嵌的 MonoScript（!u!115）以及指向它的悬空脚本引用。

背景：如果一个 MonoBehaviour 类所在的 .cs 文件名和类名不一致，Unity 找不到对应的脚本
资产，就会在内存里临时捏一个 MonoScript，并把它一起写进场景文件。这种场景在编辑器里
看着正常，但打包后播放器无法解析，表现为 level0 损坏、exe 一启动就闪退。

本脚本把这类引用改写成正常的脚本资产引用（fileID 11500000 + guid + type 3），
并删掉内嵌的 MonoScript 对象。树木等物体上已经配好的数值全部原样保留。

用法（必须先关闭 Unity，否则编辑器内存里的场景会覆盖修改）：
    python tools/fix_scene_monoscripts.py Assets/Scenes/SampleScene.unity
    python tools/fix_scene_monoscripts.py Assets/Scenes/SampleScene.unity --dry-run
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

DOC_START = re.compile(r"^--- !u!(\d+) &(\d+)")
CLASS_NAME = re.compile(r"^  m_ClassName: (.+?)\s*$", re.MULTILINE)
GUID_LINE = re.compile(r"^guid: ([0-9a-f]{32})\s*$", re.MULTILINE)

MONOSCRIPT_CLASS_ID = "115"
# 脚本资产里 MonoScript 的固定 local file id。
MONOSCRIPT_FILE_ID = 11500000


def split_documents(text: str) -> list[str]:
    """按 YAML 文档头切分场景，保留每段原始文本。"""
    lines = text.split("\n")
    docs: list[list[str]] = []
    current: list[str] = []
    for line in lines:
        if DOC_START.match(line) and current:
            docs.append(current)
            current = [line]
        else:
            current.append(line)
    if current:
        docs.append(current)
    return ["\n".join(d) for d in docs]


def find_script_guid(project_root: Path, class_name: str) -> str | None:
    """按 Unity 规则定位脚本资产：文件名必须与类名一致。"""
    matches = [p for p in project_root.joinpath("Assets").rglob(f"{class_name}.cs") if p.is_file()]
    if len(matches) != 1:
        return None
    meta = matches[0].with_suffix(".cs.meta")
    if not meta.is_file():
        return None
    found = GUID_LINE.search(meta.read_text(encoding="utf-8"))
    return found.group(1) if found else None


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("scene", type=Path)
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    scene_path: Path = args.scene
    if not scene_path.is_file():
        print(f"找不到场景：{scene_path}", file=sys.stderr)
        return 1

    project_root = Path(__file__).resolve().parent.parent
    raw = scene_path.read_text(encoding="utf-8", newline="")
    newline = "\r\n" if "\r\n" in raw else "\n"
    text = raw.replace("\r\n", "\n")

    docs = split_documents(text)

    embedded: dict[str, str] = {}  # anchor -> class name
    for doc in docs:
        head = DOC_START.match(doc)
        if head and head.group(1) == MONOSCRIPT_CLASS_ID:
            name = CLASS_NAME.search(doc)
            if name:
                embedded[head.group(2)] = name.group(1)

    if not embedded:
        print("场景里没有内嵌 MonoScript，无需修复。")
        return 0

    resolved: dict[str, tuple[str, str]] = {}  # anchor -> (class, guid)
    for anchor, class_name in embedded.items():
        guid = find_script_guid(project_root, class_name)
        if guid is None:
            print(
                f"无法定位类 {class_name} 的脚本资产。"
                f"请先把它拆成同名文件 Assets/.../{class_name}.cs 再运行。",
                file=sys.stderr,
            )
            return 2
        resolved[anchor] = (class_name, guid)
        print(f"内嵌 MonoScript &{anchor} -> {class_name} (guid {guid})")

    kept = [d for d in docs if not (DOC_START.match(d) and DOC_START.match(d).group(2) in resolved)]
    out = "\n".join(kept)

    total = 0
    for anchor, (class_name, guid) in resolved.items():
        pattern = re.compile(r"m_Script: \{fileID: " + anchor + r"\}")
        replacement = f"m_Script: {{fileID: {MONOSCRIPT_FILE_ID}, guid: {guid}, type: 3}}"
        out, count = pattern.subn(replacement, out)
        total += count
        print(f"改写 {count} 处 {class_name} 引用")

    leftover = re.findall(r"m_Script: \{fileID: \d+\}", out)
    if leftover:
        print(f"仍有 {len(leftover)} 处悬空脚本引用未能修复。", file=sys.stderr)
        return 3

    print(f"共移除 {len(resolved)} 个内嵌 MonoScript，改写 {total} 处引用。")

    if args.dry_run:
        print("dry-run：未写入文件。")
        return 0

    backup = scene_path.with_suffix(scene_path.suffix + ".bak")
    backup.write_text(raw, encoding="utf-8", newline="")
    scene_path.write_text(out.replace("\n", newline), encoding="utf-8", newline="")
    print(f"已写入 {scene_path}（备份：{backup.name}）")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
