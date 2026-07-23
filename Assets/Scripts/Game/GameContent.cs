using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 伪人种类。每种对应一条侦查线（露馅方式）：
/// DouBao      —— 对话线：说话一股 AI 官方腔
/// SixFinger   —— 取景线：命令「比耶」时露出六根手指
/// ScarySmile  —— 取景线：命令「笑」时变成恐怖脸
/// FrameDrop   —— 取景线：在镜头里不停抖动/瞬移（掉帧）
/// Stitched    —— 相册线：拍出的照片里身体是拼接的
/// PhotoMissing—— 相册线：真人在取景框里，但照片里没有 TA
/// Deflate     —— 世界线：玩家靠近接触时会“变瘪”
/// </summary>
public enum NpcKind
{
    Normal,
    DouBao,
    SixFinger,
    ScarySmile,
    FrameDrop,
    Stitched,
    PhotoMissing,
    Deflate
}

/// <summary>一个可配置角色（来自 characters.txt）。名字支持中英双语。</summary>
public class CharacterDef
{
    public string charId;
    public string nameEn;
    public string nameZh;
    public string artFolder;    // 相对 Resources/Art，例如 Characters/npc_00
    public string dialogueId;   // 普通状态用的对话 id，可空

    /// <summary>按当前语言返回显示名。</summary>
    public string DisplayName => Loc.Pick(nameEn, nameZh);
}

/// <summary>一关的配置（来自 rounds.txt）。</summary>
public class RoundDef
{
    public string roundId = "r1";
    public int npcCount = 8;
    public int imposterCount = 3;
    public int film = 10;
    public Dictionary<string, NpcKind> assign = new Dictionary<string, NpcKind>();
}

/// <summary>
/// 内容层：从 Resources/GameData/*.txt（Tab 分隔）读取姓名、对话、关卡配置。
/// 读表失败或字段缺失时回退到内置默认值，保证游戏始终可运行。
/// 策划改表即可，无需改代码。
/// </summary>
public static class GameContent
{
    static bool _loaded;
    static List<CharacterDef> _characters;
    static Dictionary<string, List<string[]>> _dialogue;   // dialogueId -> 台词序列，每句 [en, zh]
    static List<RoundDef> _rounds;

    public static IReadOnlyList<CharacterDef> Characters { get { EnsureLoaded(); return _characters; } }

    public static RoundDef GetDefaultRound()
    {
        EnsureLoaded();
        return (_rounds != null && _rounds.Count > 0) ? _rounds[0] : new RoundDef();
    }

    /// <summary>返回该 NPC 的一组对话（逐句显示，按当前语言）。charDialogueId 用于普通角色的专属闲聊。</summary>
    public static string[] GetDialogue(NpcKind kind, string charDialogueId = null)
    {
        EnsureLoaded();
        string id = kind == NpcKind.Normal
            ? (string.IsNullOrEmpty(charDialogueId) ? "generic" : charDialogueId)
            : KindDialogueId(kind);

        if (_dialogue != null && _dialogue.TryGetValue(id, out var lines) && lines != null && lines.Count > 0)
            return PickLines(lines);
        return PickLines(FallbackDialogue(kind));
    }

    static string[] PickLines(List<string[]> lines)
    {
        var arr = new string[lines.Count];
        for (int i = 0; i < lines.Count; i++)
        {
            var pair = lines[i];
            arr[i] = Loc.Pick(pair.Length > 0 ? pair[0] : "", pair.Length > 1 ? pair[1] : "");
        }
        return arr;
    }

    /// <summary>用于结算/指认提示的伪人类型名（按当前语言）。</summary>
    public static string KindLabel(NpcKind kind)
    {
        switch (kind)
        {
            case NpcKind.DouBao: return Loc.Get("kind.doubao");
            case NpcKind.SixFinger: return Loc.Get("kind.sixfinger");
            case NpcKind.ScarySmile: return Loc.Get("kind.scarysmile");
            case NpcKind.FrameDrop: return Loc.Get("kind.framedrop");
            case NpcKind.Stitched: return Loc.Get("kind.stitched");
            case NpcKind.PhotoMissing: return Loc.Get("kind.photomissing");
            case NpcKind.Deflate: return Loc.Get("kind.deflate");
            default: return Loc.Get("kind.normal");
        }
    }

    /// <summary>把表里的字符串解析成 NpcKind，无法识别返回 Normal。</summary>
    public static NpcKind ParseKind(string s)
    {
        if (!string.IsNullOrEmpty(s) &&
            System.Enum.TryParse(s.Trim(), true, out NpcKind k))
            return k;
        return NpcKind.Normal;
    }

    static string KindDialogueId(NpcKind kind)
    {
        switch (kind)
        {
            case NpcKind.DouBao: return "doubao";
            case NpcKind.SixFinger: return "sixfinger";
            case NpcKind.ScarySmile: return "scarysmile";
            case NpcKind.FrameDrop: return "framedrop";
            case NpcKind.Stitched: return "stitched";
            case NpcKind.PhotoMissing: return "photomissing";
            case NpcKind.Deflate: return "deflate";
            default: return "generic";
        }
    }

    // ---------------- 读表 ----------------

    static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        try
        {
            LoadCharacters();
            LoadDialogue();
            LoadRounds();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[GameContent] 读表异常，改用内置默认：{e.Message}");
        }
        if (_characters == null || _characters.Count == 0) _characters = DefaultCharacters();
        if (_dialogue == null || _dialogue.Count == 0) _dialogue = DefaultDialogue();
        if (_rounds == null || _rounds.Count == 0) _rounds = new List<RoundDef> { new RoundDef() };
    }

    static void LoadCharacters()
    {
        var rows = ReadTable("GameData/characters");
        if (rows == null) return;
        // 列：charId  artFolder  dialogueId  name_en  name_zh
        _characters = new List<CharacterDef>();
        foreach (var r in rows)
        {
            if (r.Length < 2 || string.IsNullOrEmpty(r[0])) continue;
            string en = r.Length > 3 ? r[3].Trim() : "";
            string zh = r.Length > 4 ? r[4].Trim() : "";
            _characters.Add(new CharacterDef
            {
                charId = r[0].Trim(),
                artFolder = r[1].Trim(),
                dialogueId = r.Length > 2 ? r[2].Trim() : "",
                nameEn = en,
                nameZh = zh
            });
        }
    }

    static void LoadDialogue()
    {
        var rows = ReadTable("GameData/dialogue");
        if (rows == null) return;
        // 列：dialogueId  order  en  zh
        // dialogueId -> (order -> [en, zh])
        var tmp = new Dictionary<string, SortedList<int, string[]>>();
        foreach (var r in rows)
        {
            if (r.Length < 3 || string.IsNullOrEmpty(r[0])) continue;
            string id = r[0].Trim();
            int order = ParseInt(r[1], 0);
            string en = Unescape(r.Length > 2 ? r[2] : "");
            string zh = Unescape(r.Length > 3 ? r[3] : "");
            if (!tmp.TryGetValue(id, out var sl)) { sl = new SortedList<int, string[]>(); tmp[id] = sl; }
            while (sl.ContainsKey(order)) order++; // 容错：order 重复时顺延
            sl[order] = new[] { en, zh };
        }
        _dialogue = new Dictionary<string, List<string[]>>();
        foreach (var kv in tmp)
            _dialogue[kv.Key] = new List<string[]>(kv.Value.Values);
    }

    static void LoadRounds()
    {
        var rows = ReadTable("GameData/rounds");
        if (rows == null) return;
        _rounds = new List<RoundDef>();
        foreach (var r in rows)
        {
            if (r.Length < 4 || string.IsNullOrEmpty(r[0])) continue;
            var rd = new RoundDef
            {
                roundId = r[0].Trim(),
                npcCount = ParseInt(r[1], 8),
                imposterCount = ParseInt(r[2], 3),
                film = ParseInt(r[3], 10)
            };
            string assign = r.Length > 4 ? r[4].Trim() : "";
            if (!string.IsNullOrEmpty(assign))
            {
                foreach (var pair in assign.Split(','))
                {
                    var kv = pair.Split('=');
                    if (kv.Length == 2)
                        rd.assign[kv[0].Trim()] = ParseKind(kv[1]);
                }
            }
            _rounds.Add(rd);
        }
    }

    /// <summary>读取 Tab 分隔表：跳过空行、以 # 开头的注释行与表头，返回数据行的列数组。</summary>
    static List<string[]> ReadTable(string resourcePath)
    {
        var asset = Resources.Load<TextAsset>(resourcePath);
        if (asset == null)
        {
            Debug.LogWarning($"[GameContent] 找不到表 {resourcePath}，改用内置默认。");
            return null;
        }
        var result = new List<string[]>();
        bool headerSkipped = false;
        foreach (var raw in asset.text.Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#")) continue;
            if (!headerSkipped) { headerSkipped = true; continue; }
            result.Add(line.Split('\t'));
        }
        return result;
    }

    static int ParseInt(string s, int fallback)
        => int.TryParse(s?.Trim(), out int v) ? v : fallback;

    static string Unescape(string s)
        => string.IsNullOrEmpty(s) ? s : s.Replace("\\n", "\n").Trim();

    // ---------------- 内置默认（读表失败时的回退） ----------------

    static List<CharacterDef> DefaultCharacters()
    {
        string[] en = { "Leo", "Chad", "Walt", "Rose", "Bruno", "Jenny", "Tiger", "Lily" };
        string[] zh = { "小李", "阿强", "老王", "翠花", "大壮", "阿珍", "胖虎", "丽丽" };
        var list = new List<CharacterDef>();
        for (int i = 0; i < en.Length; i++)
        {
            string id = $"npc_{i:00}";
            list.Add(new CharacterDef
            {
                charId = id,
                nameEn = en[i],
                nameZh = zh[i],
                artFolder = $"Characters/{id}",
                dialogueId = "generic"
            });
        }
        return list;
    }

    static List<string[]> L(params string[][] pairs) => new List<string[]>(pairs);

    static Dictionary<string, List<string[]>> DefaultDialogue() => new Dictionary<string, List<string[]>>
    {
        ["generic"] = L(
            new[] { "Nice weather today, just out for a stroll.", "今天天气不错，出来走走。" },
            new[] { "You're at the market too? Quite a crowd.", "你也在逛集市啊？人挺多的。" },
            new[] { "Need something? I'm a bit busy.", "有事吗？我还挺忙的。" },
            new[] { "I come to this street every day, know it well.", "这条街我天天来，熟得很。" }),
        ["doubao"] = L(
            new[] { "Hello! I am a local resident and I'm happy to help you.", "您好！我是本地居民，很高兴能为您提供帮助。" },
            new[] { "That's an interesting question; we can analyze it from multiple dimensions.", "这个问题很有意思，我们可以从多个维度来分析它。" },
            new[] { "In summary, specifics require specific analysis. I hope this helps.", "总的来说，具体情况需要具体分析，希望以上回答对您有帮助。" },
            new[] { "Sorry, as an ordinary citizen, I cannot provide more information.", "抱歉，作为一名普通市民，我暂时无法提供更多相关信息。" }),
        ["sixfinger"] = L(
            new[] { "Hi there. A handshake? ...maybe not.", "你好呀，握手就……还是算了吧。" },
            new[] { "I'm shy, I'll keep my hands to myself, ha.", "我这人怕生，手就不伸出来了哈。" },
            new[] { "Photos are fine, but let's skip the peace sign, okay?", "拍照可以，比耶什么的就免了吧？" }),
        ["scarysmile"] = L(
            new[] { "Hey, hello there.", "嗨，你好呀。" },
            new[] { "I look great smiling — want to take a photo of me?", "我笑起来可好看了，要不要给我拍张照？" },
            new[] { "Don't just chat, come on, smile for a photo.", "别光顾着聊天，来，笑一个拍张照嘛。" }),
        ["framedrop"] = L(
            new[] { "Uh... h-hello.", "呃……你、你好。" },
            new[] { "Did I... did I just glitch for a second?", "我……我刚才是不是卡了一下？" },
            new[] { "D-don't keep staring at me, it's unsettling.", "别、别老盯着我看，怪不自在的。" }),
        ["stitched"] = L(
            new[] { "Hello, lovely weather today, isn't it?", "你好，今天天气真不错呢。" },
            new[] { "These clothes? Just a random mix of bits and pieces.", "我这身衣服？东拼西凑随便穿的啦。" },
            new[] { "Don't get too close... photos look better from afar.", "别离太近……离远点拍照更好看。" }),
        ["photomissing"] = L(
            new[] { "Hello.", "你好。" },
            new[] { "I'm very photogenic, take a photo of me quick.", "我可上镜了，快给我拍张照。" },
            new[] { "Be sure to get me in the shot when you take a photo.", "拍照的时候一定要把我拍进去哦。" }),
        ["deflate"] = L(
            new[] { "Hi there, d-don't bump into me.", "你好呀，别、别撞过来。" },
            new[] { "My skin is pretty sensitive, better not touch me.", "我皮肤比较敏感，最好别碰我。" },
            new[] { "Keep your distance please, thanks for understanding.", "保持距离哈，谢谢配合。" })
    };

    static List<string[]> FallbackDialogue(NpcKind kind)
    {
        var d = DefaultDialogue();
        return d.TryGetValue(KindDialogueId(kind), out var lines) ? lines : d["generic"];
    }
}
