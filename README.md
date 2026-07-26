# 人类公司（Human Company · GMTK 2026）

一款 2.5D 观察解谜小游戏：受邀来「人类公司」拍纪录片的网红，在大楼里混进了若干「炼化人」。你拿着数码相机通过**观察、对话、拍照**找出他们，标记后一次性提交甄别。

- 引擎：Unity **2023.1.22f1**（Built-in 渲染管线）
- 整个场景在运行时由脚本程序化生成，任意场景启动即可（`GameBootstrap` 自动拉起 `GameManager`）。
- **主菜单**：启动进入主菜单，可「开始游戏 / 制作者名单 / 切换语言 / 退出游戏」。
- **中英双语**：默认英文，可在主菜单一键切换中/英，选择会被记住（`PlayerPrefs`）。所有 UI 文案与角色名、对话都支持双语配置。
- **单局固定剧本**：每局 **13 名角色**（8 种炼化人各 1 + 5 真人）全部出场，身份写死在 `characters.txt` 的 `kind` 列。
- **拍照不限次数**：无胶卷上限，每次拍照仍推进时间轴 +1。

---

## 一、怎么玩

### 操作一览

**大厅 / 探索**
| 操作 | 按键 |
| --- | --- |
| 移动 | `W A S D` / 方向键 |
| 交谈（靠近 NPC） | `E` |
| 查看该角色的照片 | `Q` |
| 标记 / 取消标记炼化人 | `F` |
| 打开相机 | `空格` |
| 打开相册（全部素材） | `Tab` |
| 打开甄别名单 | `M` |

**相机模式**
| 操作 | 按键 |
| --- | --- |
| 移动取景框 | 鼠标 |
| 让取景框内的人「比耶」 | `1` |
| 让取景框内的人「笑一下」 | `2` |
| 按快门拍照 | `空格` |
| 退出相机 | `Esc` |

> 拍照所见即所拍：相册里的素材 = 你在取景框里看到的画面。

### 主菜单

启动后先进入主菜单：
- **开始游戏**：进入公司大楼开始一局。
- **制作者名单**：查看 Credits（内容可在 `ui.txt` 的 `credits.body` 里改，`Esc` 或「返回」退出）。
- **语言：English / 中文**：一键在中英之间切换，全部界面即时刷新，选择会被记住。
- **退出游戏**：退出（编辑器内为停止运行）。

结算界面除「再玩一次」外，也可「返回主菜单」。

### 游玩流程

1. **探索大楼**：走近职员，用 `E` 聊天听话术、用相机拍照留证。
2. **找破绽**：不同炼化人破绽不同（见下表），有的靠对话、有的靠摆姿势、有的只有拍出来的照片里才露馅。
3. **标记炼化人**：确定可疑对象后靠近按 `F` 标记（此时**不会告诉你对错**，被标记的人会泛橙黄色）。
4. **提交甄别**：按 `M` 打开「甄别名单」，检查名单 → 点「提交甄别」→ 弹窗确认。**提交后本局立即结束。**
5. **看结算**：公布你指认的正误、本局真正的炼化人名单，以及胜负结局正文。**正确标记全部 8 名炼化人、无误指任何真人**才算完美通关。

### 时间阶段

时间轴为整局累计整数：**拍照 +1**，**完整对话 +3**（Esc 中断对话不推进）。

| 阶段 | 阈值 | 含义 |
| --- | --- | --- |
| 上午 | 0 | 参观初期，炼化痕迹尚浅 |
| 下午 | 30 | 办公区深入，异常增多 |
| 晚上 | 60 | 必须完成甄别 |

时间轴满格为 **90**（末段与上一段等长）。推进：拍照 +1，完整对话 +3。时间轴满后自动打开甄别名单并弹出提交确认，与手动提交走同一结算流程。

### 本局固定身份（8 炼化 / 5 真人）

**炼化人（8 种各 1）**

| charId | 角色 | kind |
| --- | --- | --- |
| npc_00 | 林采 | DouBao（话术炼化体） |
| npc_02 | 陈维 | FrameDrop（帧同步异常体） |
| npc_04 | 赵岩 | Deflate（空壳皮囊体） |
| npc_06 | 吴昂 | Stitched（模型拼接体） |
| npc_08 | 魏大爷 | DogSkin（狗皮人） |
| npc_10 | 韩露 | SixFinger（肢体渲染异常体） |
| npc_11 | 顾映 | ScarySmile（表情映射失败体） |
| npc_12 | 程书 | PhotoMissing（光学消隐体） |

**真人（5 名，各有专属 count 台词线）**

| charId | 角色 | 说明 |
| --- | --- | --- |
| npc_01 | 王建国 | 老板，爹味说教（count 1~3） |
| npc_03 | 苏晴 | HR，电话旁白递进透露炼化线索（count 1~4） |
| npc_05 | 方晓 | 通宵加班，神志不清（count 1~3，立绘 tired/dazed） |
| npc_07 | 陆远 | 眼伤男，滴眼药水吐槽工作量（count 1~3） |
| npc_09 | 安安 | 门口走失小女孩；前 4 次仅「……」，第 5 次起特殊台词 |

炼化人数量由 `characters.txt` 自动统计（`kind != Normal`）→ HUD 显示「炼化人共 8」。

**炼化人对话模式**
- **林采 / 魏大爷**：`phase` 模式，随时间阶段（上午/下午/晚上）换台词。
- **陈维 / 赵岩 / 吴昂 / 韩露 / 顾映 / 程书**：`count` 模式，第 1~3 次完整对话递进（自我介绍 → 略异常 → 明显诡异），第 4 次起重复第 3 档。

### 八种炼化人 & 破绽

| 类型 | 破绽线索 |
| --- | --- |
| 话术炼化体 DouBao | 对话一股 AI / 公关官方腔 |
| 肢体渲染异常体 SixFinger | 让 TA「比耶」时露出六根手指 |
| 表情映射失败体 ScarySmile | 让 TA「笑」时变成恐怖脸 |
| 帧同步异常体 FrameDrop | 在镜头里不停抖动 / 瞬移 |
| 模型拼接体 Stitched | 拍出来的照片里身体是拼接的 |
| 光学消隐体 PhotoMissing | 真人在取景框里，但照片里没有 TA |
| 空壳皮囊体 Deflate | 玩家靠近接触时会「变瘪」 |
| 狗皮人 DogSkin | 三阶段对话异化：正常遛狗 → 狗丢了 → **无台词 + 恐怖立绘** |

---

## 二、目录结构（关键部分）

```
Assets/
  Resources/
    GameData/            # 策划配置表（Tab 分隔 .txt）
      characters.txt     # 角色（含 kind 固定身份）
      dialogue.txt       # 对话（含 portraitId）
      phases.txt         # 时间阶段
      npc_dialogues.txt  # 角色对话模式（阶段/次数/固定）
      portraits.txt      # 对话 UI 立绘映射
      rounds.txt         # 单局占位（仅 roundId）
      ui.txt             # 界面文案 / 菜单 / 制作名单（中英双语）
    Art/
      Characters/        # 场景全身立绘
      Portraits/         # 对话 UI 立绘（独立资源）
      Imposters/         # 通用露馅立绘（六指手、拼接、变瘪等）
      Camera/            # 相机外壳、快门手
  Scripts/
    Game/                # 运行时逻辑
    Editor/              # 打包工具、美术导入后处理（不进最终包）
tools/
  export_tables.py       # 策划 Excel → .txt 导出脚本
  import_tables.py       # .txt → 生成可编辑 Excel（首次初始化用）
  process_art.py         # 美术图批处理（去背景 / 裁剪 / 对齐身高）
```

---

## 三、策划：如何配置对话与关卡

游戏运行时**只读 `Assets/Resources/GameData/` 下的 `.txt`**（Unity 打包不认 `.xlsx`）。但你**配置时用 Excel 就行**，中英各一列，改完一键导成 txt——txt 只是"游戏吃的格式"，不用手写。`#` 开头的行是注释会被忽略，首个非注释行是表头。

> **双语说明**：需要显示给玩家的文本都拆成 `en` / `zh` 两列。游戏按当前语言取对应列；某列留空会自动回退到另一语言。

### 推荐工作流（在 Excel 里编辑，最省事）
用 Unity 菜单，全程不碰命令行（本机需装 Python 并 `pip install openpyxl`）：
1. 首次：Unity 顶部菜单 **GMTK → 配置表：txt → Excel（生成可编辑表）**。会用当前内容生成 `tools/game_tables.xlsx`（含 `characters` / `dialogue` / `rounds` / `ui` / `phases` / `npc_dialogues` / `portraits` 七个 sheet，列已排好、中英分列），并自动打开所在文件夹。
2. 用 Excel 编辑这个表：加角色、改台词、调关卡、翻译文案。多行文本直接在单元格里回车换行即可。
3. 改完：Unity 菜单 **GMTK → 配置表：Excel → txt（导入游戏）**。会把 Excel 导成 `Resources/GameData/*.txt` 并刷新，下次 Play 生效。

> 命令行等价物（不想用菜单时）：
> ```
> pip install openpyxl
> python tools/import_tables.py     # txt → 生成可编辑 game_tables.xlsx
> python tools/export_tables.py     # game_tables.xlsx → 覆盖生成 txt
> ```

也可以直接手改 `.txt`（**Tab 分隔**，`#` 开头是注释，首个非注释行是表头，单元格换行写成 `\n`）——但推荐走 Excel。

### characters.txt — 角色主表
`charId  artFolder  dialogueId  name_en  name_zh  kind  title_en  title_zh`
- `kind`：固定身份，`Normal` 或炼化人类型 enum 名（`DouBao`、`FrameDrop`、`DogSkin` 等）。
- `title_en` / `title_zh`：岗位（可选）。有值时 UI 各处在名字右侧以**更小字号、灰色**显示岗位（如 `林采  公关`）；魏大爷、安安等非职员可留空。
- 游戏按表顺序生成**全部**角色，不再随机抽人。

### dialogue.txt — 台词表
`dialogueId  order  portraitId  en  zh`
- 每个 NPC 的台词 id 由 `npc_dialogues.txt`（或 `characters.txt` 的 `dialogueId`）指定，不再按 `kind` 回退。
- 阶段 3 狗皮人可留空 `en`/`zh`，仅显示 `uncle_horror` 立绘。

### phases.txt — 时间阶段表
`phaseId  order  threshold  name_en  name_zh`

### npc_dialogues.txt — 角色对话模式表
`charId  mode  index  dialogueId`
- `mode`：`phase`（按当前阶段）、`count`（按对话次数）、`static`（固定）
- 炼化人：林采/魏大爷用 `phase`；陈维/赵岩/吴昂/韩露/顾映/程书用 `count`（见上表）

### portraits.txt — 对话 UI 立绘表
`portraitId  imagePath`

### ui.txt — 界面文案表
`key  en  zh`
- 标题「人类公司」、炼化人显示名（`kind.*`）、胜负结局（`result.win.body` / `result.lose.body`）
- `result.detail` 含 `{5}` 占位符插入结局正文

### rounds.txt — 关卡表
`roundId`
- 单局固定剧本，此表仅作占位；身份与人数均由 `characters.txt` 决定。

> 容错：任何表缺失或字段写错，游戏会打印警告并回退到内置默认内容，不会崩。

---

## 四、美术：如何替换 / 新增资源

### 换角色立绘（最常见）
```
Resources/Art/Characters/<charId>/
  base.png     # 必需
  smile.png    # 可选
  yeah.png     # 可选

Resources/Art/Portraits/<charId>/    # 对话 UI 立绘
  ...                                # 在 portraits.txt 里映射 portraitId
```

### 新增魏大爷（npc_08）类角色
1. 建 `Characters/npc_08/` 与 `Portraits/npc_08/`（`normal` / `anxious` / `horror` 等）。
2. 在 `characters.txt` 加一行并指定 `kind`。
3. 在 `npc_dialogues.txt` 配置 phase 三阶段线。

### 其它美术
- 通用露馅图在 `Resources/Art/Imposters/`。
- 相机外壳 / 快门手在 `Resources/Art/Camera/`。

---

## 五、调试与布景（在 Scene 里可视化调）

> 本作**整局是运行时用代码程序化生成的**。为方便可视化调试，提供了 GameConfig、出生点、场景预览、F1 调试面板等工具。

### NPC 固定位置（每局一致）
- 每个角色的出生坐标写在 **`Assets/Resources/GameData/spawns.txt`**（列：`charId  x  z  yaw  faceCamera`，`x/z` 为世界坐标，`faceCamera=1` 时始终正对相机）。开局按表摆放，**不再随机**；表里没配到的角色回退到一份确定性网格布局（仍每局一致）。
- **手动挪棋子（推荐）**：Play 时按 **`F2`** 进入「摆位模式」——场景会冻结，用**鼠标左键**在某个棋子附近按住并拖动即可把它挪到落点；调好后点面板上的 **「保存位置到 spawns.txt」**，会把当前所有棋子位置写回该表（仅编辑器/开发包可写盘）。下次运行即生效。
- 也可直接用文本 / Excel 编辑 `spawns.txt`（Excel 工作流见第四节，已含 `spawns` 页）。

### 场景预览 / GameConfig / F1 调试
见原工程说明：菜单 **`GMTK/预览场景`**, **`GMTK/创建 GameConfig 资产`**, Play 时 **F1** 调试面板。

---

## 六、如何打包（一键出包）

Unity 顶部菜单栏 **GMTK**：
- **一键打包（当前平台）**（快捷键 `Ctrl+Shift+B`）
- **打包 Windows** / **打包 WebGL**
- **打开打包输出目录**

命令行 / CI：
```
Unity -quit -batchmode -projectPath . -executeMethod BuildTool.BuildWindows
Unity -quit -batchmode -projectPath . -executeMethod BuildTool.BuildWebGL
```

---

## 七、开发环境

1. 用 Unity **2023.1.22f1** 打开本工程。
2. 打开任意场景（`Assets/Scenes/SampleScene.unity`）直接 Play 即可运行。
3. 想在编辑模式下可视化调相机/摆位，见「五、调试与布景」。
