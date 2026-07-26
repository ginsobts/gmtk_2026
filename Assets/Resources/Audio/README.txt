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
mark            标记某人为嫌疑人（按 F）
unmark          取消标记
phase_enter     首次进入新阶段（旁白同时出现）
monster         死亡演出怪物出现
death           普通死亡（被怪物抓到）
special_death   特殊死亡（对话分支 / 人皮狗互动触发）
victory         指认成功、胜利
ui_click        选择对话分支等按钮点击

音量：在 AudioManager 组件（挂在运行时的 GameManager 物体上）上有
bgmVolume / sfxVolume，可在 Inspector 或代码里调。
