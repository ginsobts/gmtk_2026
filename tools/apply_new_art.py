# -*- coding: utf-8 -*-
"""
一次性脚本：把美术交付的「立绘」「场景素材」替换进游戏资源目录。
- 立绘 -> Assets/Resources/Art/Portraits/<char>/<state>.png（裁到不透明包围盒，脚底贴齐、尺寸一致）
- 场景素材 -> Assets/Resources/Art/Props/<name>.png（同样裁边）
- BE.png（1920x1080 整图）-> Assets/Resources/Art/Endings/lose.png（失败结算大图）

用法：先把两个 zip 用 GBK 文件名解压到 %TEMP%/gmtk_art_in（见对话里的解压命令），再在仓库根目录执行：
    python tools/apply_new_art.py
"""
import os
from PIL import Image, ImageFilter

SRC = os.path.expandvars(r"%TEMP%\gmtk_art_in")
LIHUI = os.path.join(SRC, "lihui", "立绘")
SCENE = os.path.join(SRC, "scene", "场景素材")
ART = os.path.join("Assets", "Resources", "Art")

# 立绘中文名 -> 目标（相对 Art/）。变体顺序按对话状态：neutral / reveal / 阶段。
PORTRAITS = {
    "林采.png": "Portraits/lin_cai/neutral.png",
    "王建国.png": "Portraits/wang_jianguo/neutral.png",
    "陈维.png": "Portraits/chen_wei/neutral.png",
    "苏晴.png": "Portraits/su_qing/neutral.png",
    "方晓.png": "Portraits/fang_xiao/neutral.png",
    "陆远.png": "Portraits/lu_yuan/neutral.png",
    "安安.png": "Portraits/an_an/neutral.png",
    "程书.png": "Portraits/cheng_shu/neutral.png",
    "女学生1.png": "Portraits/shrink_girl/neutral.png",
    "女学生2.png": "Portraits/shrink_girl/reveal.png",
    "韩露1.png": "Portraits/han_lu/neutral.png",
    "韩露2.png": "Portraits/han_lu/reveal.png",
    "顾映.png": "Portraits/gu_ying/neutral.png",
    "顾映乐.png": "Portraits/gu_ying/reveal_smile.png",
    "顾映悲.png": "Portraits/gu_ying/reveal_sad.png",
    "吴昂1.png": "Portraits/wu_ang/neutral.png",
    "吴昂2.png": "Portraits/wu_ang/s2.png",
    "吴昂3.png": "Portraits/wu_ang/s3.png",
    "魏大爷1.png": "Portraits/wei_daye/neutral.png",
    "魏大爷2.png": "Portraits/wei_daye/anxious.png",
    "魏大爷3.png": "Portraits/wei_daye/horror.png",
}

# 场景素材中文名 -> 目标（相对 Art/）
PROPS = {
    "树.png": "Props/tree.png",
    "灌木.png": "Props/bush.png",
    "椅子.png": "Props/chair.png",
    "垃圾桶.png": "Props/trashcan.png",
    "健身器材.png": "Props/gym.png",
}


def crop_to_alpha(im, pad=2):
    im = im.convert("RGBA")
    bbox = im.getchannel("A").getbbox()
    if not bbox:
        return im
    l, t, r, b = bbox
    l = max(0, l - pad); t = max(0, t - pad)
    r = min(im.width, r + pad); b = min(im.height, b + pad)
    return im.crop((l, t, r, b))


# 立绘统一到同一画布：裁掉原图周围的透明留白，等比放大到贴合固定画框，脚底居中贴底。
# 这样所有立绘「同尺寸、同画框」，角色又能填满画框显得更大；宽度固定所以不会压到台词。
PORTRAIT_CW, PORTRAIT_CH = 640, 900


def robust_content_bbox(im):
    """求人物包围盒，先做形态学开运算(腐蚀)去掉细小杂点/线，避免个别立绘上的
    孤立笔触把包围盒撑大（如顾映右侧的小杂点导致整体被缩小、偏左）。"""
    a = im.getchannel("A").point(lambda v: 255 if v > 16 else 0)
    eroded = a.filter(ImageFilter.MinFilter(7))   # 腐蚀 ~3px，抹掉细于 6px 的杂线
    bb = eroded.getbbox() or a.getbbox()
    if bb is None:
        return None
    l, t, r, b = bb
    return (max(0, l - 3), max(0, t - 3), min(im.width, r + 3), min(im.height, b + 3))


def normalize_portrait(im):
    im = im.convert("RGBA")
    bbox = robust_content_bbox(im)
    if not bbox:
        return im.resize((PORTRAIT_CW, PORTRAIT_CH))
    content = im.crop(bbox)
    cw, ch = content.size
    s = min(PORTRAIT_CW / cw, PORTRAIT_CH / ch)   # 等比缩放，铺满画框但不裁掉人物
    nw, nh = max(1, int(round(cw * s))), max(1, int(round(ch * s)))
    content = content.resize((nw, nh), Image.LANCZOS)
    canvas = Image.new("RGBA", (PORTRAIT_CW, PORTRAIT_CH), (0, 0, 0, 0))
    canvas.alpha_composite(content, ((PORTRAIT_CW - nw) // 2, PORTRAIT_CH - nh))  # 水平居中 + 贴底
    return canvas


def save(im, rel):
    dst = os.path.join(ART, *rel.split("/"))
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    im.save(dst)
    print(f"  -> {rel}  ({im.width}x{im.height})")


def main():
    done = missing = 0
    print("[立绘]")
    # 立绘统一归一化到 640x900 画框：角色填满画框（更大）、同尺寸、脚底贴底、宽度固定不压字。
    for name, rel in PORTRAITS.items():
        src = os.path.join(LIHUI, name)
        if not os.path.exists(src):
            print(f"  !! 缺文件: {name}"); missing += 1; continue
        save(normalize_portrait(Image.open(src)), rel); done += 1

    print("[场景素材]")
    for name, rel in PROPS.items():
        src = os.path.join(SCENE, name)
        if not os.path.exists(src):
            print(f"  !! 缺文件: {name}"); missing += 1; continue
        save(crop_to_alpha(Image.open(src)), rel); done += 1

    print("[结算大图]")
    be = os.path.join(LIHUI, "BE.png")
    if os.path.exists(be):
        save(Image.open(be).convert("RGBA"), "Endings/lose.png"); done += 1
    else:
        print("  !! 缺 BE.png"); missing += 1

    print(f"\n完成：{done} 个，缺失：{missing} 个")


if __name__ == "__main__":
    main()
