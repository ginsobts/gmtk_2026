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
        ["menu.title"] = new[] { "The Imposter Hunt", "寻找伪人" },
        ["menu.subtitle"] = new[] { "A 2.5D observation game", "2.5D 观察解谜小游戏" },
        ["menu.start"] = new[] { "Start Game", "开始游戏" },
        ["menu.credits"] = new[] { "Credits", "制作者名单" },
        ["menu.quit"] = new[] { "Quit", "退出游戏" },
        ["menu.language"] = new[] { "Language: {0}", "语言：{0}" },
        ["credits.title"] = new[] { "Credits", "制作者名单" },
        ["credits.back"] = new[] { "Back", "返回" },
        ["credits.body"] = new[]
        {
            "The Imposter Hunt\n\nGame Design & Programming: Your Name\nArt: Your Name\nSpecial Thanks: GMTK 2026\n\nMade with Unity",
            "寻找伪人\n\n策划 & 程序：你的名字\n美术：你的名字\n特别鸣谢：GMTK 2026\n\n使用 Unity 制作"
        },

        // HUD
        ["hud.film"] = new[] { "Film {0}", "胶卷 {0}" },
        ["hud.marked"] = new[] { "Marked {0} (imposters: {1})", "已标记 {0}（伪人共 {1}）" },
        ["hud.prompt"] = new[] { "Move [WASD]   Photo [Space]   Album [Tab]   Accuse List [M]", "移动 [WASD]　拍照 [空格]　相册 [Tab]　指认列表 [M]" },
        ["btn.camera"] = new[] { "Camera (Space)", "相机 (空格)" },
        ["btn.album"] = new[] { "Album (Tab)", "相册 (Tab)" },
        ["btn.marklist"] = new[] { "Accuse List (M)", "指认列表 (M)" },

        // 靠近交互
        ["interact.talk"] = new[] { "Talk [E]", "交谈 [E]" },
        ["interact.viewphotos"] = new[] { "View Photos [Q]", "查看照片 [Q]" },
        ["interact.mark"] = new[] { "Mark Suspect [F]", "标记嫌疑人 [F]" },
        ["interact.unmark"] = new[] { "Unmark [F]", "取消标记 [F]" },
        ["interact.markedSuffix"] = new[] { " (marked)", "（已标记）" },

        // 对话
        ["dlg.next"] = new[] { "Continue \u25B6", "继续 \u25B6" },
        ["dlg.end"] = new[] { "End", "结束对话" },

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
        ["cam.shot"] = new[] { "Snap! Saved to album (film left {0})", "咔嚓！进相册了（剩余胶卷 {0}）" },

        // 相册
        ["album.titleAll"] = new[] { "Album — All Photos ({0})", "相册 —— 全部照片（{0} 张）" },
        ["album.titleChar"] = new[] { "{0}'s Photos ({1})", "{0} 的照片（{1} 张）" },
        ["album.empty"] = new[] { "No photos yet. Take some with the camera first.", "还没有照片，先用相机拍几张吧。" },
        ["album.close"] = new[] { "Close (Esc)", "关闭 (Esc)" },
        ["album.inphoto"] = new[] { "In photo: {0}", "照片里：{0}" },

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
        ["confirm.body"] = new[] { "You will accuse these {0} as imposters:\n\n{1}\n\nThe round ends immediately and can't be changed. Sure?", "你将指认以下 {0} 人为伪人：\n\n{1}\n\n提交后本局立即结束，且无法修改。确定吗？" },
        ["confirm.bodyEmpty"] = new[] { "You haven't marked anyone.\nSubmitting now counts as \u201Cfound no imposters\u201D.\n\nThe round ends immediately. Sure?", "你还没有标记任何人。\n若直接提交，将视为\u201C没有找出任何伪人\u201D。\n\n提交后本局立即结束，确定吗？" },

        // Toast
        ["toast.marked"] = new[] { "Marked \u201C{0}\u201D as a suspect (pending)", "已标记\u300C{0}\u300D为嫌疑人（待提交）" },
        ["toast.unmarked"] = new[] { "Unmarked \u201C{0}\u201D", "已取消标记\u300C{0}\u300D" },

        // 结算
        ["result.win"] = new[] { "All Uncovered!", "全部识破！" },
        ["result.lose"] = new[] { "Investigation Over", "调查结束" },
        ["result.detail"] = new[] { "Correct {0}/{1}   Wrong {2}\nPhotos taken: {3}\n\nThe real imposters were:\n{4}", "指认正确 {0}/{1}　误指 {2} 人\n共拍摄 {3} 张照片\n\n真正的伪人是：\n{4}" },
        ["result.none"] = new[] { "(none)", "（无）" },
        ["result.replay"] = new[] { "Play Again", "再玩一次" },
        ["result.menu"] = new[] { "Main Menu", "返回主菜单" },

        // 伪人类型名
        ["kind.normal"] = new[] { "Normal", "普通人" },
        ["kind.doubao"] = new[] { "AI Bot", "豆包人" },
        ["kind.sixfinger"] = new[] { "Six-Finger", "六指人" },
        ["kind.scarysmile"] = new[] { "Scary Smile", "一笑变可怕人" },
        ["kind.framedrop"] = new[] { "Frame-Drop", "掉帧人" },
        ["kind.stitched"] = new[] { "Stitched", "拼接人" },
        ["kind.photomissing"] = new[] { "Photo-Ghost", "照片消失人" },
        ["kind.deflate"] = new[] { "Deflate", "变瘪人" },
    };
}
