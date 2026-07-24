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

## 五、调试与布景（在 Scene 里可视化调）

> 本作**整局是运行时用代码程序化生成的**：编辑模式下 `SampleScene` 基本是空的，Play 后由 `GameBootstrap → GameManager` 搭出相机、玩家、NPC、场景与 UI。为方便可视化调试，提供了下面几套工具，**都不改变这套架构，缺任何配置也能正常跑**。

### 1) GameConfig —— Inspector 调参（免改代码、持久）
把相机 / 玩家 / NPC 的可调数值集中到一个资产里，改完下次 Play 生效。
- 菜单 **`GMTK/创建 GameConfig 资产`** → 生成 `Assets/Resources/GameData/GameConfig.asset`。
- 在 Inspector 里可调：
  - 相机：`Camera Field Of View`、`Camera Tilt`（俯角）、`Camera Offset`、`Camera Follow Lerp`
  - 玩家：`Player Start`、`Player Scale`、`Player Move Speed`、`Player Interact Range`
  - NPC：`Npc Scale`、`Npc Default Yaw`（默认朝向，0=正对相机）、`Npc Yaw Random`（朝向随机抖动）
  - 随机生成范围：`Spawn Area X / Z`（没有出生点时使用）
- 关卡数量仍走 `rounds.txt`，不在这里重复。
- **找不到该资产时用内置默认值**（与旧硬编码一致），所以删掉也不会坏。

### 2) 出生点 —— 在 Scene 里摆 NPC 位置与朝向
- 菜单 **`GMTK/创建出生点脚手架`** → 场景里生成 1 个 `PlayerSpawn` + 8 个 `NpcSpawn` 空物体。
- 直接在 Scene 视图里**拖动**改位置、**旋转 Y 轴**改朝向（`NpcSpawnPoint` 带朝向指示线 Gizmo）。
- 生成时：**有出生点就按出生点摆**（按名字顺序映射，多出的 NPC 随机生成）；没有出生点则整体随机。
- `NpcSpawnPoint` 上还可勾/取消 `Face Camera`、设 `Extra Yaw`。
- 出生点是**真正的场景物体**，会随场景保存。

### 3) 场景预览 —— 不按 Play 也能看到画面
编辑模式下 Scene 里什么都看不到时，用它生成一份可视化预览。
- 菜单 **`GMTK/预览场景（编辑器）`**（快捷键 `Ctrl+Shift+P`）→ 生成地面 + 游戏相机 + 玩家/NPC 占位，并**自动把 Scene 视角对齐到游戏相机**。
- 改了 GameConfig 或拖了出生点后，**再按一次 `Ctrl+Shift+P`** 刷新。
- **`GMTK/Scene 视角对齐游戏相机`**：只重新对准取景，不生成占位。
- **`GMTK/清除场景预览`**：移除预览。
- 预览对象带 `DontSave` 标记：**不会存进场景、不会进最终包**，且**进入 Play 会自动清除**，不与运行时生成的内容冲突。缺角色图时用红色占位块顶替。

### 4) 运行时调试面板（F1）
Play 时按 **`F1`** 呼出（仅编辑器 / 开发包）：
- **冻结跟随/转向**：暂停相机跟随、billboard 转向、呼吸/摇摆/掉帧抖动——这样能在 Play 时切到 Scene 视图自由观察、拖动而不被代码每帧改回去。
- 相机 **FOV / 俯角 / 偏移 / 跟随** 实时滑块，即改即看。
- **保存到 GameConfig**（编辑器内）：把当前相机参数写回资产，不用手抄；**打印当前数值到 Console** 也可以。

> 关于「每个 NPC 独立朝向」：NPC 默认是正对相机的 billboard；`Npc Default Yaw` / 出生点旋转 / `Npc Yaw Random` 可让他们各朝各的方向。角度别太大（约 ±40° 内），否则平面卡片会显得偏薄——这是 2.5D 卡片的固有限制。

---

## 六、如何打包（一键出包）

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

## 七、开发环境

1. 用 Unity **2023.1.22f1** 打开本工程。
2. 打开任意场景（`Assets/Scenes/SampleScene.unity`）直接 Play 即可运行——场景内容运行时自动生成。
3. 想在编辑模式下可视化调相机/摆位，见「五、调试与布景」。
