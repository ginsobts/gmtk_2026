using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>支持的语言。默认英文。</summary>
public enum Lang { EN, ZH }

/// <summary>
/// 轻量本地化：所有 UI 文案通过 key 取值，支持英文/中文。
/// 文案来自 Resources/GameData/ui.txt（key\ten\tzh），缺失则回退到内置默认。
/// 切换语言会触发 OnChanged，UI 据此重刷。玩家选择用 PlayerPrefs 记住。
/// </summary>
public static class Loc
{
    const string PrefKey = "gmtk_lang";

    static bool _loaded;
    static Lang _current = Lang.EN;
    static Dictionary<string, string[]> _table;   // key -> [en, zh]

    /// <summary>语言切换时触发（UI 订阅后重刷文案）。</summary>
    public static event Action OnChanged;

    public static Lang Current
    {
        get { EnsureLoaded(); return _current; }
    }

    public static void Set(Lang lang)
    {
        EnsureLoaded();
        if (_current == lang) return;
        _current = lang;
        PlayerPrefs.SetInt(PrefKey, (int)lang);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }

    public static void Toggle() => Set(_current == Lang.EN ? Lang.ZH : Lang.EN);

    /// <summary>当前语言下的显示名（用于语言按钮）。</summary>
    public static string LanguageName => _current == Lang.EN ? "English" : "中文";

    /// <summary>取本地化文案。找不到 key 时返回 key 本身，便于发现漏配。</summary>
    public static string Get(string key)
    {
        EnsureLoaded();
        if (key != null && _table.TryGetValue(key, out var pair))
        {
            int i = (int)_current;
            if (pair != null && i < pair.Length && !string.IsNullOrEmpty(pair[i])) return pair[i];
            if (pair != null && pair.Length > 0 && !string.IsNullOrEmpty(pair[0])) return pair[0]; // 回退英文
        }
        return key;
    }

    public static string Format(string key, params object[] args) => string.Format(Get(key), args);

    /// <summary>从两个候选里按当前语言取一个（用于 CharacterDef/对话这种表内双语列）。</summary>
    public static string Pick(string en, string zh)
    {
        EnsureLoaded();
        if (_current == Lang.ZH) return string.IsNullOrEmpty(zh) ? en : zh;
        return string.IsNullOrEmpty(en) ? zh : en;
    }

    static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        _table = new Dictionary<string, string[]>(Defaults());

        // 覆盖/补充：从 ui.txt 读取（存在则以表为准）
        try
        {
            var asset = Resources.Load<TextAsset>("GameData/ui");
            if (asset != null)
            {
                bool header = false;
                foreach (var raw in asset.text.Split('\n'))
                {
                    string line = raw.TrimEnd('\r');
                    if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#")) continue;
                    if (!header) { header = true; continue; }
                    var c = line.Split('\t');
                    if (c.Length < 2 || string.IsNullOrEmpty(c[0])) continue;
                    string en = Unescape(c.Length > 1 ? c[1] : "");
                    string zh = Unescape(c.Length > 2 ? c[2] : "");
                    _table[c[0].Trim()] = new[] { en, zh };
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Loc] 读取 ui.txt 失败，使用内置默认：{e.Message}");
        }

        if (PlayerPrefs.HasKey(PrefKey))
            _current = (Lang)PlayerPrefs.GetInt(PrefKey);
        else
            _current = Lang.EN; // 默认英文
    }

    static string Unescape(string s) => string.IsNullOrEmpty(s) ? s : s.Replace("\\n", "\n").Trim();

    // ---------------- 内置默认文案（读表失败时的兜底） ----------------
    static Dictionary<string, string[]> Defaults() => new Dictionary<string, string[]>
    {
        // 主菜单
        ["menu.title"] = new[] { "The Imposter Hunt", "寻找炼化人" },
        ["menu.subtitle"] = new[] { "A 2.5D observation game", "2.5D 观察解谜小游戏" },
        ["menu.start"] = new[] { "Start Game", "开始游戏" },
        ["menu.credits"] = new[] { "Credits", "制作者名单" },
        ["menu.quit"] = new[] { "Quit", "退出游戏" },
        ["menu.language"] = new[] { "Language: {0}", "语言：{0}" },
        ["credits.title"] = new[] { "Credits", "制作者名单" },
        ["credits.back"] = new[] { "Back", "返回" },
        ["credits.body"] = new[]
        {
            "The Imposter Hunt\n\nGame Design & Programming: Your Name\nArt: Your Name\nSpecial Thanks: GMTK 2026\n\nMade with Unity\n\nAudio Credits\nMusic: “Sirens in Darkness” — The Cynic Project / cynicmusic.com / pixelsphere.org\nUI Audio: Kenney / Kenney.nl",
            "寻找炼化人\n\n策划 & 程序：你的名字\n美术：你的名字\n特别鸣谢：GMTK 2026\n\n使用 Unity 制作\n\n音频署名\n音乐：《Sirens in Darkness》— The Cynic Project / cynicmusic.com / pixelsphere.org\n界面音效：Kenney / Kenney.nl"
        },
        ["credits.center"] = new[] { "We couldn't agree on how to order the credits,\nso we made them spin instead.", "因为不想纠结怎么排序，\n所以做成旋转的了。" },
        ["credits.member.silver"] = new[] { "silver\n<size=21><color=#AAB2C4>Programming</color></size>", "silver\n<size=21><color=#AAB2C4>程序</color></size>" },
        ["credits.member.zhanzhan"] = new[] { "詹詹\n<size=21><color=#AAB2C4>Design · Programming</color></size>", "詹詹\n<size=21><color=#AAB2C4>策划 · 程序</color></size>" },
        ["credits.member.zaptaind"] = new[] { "zaptaind\n<size=21><color=#AAB2C4>Design · Art</color></size>", "zaptaind\n<size=21><color=#AAB2C4>策划 · 美术</color></size>" },
        ["credits.member.yigubigu"] = new[] { "依古比古\n<size=21><color=#AAB2C4>Design · Art</color></size>", "依古比古\n<size=21><color=#AAB2C4>策划 · 美术</color></size>" },
        ["credits.member.viktor"] = new[] { "Viktor Tu\n<size=21><color=#AAB2C4>Programming</color></size>", "Viktor Tu\n<size=21><color=#AAB2C4>程序</color></size>" },
        ["credits.ai.gpt"] = new[] { "GPT5.6\n<size=21><color=#AAB2C4>AI Model</color></size>", "GPT5.6\n<size=21><color=#AAB2C4>AI 模型</color></size>" },
        ["credits.ai.opus48"] = new[] { "Opus4.8\n<size=21><color=#AAB2C4>AI Model</color></size>", "Opus4.8\n<size=21><color=#AAB2C4>AI 模型</color></size>" },
        ["credits.ai.composer"] = new[] { "Composer2.5\n<size=21><color=#AAB2C4>AI Model</color></size>", "Composer2.5\n<size=21><color=#AAB2C4>AI 模型</color></size>" },
        ["credits.ai.doubao"] = new[] { "豆包\n<size=21><color=#AAB2C4>AI Model</color></size>", "豆包\n<size=21><color=#AAB2C4>AI 模型</color></size>" },
        ["credits.ai.opus46"] = new[] { "Opus4.6\n<size=21><color=#AAB2C4>AI Model</color></size>", "Opus4.6\n<size=21><color=#AAB2C4>AI 模型</color></size>" },
        ["credits.audio"] = new[] { "Music: “Sirens in Darkness” — The Cynic Project / cynicmusic.com / pixelsphere.org   ·   UI Audio: Kenney / Kenney.nl   ·   Font: Noto Sans SC (SIL OFL 1.1)", "音乐：《Sirens in Darkness》— The Cynic Project / cynicmusic.com / pixelsphere.org   ·   界面音效：Kenney / Kenney.nl   ·   字体：Noto Sans SC（SIL OFL 1.1）" },

        // 开场目标说明
        ["briefing.title"] = new[] { "Your Objective", "你的目标" },
        ["briefing.body"] = new[]
        {
            "Something is wrong at the company.\n\nHidden among your coworkers are <b>Refined Ones</b> — imposters wearing human skins. Each one has a subtle tell.\n\n<b>·</b>  Walk up and <b>talk [E]</b> to people\n<b>·</b>  Raise your <b>camera [Space]</b> and photograph them — some tells only show through the lens\n<b>·</b>  <b>Mark [F]</b> anyone you suspect\n<b>·</b>  Open the <b>Accuse List [M]</b> and submit before time runs out\n\nAs the day goes on, things get darker. Find them all — and don't get too close to the wrong one.",
            "公司里有些不对劲。\n\n同事之中混进了<b>炼化人</b>——披着人皮的伪装者，每一个都有细微的破绽。\n\n<b>·</b>  走近并<b>交谈 [E]</b>\n<b>·</b>  举起<b>相机 [空格]</b>拍照——有些破绽只有透过镜头才看得见\n<b>·</b>  对可疑的人<b>标记 [F]</b>\n<b>·</b>  打开<b>指认列表 [M]</b>，在时间耗尽前提交\n\n随着时间推进，一切会变得越来越危险。找出全部炼化人——也别太靠近不该靠近的人。"
        },
        ["briefing.confirm"] = new[] { "Got it — Start [Enter]", "明白了，开始 [回车]" },

        // HUD
        ["hud.film"] = new[] { "Film {0}", "胶卷 {0}" },
        ["hud.marked"] = new[] { "Marked {0} (imposters: {1})", "已标记 {0}（炼化人共 {1}）" },
        ["phase.enter"] = new[] { "Phase shift: {0}", "阶段切换：{0}" },
        ["hud.prompt"] = new[] { "Move [WASD]   Photo [Space]   Album [Tab]   Accuse List [M]", "移动 [WASD]　拍照 [空格]　相册 [Tab]　指认列表 [M]" },
        ["hud.skip_phase"] = new[] { "Skip ▶", "快进 ▶" },
        ["btn.camera"] = new[] { "Camera (Space)", "相机 (空格)" },
        ["btn.album"] = new[] { "Album (Tab)", "相册 (Tab)" },
        ["btn.marklist"] = new[] { "Accuse List (M)", "指认列表 (M)" },
        ["btn.labelsShow"] = new[] { "Show Names", "显示名字" },
        ["btn.labelsHide"] = new[] { "Hide Names", "隐藏名字" },

        // 靠近交互
        ["interact.talk"] = new[] { "Talk [E]", "交谈 [E]" },
        ["interact.viewphotos"] = new[] { "View Photos [Q]", "查看照片 [Q]" },
        ["interact.mark"] = new[] { "Mark Suspect [F]", "标记嫌疑人 [F]" },
        ["interact.unmark"] = new[] { "Unmark [F]", "取消标记 [F]" },
        ["interact.markedSuffix"] = new[] { " (marked)", "（已标记）" },

        // 对话
        ["dlg.next"] = new[] { "Continue \u25B6", "继续 \u25B6" },
        ["dlg.end"] = new[] { "End", "结束对话" },
        ["robot.reward.title"] = new[] { "Companion Acquired!", "获得伙伴！" },
        ["robot.reward.body"] = new[] { "You got the Little Me robot!\nIt will follow you around and keep you company.", "你获得了小我机器人！\n它会跟着你、陪你逛逛。" },
        ["robot.reward.confirm"] = new[] { "Take It With Me", "确认带上它" },

        // 相机
        ["cam.tip"] = new[] { "Move mouse to aim;  [1] Peace   [2] Smile   [Space] Shoot   [Esc] Exit", "移动鼠标瞄准；[1] 比耶　[2] 笑　[空格] 拍照　[Esc] 退出" },
        ["cam.framed"] = new[] { "In frame: {0}", "在镜头中：{0}" },
        ["cam.framedNone"] = new[] { "In frame: (none)", "在镜头中：（无）" },
        ["cam.peace"] = new[] { "Peace [1]", "比个耶 [1]" },
        ["cam.smile"] = new[] { "Smile [2]", "笑一下 [2]" },
        ["cam.shutter"] = new[] { "Shutter [Space]", "快门 [空格]" },
        ["cam.exit"] = new[] { "Exit [Esc]", "退出 [Esc]" },
        ["cam.poseSmile"] = new[] { "\u201CEveryone smile~\u201D", "\u201C大家笑一个～\u201D" },
        ["cam.posePeace"] = new[] { "\u201CGive me a peace sign!\u201D", "\u201C来，比个耶！\u201D" },
        ["cam.nofilm"] = new[] { "No film left!", "没有胶卷了！" },
        ["cam.shot"] = new[] { "Snap! Saved to album.", "咔嚓！进相册了。" },

        // 相册
        ["album.titleAll"] = new[] { "Album — All Photos ({0})", "相册 —— 全部照片（{0} 张）" },
        ["album.titleChar"] = new[] { "{0}'s Photos ({1})", "{0} 的照片（{1} 张）" },
        ["album.empty"] = new[] { "No photos yet. Take some with the camera first.", "还没有照片，先用相机拍几张吧。" },
        ["album.close"] = new[] { "Close (Esc)", "关闭 (Esc)" },
        ["album.inphoto"] = new[] { "In this photo:{0}", "照片里有：{0}" },
        ["album.inphotoNone"] = new[] { "In this photo:\n(nobody)", "照片里有：\n（无人）" },
        ["album.accuse"] = new[] { "Accuse (ticked)", "指认（勾选的）" },
        ["album.accuseHint"] = new[] { "Tick the photos of ALL imposters, then Accuse.", "勾选出所有炼化人的照片，再点指认。" },
        ["accuse.needphoto"] = new[] { "Please tick at least one photo first.", "请先勾选照片。" },

        // 指认列表 / 提交
        ["mark.title"] = new[] { "Accuse List — Your Suspects", "指认列表 —— 你标记的嫌疑人" },
        ["mark.sub"] = new[] { "Click \u201CSubmit\u201D when you are sure. Note: the round ends immediately after submitting.", "确认无误后点击\u201C提交指认\u201D。注意：提交后本局立即结束。" },
        ["mark.empty"] = new[] { "You haven't marked anyone.\nGo near an NPC and press [F] to mark a suspect.", "你还没有标记任何人。\n回到场景靠近 NPC 按 [F] 标记你怀疑的对象。" },
        ["mark.submit"] = new[] { "Submit Accusation", "提交指认" },
        ["mark.back"] = new[] { "Back [Esc]", "返回场景 [Esc]" },
        ["mark.remove"] = new[] { "Remove", "移除标记" },
        ["confirm.title"] = new[] { "Confirm Accusation", "确认提交指认" },
        ["confirm.yes"] = new[] { "Confirm", "确认提交" },
        ["confirm.no"] = new[] { "Reconsider", "再想想" },
        ["confirm.body"] = new[] { "You will accuse these {0} as imposters:\n\n{1}\n\nThe round ends immediately and can't be changed. Sure?", "你将指认以下 {0} 人为炼化人：\n\n{1}\n\n提交后本局立即结束，且无法修改。确定吗？" },
        ["confirm.bodyEmpty"] = new[] { "You haven't marked anyone.\nSubmitting now counts as \u201Cfound no imposters\u201D.\n\nThe round ends immediately. Sure?", "你还没有标记任何人。\n若直接提交，将视为\u201C没有找出任何炼化人\u201D。\n\n提交后本局立即结束，确定吗？" },

        // Toast
        ["toast.marked"] = new[] { "Marked \u201C{0}\u201D as a suspect (pending)", "已标记\u300C{0}\u300D为嫌疑人（待提交）" },
        ["toast.unmarked"] = new[] { "Unmarked \u201C{0}\u201D", "已取消标记\u300C{0}\u300D" },

        // 结算
        ["result.win"] = new[] { "All Uncovered!", "全部识破！" },
        ["result.lose"] = new[] { "Investigation Over", "调查结束" },
        ["narrate.win"] = new[] { "You're safe... for now.", "你安全了，暂时。" },
        ["narrate.lose"] = new[] { "You're out of chances.", "你已经没有机会了。" },
        ["death.flee"] = new[] { "Something is coming. RUN.", "有什么东西来了。快跑。" },
        ["death.normal"] = new[] { "It caught you.", "它抓住了你。" },
        ["death.special"] = new[] { "You shouldn't have looked closer.", "你不该再靠近看的。" },
        ["result.detail"] = new[] { "You correctly identified {0} / {1} imposters.", "你成功识破了 {0} / {1} 名炼化人。" },
        ["result.none"] = new[] { "(none)", "（无）" },
        ["result.replay"] = new[] { "Play Again", "再玩一次" },
        ["result.menu"] = new[] { "Main Menu", "返回主菜单" },

        // 新手引导
        ["tutorial.next"] = new[] { "Continue ▶", "继续 ▶" },
        ["tutorial.progress"] = new[] { "{0}/{1}", "{0}/{1}" },
        ["tutorial.s1"] = new[] { "This is the town. Walk up to anyone — press E to talk, or hold up your camera (Space) to photograph them. Some tells only show in the photo.", "这里是小镇。走近任何人：按 E 交谈，或举起相机（空格）拍照。有些破绽只有在照片里才看得出来。" },
        ["tutorial.s2"] = new[] { "This bar is your time. Talking and taking photos both cost time. Find the imposters before the countdown runs out.", "上方这条是你的时间。交谈和拍照都会消耗时间，必须在倒计时结束前找出所有伪人。" },
        ["tutorial.s3"] = new[] { "Stand near an NPC and press F to mark them as a suspect. Open the Accusation List (bottom-right) and submit before time runs out — get everyone right to win.", "靠近某个 NPC 按 F 可将其标记为嫌疑人。在时间用完前打开右下角「指认列表」提交结果，全部猜对即获胜。" },

        // 炼化人类型名
        ["kind.normal"] = new[] { "Normal", "普通人" },
        ["kind.doubao"] = new[] { "AI Bot", "豆包人" },
        ["kind.sixfinger"] = new[] { "Six-Finger", "六指人" },
        ["kind.scarysmile"] = new[] { "Scary Smile", "一笑变可怕人" },
        ["kind.framedrop"] = new[] { "Frame-Drop", "掉帧人" },
        ["kind.stitched"] = new[] { "Stitched", "拼接人" },
        ["kind.photomismatch"] = new[] { "Photo-Mismatch", "照片不符人" },
        ["kind.deflate"] = new[] { "Deflate", "变瘪人" },
        ["kind.lookaway"] = new[] { "Look-Away", "悲工坊人" },
        ["kind.skindog"] = new[] { "Skin-Dog", "人皮狗" },
    };
}
