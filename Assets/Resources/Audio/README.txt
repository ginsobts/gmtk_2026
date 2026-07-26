音频资源放这里（缺文件时游戏静默，不报错）。
约定路径（文件名不带扩展名即可，.wav/.mp3/.ogg 都行）：

  Resources/Audio/BGM/<key>    循环背景乐
  Resources/Audio/SFX/<key>    一次性音效

==================== 需要的 BGM（放 Audio/BGM/） ====================
menu         主菜单
phase1       第一阶段（游戏开局）
phase2       第二阶段
phase3       第三阶段
death        死亡演出（红光+怪物追逐）
（阶段数跟随 phases.txt；有几个阶段就准备 phase1..phaseN）

==================== 需要的 SFX（放 Audio/SFX/） ====================
dialogue_open   打开对话
shutter         拍照快门
footstep1..4    玩家在地面上行走（随机轮播）
typewriter1..3  对话逐字显示（随机轮播）
mark            标记某人为嫌疑人（按 F）
unmark          取消标记
phase_enter     首次进入新阶段（旁白同时出现）
monster         死亡演出怪物出现
death           普通死亡（被怪物抓到）
special_death   特殊死亡（对话分支 / 人皮狗互动触发）
victory         指认成功、胜利
ui_click        选择对话分支等按钮点击

==================== 当前已接入（2026-07） ====================
BGM/phase1.mp3       通用“平静但诡异”BGM；菜单和缺少单独音乐的 phase 自动回退到此曲
SFX/footstep1..4.ogg 草地/沙土地脚步
SFX/ui_click.wav     鼠标点击 UI
SFX/typewriter1..3.wav  对话打字机单键声
SFX/shutter.ogg      相机快门

第三方素材来源及授权详见 THIRD_PARTY_AUDIO.md。

音量：在 AudioManager 组件（挂在运行时的 GameManager 物体上）上有
bgmVolume / sfxVolume，可在 Inspector 或代码里调。
