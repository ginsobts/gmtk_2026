# 寻找伪人（GMTK 2026）

一款 2.5D 观察解谜小游戏：小镇里混进了若干「伪人」，你扮演拿着数码相机的调查者，通过**观察、对话、拍照**找出他们，标记后一次性提交指认。

- 引擎：Unity **2023.1.22f1**（Built-in 渲染管线）
- 整个场景在运行时由脚本程序化生成，任意场景启动即可（`GameBootstrap` 自动拉起 `GameManager`）。
- **主菜单**：启动进入主菜单，可「开始游戏 / 制作者名单 / 切换语言 / 退出游戏」。
- **中英双语**：默认英文，可在主菜单一键切换中/英，选择会被记住（`PlayerPrefs`）。所有 UI 文案与角色名、对话都支持双语配置。

---

## 一、怎么玩

### 操作一览

**大厅 / 探索**
| 操作 | 按键 |
| --- | --- |
| 移动 | `W A S D` / 方向键 |
| 交谈（靠近 NPC） | `E` |
| 查看该角色的照片 | `Q` |
| 标记 / 取消标记为嫌疑人 | `F` |
| 打开相机 | `空格` |
| 打开相册（全部照片） | `Tab` |
| 打开指认列表 | `M` |

**相机模式**
| 操作 | 按键 |
| --- | --- |
| 移动取景框 | 鼠标 |
| 让取景框内的人「比耶」 | `1` |
| 让取景框内的人「笑一下」 | `2` |
| 按快门拍照 | `空格` |
| 退出相机 | `Esc` |

> 拍照所见即所拍：相册里的照片 = 你在取景框里看到的画面。

### 主菜单

启动后先进入主菜单：
- **开始游戏**：进入小镇开始一局。
- **制作者名单**：查看 Credits（内容可在 `ui.txt` 的 `credits.body` 里改，`Esc` 或「返回」退出）。
- **语言：English / 中文**：一键在中英之间切换，全部界面即时刷新，选择会被记住。
- **退出游戏**：退出（编辑器内为停止运行）。

结算界面除「再玩一次」外，也可「返回主菜单」。

### 游玩流程

1. **探索小镇**：走近 NPC，用 `E` 聊天听话术、用相机拍照留证。
2. **找破绽**：不同伪人破绽不同（见下表），有的靠对话、有的靠摆姿势、有的只有拍出来的照片里才露馅。
3. **标记嫌疑人**：确定可疑对象后靠近按 `F` 标记（此时**不会告诉你对错**，被标记的人在场景里会泛橙黄色）。
4. **提交指认**：按 `M` 打开「指认列表」，检查名单 → 点「提交指认」→ 弹窗确认。**提交后本局立即结束。**
5. **看结算**：公布你指认的正误，以及本局真正的伪人是谁。全部指对且无误指才算完美通关。

### 七种伪人 & 破绽

| 类型 | 破绽线索 |
| --- | --- |
| 豆包人 DouBao | 对话一股 AI 官方腔 |
| 六指人 SixFinger | 让 TA「比耶」时露出六根手指 |
| 一笑变可怕 ScarySmile | 让 TA「笑」时变成恐怖脸 |
| 掉帧人 FrameDrop | 在镜头里不停抖动 / 瞬移 |
| 拼接人 Stitched | 拍出来的照片里身体是拼接的 |
| 照片消失人 PhotoMissing | 真人在取景框里，但照片里没有 TA |
| 变瘪人 Deflate | 玩家靠近接触时会「变瘪」 |

---

## 二、目录结构（关键部分）

```
Assets/
  Resources/
    GameData/            # 策划配置表（Tab 分隔 .txt）
      characters.txt     # 角色（含中英名字）
      dialogue.txt       # 对话（中英双语）
      rounds.txt         # 关卡
      ui.txt             # 界面文案 / 菜单 / 制作名单（中英双语）
    Art/
      Characters/        # 角色立绘：每角色一个文件夹
        npc_00/ base.png  smile.png  yeah.png
        ...
        player.png
      Imposters/         # 通用露馅立绘（六指手、拼接、变瘪等）
      Camera/            # 相机外壳、快门手
      （其余为地面/树林/道具/UI 图集）
  Scripts/
    Game/                # 运行时逻辑
    Editor/              # 打包工具、美术导入后处理（不进最终包）
tools/
  export_tables.py       # 策划 Excel → .txt 导出脚本
  process_art.py         # 美术图批处理（去背景 / 裁剪 / 对齐身高）
```

---

## 三、策划：如何配置对话与关卡

所有内容都在 `Assets/Resources/GameData/` 的四张表里，**改表即生效，无需改代码**。表为 **Tab（制表符）分隔**，`#` 开头的行是注释会被忽略，首个非注释行是表头。

> **双语说明**：需要显示给玩家的文本都拆成 `en` / `zh` 两列。游戏按当前语言取对应列；某列留空会自动回退到另一语言，避免出现空文本。

### 推荐工作流（Excel → 表）
1. 维护一个 `tools/game_tables.xlsx`，里面建四个 sheet：`characters`、`dialogue`、`rounds`、`ui`，首行为列名。
2. 运行导出脚本：
   ```
   pip install openpyxl
   python tools/export_tables.py            # 默认读 tools/game_tables.xlsx
   ```
   会覆盖生成 `Resources/GameData/*.txt`。单元格里的换行会自动转成 `\n`。
3. 也可以直接手改 `.txt`（注意用 Tab 分隔）。

### characters.txt — 角色主表
`charId  artFolder  dialogueId  name_en  name_zh`
- `charId`：角色唯一 id（现用 `npc_00`…）。
- `artFolder`：立绘文件夹，相对 `Resources/Art`，如 `Characters/npc_00`。
- `dialogueId`：该角色**普通状态**用的对话 id（留空则用 `generic`）。
- `name_en` / `name_zh`：游戏里显示的名字（英文 / 中文）。

### dialogue.txt — 台词表
`dialogueId  order  en  zh`
- 同一个 `dialogueId` 写多行，按 `order` 从小到大逐句显示。
- `en` / `zh` 为双语台词；要换行就写 `\n`。
- 伪人各自的对话 id 是固定的：`doubao / sixfinger / scarysmile / framedrop / stitched / photomissing / deflate`；普通闲聊用 `generic` 或角色自定义 id。

### ui.txt — 界面文案表
`key  en  zh`
- `key` 是程序取用的键（如 `menu.start`、`result.win`、`kind.sixfinger`），**不要改 key**，只改 `en` / `zh` 文本。
- 含 `{0}` `{1}` 的是带参数模板（如 `hud.film = Film {0}`），改文案时**保留占位符**。
- 主菜单标题、制作者名单（`credits.body`，用 `\n` 换行）、七种伪人类型名（`kind.*`）都在这里配。
- 漏配某个 key 或某列时，游戏会回退到代码内置默认，不会崩。

### rounds.txt — 关卡表
`roundId  npcCount  imposterCount  film  assign`
- `npcCount` 本关出场角色数；`imposterCount` 伪人数；`film` 胶卷数。
- `assign`：**强制指派**某角色为某种伪人，格式 `charId=Kind`，多个用英文逗号分隔；**留空则随机**。
  - 例：`npc_02=SixFinger,npc_05=DouBao,npc_07=Deflate`
  - `Kind` 取值：`DouBao SixFinger ScarySmile FrameDrop Stitched PhotoMissing Deflate`
- 游戏默认使用第一行有效关卡。

> 容错：任何表缺失或字段写错，游戏会打印警告并回退到内置默认内容，不会崩。

---

## 四、美术：如何替换 / 新增资源

### 换角色立绘（最常见）
直接往对应文件夹里**覆盖同名 png** 即可，导入设置会自动配好，无需手动调 Importer：
```
Resources/Art/Characters/<charId>/
  base.png     # 必需：默认立绘
  smile.png    # 可选：命令「笑」时用（缺失则回退 base）
  yeah.png     # 可选：命令「比耶」时用（缺失则回退 base）
```

### 新增一个角色
1. 建文件夹 `Resources/Art/Characters/<newId>/` 放 `base.png`（可选 smile/yeah）。
2. 在 `characters.txt` 加一行，`artFolder` 指向该文件夹。
3. 需要出场就把 `rounds.txt` 的 `npcCount` 调大，或用 `assign` 点名。

### 其它美术
- 通用露馅图（六指手、拼接体、变瘪等）在 `Resources/Art/Imposters/`，覆盖同名即可。
- 相机外壳 / 快门手在 `Resources/Art/Camera/`。相机外壳中央的透明取景窗会被游戏在运行时自动量取，换图后取景/拍照仍然对齐（保持中间是透明窟窿即可）。

### 导入设置是自动的
`Assets/Scripts/Editor/ArtImportPostprocessor.cs` 会对 `Characters / Imposters / Camera` 下的图自动设置：Sprite 类型、脚底为轴、100 PPU、无 mipmap、不压缩、透明正确；相机图额外开启可读像素。**美术不用碰导入选项。**

### 图片预处理（可选）
原图若带棋盘格/白底、需要裁剪或统一身高，可用：
```
python tools/process_art.py   # 去背景 / 裁剪到透明边界 / 按目标身高缩放
```

---

## 五、如何打包（一键出包）

Unity 顶部菜单栏 **GMTK**：
- **一键打包（当前平台）**（快捷键 `Ctrl+Shift+B`）：按当前激活的平台出包。
- **打包 Windows** / **打包 WebGL**：指定平台出包。
- **打开打包输出目录**：定位到输出文件夹。

输出在项目根目录 `Build/<平台>/` 下（已在 `.gitignore` 忽略，不会进仓库）。打包成功会弹窗并可一键打开目录。

命令行 / CI 也可调用：
```
Unity -quit -batchmode -projectPath . -executeMethod BuildTool.BuildWindows
Unity -quit -batchmode -projectPath . -executeMethod BuildTool.BuildWebGL
```

---

## 六、开发环境

1. 用 Unity **2023.1.22f1** 打开本工程。
2. 打开任意场景（`Assets/Scenes/SampleScene.unity`）直接 Play 即可运行——场景内容运行时自动生成。
