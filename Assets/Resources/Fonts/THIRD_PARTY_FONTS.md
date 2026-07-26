# 第三方字体

## Noto Sans SC

- 文件：`Assets/Resources/Fonts/GameFont.ttf`
- 原始字体：Noto Sans SC（可变字重版本 `NotoSansSC[wght].ttf`）
- 作者 / 来源：Google Fonts — https://github.com/google/fonts/tree/main/ofl/notosanssc
- 许可：SIL Open Font License 1.1（允许商用、允许再分发与修改，协议全文见同目录 `OFL.txt`）
- 修改说明：已固化到 Regular（wght=400），并按游戏实际使用的字符做了子集化，
  从 17.7 MB 压到约 350 KB。

## 为什么必须内嵌字体

`UIManager` 早期用 `Font.CreateDynamicFontFromOSFont` 直接取系统字体（微软雅黑等）。
这在 PC 上可行，但 WebGL 运行于浏览器沙箱，拿不到任何系统字体，中文会全部渲染为空白。
因此字体必须作为资源打进包里。另外，微软雅黑不可再分发，也不适合随包发布。

## 新增中文文案后如何更新字体

子集只包含扫描时用到的字符。改完配置表或代码里的中文文案后，重跑：

```bash
python tools/collect_glyphs.py      # 扫描 GameData 配置表 + 脚本字符串字面量
python tools/build_font_subset.py   # 重新生成 GameFont.ttf
```

`tools/fonts_src/` 存放下载的原始字体，不入库；脚本在缺失时会自动重新下载。
