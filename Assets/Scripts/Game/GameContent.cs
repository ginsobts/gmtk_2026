using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 炼化人种类。每种对应一条侦查线（露馅方式）：
/// DouBao      —— 对话线：说话一股 AI 官方腔
/// SixFinger   —— 取景线：命令「比耶」时露出六根手指
/// ScarySmile  —— 取景线：命令「笑」时变成恐怖脸
/// FrameDrop   —— 取景线：在镜头里不停抖动/瞬移（掉帧）
/// Stitched    —— 相册线：拍出的照片里身体是拼接的
/// PhotoMismatch—— 相册线：真人在取景框里，但照片里变成另一个样子（程书，照片与本人不一致）
/// Deflate     —— 世界线：玩家靠近接触时会“变瘪”
/// SkinDog     —— 对话线/时间轴：三阶段差分，最终扒皮恐怖立绘（魏大爷）
/// LookAway    —— 取景线：镜头移开时表情在三态间循环切换（顾映，N3）
/// </summary>
public enum NpcKind
{
    Normal,
    DouBao,
    SixFinger,
    ScarySmile,
    FrameDrop,
    Stitched,
    PhotoMismatch,
    Deflate,
    SkinDog,
    LookAway
}

public enum NpcDialogueMode { Static, Phase, Count }

/// <summary>运行时一句对话（已本地化 + 立绘 id）。</summary>
public class DialogueLine
{
    public string portraitId;
    public string text;
}

/// <summary>表内一句台词原始数据。</summary>
class DialogueLineDef
{
    public string portraitId;
    public string en;
    public string zh;
}

/// <summary>一个可配置角色（来自 characters.txt）。名字支持中英双语。</summary>
public class CharacterDef
{
    public string charId;
    public string nameEn;
    public string nameZh;
    public string artFolder;
    public string dialogueId;
    public NpcKind kind = NpcKind.Normal;
    public bool harmless;   // 无害正常人：错认也不算失败（策划 N2，如王建国）
    public string titleEn;
    public string titleZh;

    public string DisplayName => Loc.Pick(nameEn, nameZh);

    /// <summary>UI 显示名：有岗位时，岗位以更小字号 + 灰色显示在名字右侧（富文本，各 UI Text 均已开启 rich text）。</summary>
    public string DisplayLabel
    {
        get
        {
            string title = Loc.Pick(titleEn, titleZh);
            if (string.IsNullOrEmpty(title)) return DisplayName;
            return $"{DisplayName}   <size=22><color=#9AA0AA>{title}</color></size>";
        }
    }
}

/// <summary>单局占位（来自 rounds.txt，仅 roundId）。</summary>
public class RoundDef
{
    public string roundId = "r1";
}

/// <summary>固定出生点（来自 spawns.txt）：某角色在场景里的固定坐标与朝向。</summary>
public class SpawnDef
{
    public float x;
    public float z;
    public float yaw;
    public bool faceCamera = true;
}

/// <summary>时间阶段（来自 phases.txt）。</summary>
public class PhaseDef
{
    public string phaseId;
    public int order;
    public int threshold;
    public string nameEn;
    public string nameZh;

    public string DisplayName => Loc.Pick(nameEn, nameZh);
}

class NpcDialogueEntry
{
    public NpcDialogueMode mode;
    public int index;
    public string dialogueId;
}

/// <summary>
/// 内容层：从 Resources/GameData/*.txt（Tab 分隔）读取姓名、对话、关卡配置。
/// </summary>
public static class GameContent
{
    static bool _loaded;
    static List<CharacterDef> _characters;
    static Dictionary<string, List<DialogueLineDef>> _dialogue;
    static List<RoundDef> _rounds;
    static List<PhaseDef> _phases;
    static Dictionary<string, List<NpcDialogueEntry>> _npcDialogues;
    static Dictionary<string, string> _portraits;
    static Dictionary<string, SpawnDef> _spawns;

    public static IReadOnlyList<CharacterDef> Characters { get { EnsureLoaded(); return _characters; } }
    public static IReadOnlyList<PhaseDef> Phases { get { EnsureLoaded(); return _phases; } }

    /// <summary>某角色的固定出生点（spawns.txt）；没配则返回 null，由调用方回退。</summary>
    public static SpawnDef GetSpawn(string charId)
    {
        EnsureLoaded();
        if (_spawns != null && !string.IsNullOrEmpty(charId) && _spawns.TryGetValue(charId, out var s)) return s;
        return null;
    }

    public static RoundDef GetDefaultRound()
    {
        EnsureLoaded();
        return (_rounds != null && _rounds.Count > 0) ? _rounds[0] : new RoundDef();
    }

    /// <summary>按时间轴数值推导当前阶段 order（1 起）。</summary>
    public static int GetPhaseForTimeline(int timelineValue)
    {
        EnsureLoaded();
        int phase = 1;
        if (_phases == null) return phase;
        foreach (var p in _phases)
        {
            if (timelineValue >= p.threshold && p.order >= phase)
                phase = p.order;
        }
        return phase;
    }

    public static PhaseDef GetPhaseDef(int phaseOrder)
    {
        EnsureLoaded();
        if (_phases == null) return null;
        foreach (var p in _phases)
            if (p.order == phaseOrder) return p;
        return _phases.Count > 0 ? _phases[0] : null;
    }

    /// <summary>时间轴进度条满格值：末阶段 threshold + 与上一段等长的余量。</summary>
    public static int GetTimelineMax()
    {
        EnsureLoaded();
        if (_phases == null || _phases.Count == 0) return 90;
        var sorted = new List<PhaseDef>(_phases);
        sorted.Sort((a, b) => a.order.CompareTo(b.order));
        var last = sorted[sorted.Count - 1];
        if (sorted.Count >= 2)
        {
            var prev = sorted[sorted.Count - 2];
            return last.threshold + System.Math.Max(1, last.threshold - prev.threshold);
        }
        return last.threshold + 15;
    }

    /// <summary>查 portraits.txt，返回相对 Resources/Art/ 的路径；缺失返回 null。</summary>
    public static string GetPortraitPath(string portraitId)
    {
        if (string.IsNullOrEmpty(portraitId)) return null;
        EnsureLoaded();
        if (_portraits != null && _portraits.TryGetValue(portraitId, out var path))
            return path;
        return null;
    }

    public static NpcDialogueMode GetDialogueMode(string charId)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(charId) || _npcDialogues == null) return NpcDialogueMode.Static;
        if (!_npcDialogues.TryGetValue(charId, out var entries) || entries == null || entries.Count == 0)
            return NpcDialogueMode.Static;
        return entries[0].mode;
    }

    /// <summary>解析该 NPC 当前应播放的一组对话。</summary>
    public static DialogueLine[] ResolveDialogue(Npc npc, int currentPhase)
    {
        EnsureLoaded();
        if (npc == null) return System.Array.Empty<DialogueLine>();

        string dialogueId = ResolveDialogueId(npc, currentPhase);
        if (TryGetLines(dialogueId, out var lines))
            return PickLines(lines, npc);
        return PickLines(FallbackGenericDialogue(), npc);
    }

    static string ResolveDialogueId(Npc npc, int currentPhase)
    {
        if (!string.IsNullOrEmpty(npc.charId) &&
            _npcDialogues != null &&
            _npcDialogues.TryGetValue(npc.charId, out var entries) &&
            entries != null && entries.Count > 0)
        {
            var mode = entries[0].mode;
            if (mode == NpcDialogueMode.Static)
            {
                foreach (var e in entries)
                    if (e.index == 0) return e.dialogueId;
                return entries[0].dialogueId;
            }
            if (mode == NpcDialogueMode.Phase)
            {
                foreach (var e in entries)
                    if (e.index == currentPhase) return e.dialogueId;
            }
            else if (mode == NpcDialogueMode.Count)
            {
                int target = npc.dialogueVisitCount + 1;
                string last = null;
                foreach (var e in entries)
                {
                    if (e.index <= target) last = e.dialogueId;
                    if (e.index == target) return e.dialogueId;
                }
                if (!string.IsNullOrEmpty(last)) return last;
            }
        }

        return string.IsNullOrEmpty(npc.dialogueId) ? "generic" : npc.dialogueId;
    }

    static bool TryGetLines(string dialogueId, out List<DialogueLineDef> lines)
    {
        lines = null;
        if (string.IsNullOrEmpty(dialogueId) || _dialogue == null) return false;
        return _dialogue.TryGetValue(dialogueId, out lines) && lines != null && lines.Count > 0;
    }

    static DialogueLine[] PickLines(List<DialogueLineDef> lines, Npc npc)
    {
        string fallbackCharId = npc != null ? npc.charId : null;
        string expr = npc != null ? npc.PortraitExpression() : null;   // 当前露馅表情态（顾映 look-away 三表情），null=neutral
        var arr = new DialogueLine[lines.Count];
        for (int i = 0; i < lines.Count; i++)
        {
            var def = lines[i];
            // 空 / generic_neutral 的 portraitId → 回退到说话人自己的立绘；
            // 若说话人当前有露馅表情态则用变体（<charId>_<expr>），实现对话立绘与场景棋子同步。
            string pid = def.portraitId;
            if ((string.IsNullOrEmpty(pid) || pid == "generic_neutral") && !string.IsNullOrEmpty(fallbackCharId))
                pid = !string.IsNullOrEmpty(expr) ? fallbackCharId + "_" + expr : fallbackCharId + "_neutral";
            arr[i] = new DialogueLine
            {
                portraitId = pid,
                text = Loc.Pick(def.en, def.zh)
            };
        }
        return arr;
    }

    public static string KindLabel(NpcKind kind)
    {
        switch (kind)
        {
            case NpcKind.DouBao: return Loc.Get("kind.doubao");
            case NpcKind.SixFinger: return Loc.Get("kind.sixfinger");
            case NpcKind.ScarySmile: return Loc.Get("kind.scarysmile");
            case NpcKind.FrameDrop: return Loc.Get("kind.framedrop");
            case NpcKind.Stitched: return Loc.Get("kind.stitched");
            case NpcKind.PhotoMismatch: return Loc.Get("kind.photomismatch");
            case NpcKind.Deflate: return Loc.Get("kind.deflate");
            case NpcKind.SkinDog: return Loc.Get("kind.skindog");
            case NpcKind.LookAway: return Loc.Get("kind.lookaway");
            default: return Loc.Get("kind.normal");
        }
    }

    public static NpcKind ParseKind(string s)
    {
        if (!string.IsNullOrEmpty(s) &&
            System.Enum.TryParse(s.Trim(), true, out NpcKind k))
            return k;
        return NpcKind.Normal;
    }

    static NpcDialogueMode ParseDialogueMode(string s)
    {
        if (string.IsNullOrEmpty(s)) return NpcDialogueMode.Static;
        switch (s.Trim().ToLowerInvariant())
        {
            case "phase": return NpcDialogueMode.Phase;
            case "count": return NpcDialogueMode.Count;
            default: return NpcDialogueMode.Static;
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
            LoadPhases();
            LoadNpcDialogues();
            LoadPortraits();
            LoadSpawns();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[GameContent] 读表异常，改用内置默认：{e.Message}");
        }
        if (_characters == null || _characters.Count == 0) _characters = DefaultCharacters();
        if (_dialogue == null || _dialogue.Count == 0) _dialogue = DefaultDialogue();
        if (_rounds == null || _rounds.Count == 0) _rounds = new List<RoundDef> { new RoundDef() };
        if (_phases == null || _phases.Count == 0) _phases = DefaultPhases();
        if (_npcDialogues == null) _npcDialogues = new Dictionary<string, List<NpcDialogueEntry>>();
        if (_portraits == null) _portraits = new Dictionary<string, string>();
        if (_spawns == null) _spawns = new Dictionary<string, SpawnDef>();
    }

    static void LoadCharacters()
    {
        var rows = ReadTable("GameData/characters");
        if (rows == null) return;
        _characters = new List<CharacterDef>();
        foreach (var r in rows)
        {
            if (r.Length < 2 || string.IsNullOrEmpty(r[0])) continue;
            _characters.Add(new CharacterDef
            {
                charId = r[0].Trim(),
                artFolder = r[1].Trim(),
                dialogueId = r.Length > 2 ? r[2].Trim() : "",
                nameEn = r.Length > 3 ? r[3].Trim() : "",
                nameZh = r.Length > 4 ? r[4].Trim() : "",
                kind = r.Length > 5 ? ParseKind(r[5]) : NpcKind.Normal,
                titleEn = r.Length > 6 ? r[6].Trim() : "",
                titleZh = r.Length > 7 ? r[7].Trim() : "",
                harmless = r.Length > 8 && r[8].Trim() == "1"
            });
        }
    }

    static void LoadDialogue()
    {
        var rows = ReadTable("GameData/dialogue");
        if (rows == null) return;
        var tmp = new Dictionary<string, SortedList<int, DialogueLineDef>>();
        foreach (var r in rows)
        {
            if (r.Length < 3 || string.IsNullOrEmpty(r[0])) continue;
            string id = r[0].Trim();
            int order = ParseInt(r[1], 0);

            string portraitId = "";
            string en, zh;
            if (r.Length >= 5)
            {
                portraitId = r[2].Trim();
                en = Unescape(r[3]);
                zh = Unescape(r[4]);
            }
            else
            {
                en = Unescape(r[2]);
                zh = Unescape(r.Length > 3 ? r[3] : "");
            }

            if (!tmp.TryGetValue(id, out var sl)) { sl = new SortedList<int, DialogueLineDef>(); tmp[id] = sl; }
            while (sl.ContainsKey(order)) order++;
            sl[order] = new DialogueLineDef { portraitId = portraitId, en = en, zh = zh };
        }
        _dialogue = new Dictionary<string, List<DialogueLineDef>>();
        foreach (var kv in tmp)
        {
            var list = new List<DialogueLineDef>(kv.Value.Values);
            _dialogue[kv.Key] = list;
        }
    }

    static void LoadRounds()
    {
        var rows = ReadTable("GameData/rounds");
        if (rows == null) return;
        _rounds = new List<RoundDef>();
        foreach (var r in rows)
        {
            if (string.IsNullOrEmpty(r[0])) continue;
            _rounds.Add(new RoundDef { roundId = r[0].Trim() });
        }
    }

    static void LoadPhases()
    {
        var rows = ReadTable("GameData/phases");
        if (rows == null) return;
        _phases = new List<PhaseDef>();
        foreach (var r in rows)
        {
            if (r.Length < 4 || string.IsNullOrEmpty(r[0])) continue;
            _phases.Add(new PhaseDef
            {
                phaseId = r[0].Trim(),
                order = ParseInt(r[1], 1),
                threshold = ParseInt(r[2], 0),
                nameEn = r.Length > 3 ? r[3].Trim() : "",
                nameZh = r.Length > 4 ? r[4].Trim() : ""
            });
        }
        _phases.Sort((a, b) => a.order.CompareTo(b.order));
    }

    static void LoadNpcDialogues()
    {
        var rows = ReadTable("GameData/npc_dialogues");
        if (rows == null) return;
        _npcDialogues = new Dictionary<string, List<NpcDialogueEntry>>();
        foreach (var r in rows)
        {
            if (r.Length < 4 || string.IsNullOrEmpty(r[0])) continue;
            string charId = r[0].Trim();
            var entry = new NpcDialogueEntry
            {
                mode = ParseDialogueMode(r[1]),
                index = ParseInt(r[2], 0),
                dialogueId = r[3].Trim()
            };
            if (!_npcDialogues.TryGetValue(charId, out var list))
            {
                list = new List<NpcDialogueEntry>();
                _npcDialogues[charId] = list;
            }
            list.Add(entry);
        }
        foreach (var kv in _npcDialogues)
            kv.Value.Sort((a, b) => a.index.CompareTo(b.index));
    }

    static void LoadPortraits()
    {
        var rows = ReadTable("GameData/portraits");
        if (rows == null) return;
        _portraits = new Dictionary<string, string>();
        foreach (var r in rows)
        {
            if (r.Length < 2 || string.IsNullOrEmpty(r[0])) continue;
            _portraits[r[0].Trim()] = r[1].Trim();
        }
    }

    static void LoadSpawns()
    {
        var rows = ReadTable("GameData/spawns");
        if (rows == null) return;
        _spawns = new Dictionary<string, SpawnDef>();
        foreach (var r in rows)
        {
            if (r.Length < 3 || string.IsNullOrEmpty(r[0])) continue;
            _spawns[r[0].Trim()] = new SpawnDef
            {
                x = ParseFloat(r[1], 0f),
                z = ParseFloat(r[2], 0f),
                yaw = r.Length > 3 ? ParseFloat(r[3], 0f) : 0f,
                faceCamera = r.Length <= 4 || r[4].Trim() != "0"
            };
        }
    }

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

    static float ParseFloat(string s, float fallback)
        => float.TryParse(s?.Trim(), System.Globalization.NumberStyles.Float,
                          System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : fallback;

    static string Unescape(string s)
        => string.IsNullOrEmpty(s) ? s : s.Replace("\\n", "\n").Trim();

    // ---------------- 内置默认 ----------------

    static List<CharacterDef> DefaultCharacters()
    {
        var list = new List<CharacterDef>
        {
            C("lin_cai", "Lin Cai", "林采", NpcKind.DouBao, "PR", "公关"),
            C("wang_jianguo", "Wang Jianguo", "王建国", NpcKind.Normal, "CEO", "老板", harmless: true),
            C("chen_wei", "Chen Wei", "陈维", NpcKind.FrameDrop, "Ops", "运维"),
            C("su_qing", "Su Qing", "苏晴", NpcKind.Normal, "HR", "人事"),
            C("shrink_girl", "Schoolgirl", "女学生", NpcKind.Deflate),
            C("fang_xiao", "Fang Xiao", "方晓", NpcKind.Normal, "Staff", "职员"),
            C("wu_ang", "Wu Ang", "吴昂", NpcKind.Stitched, "Algorithm", "算法"),
            C("lu_yuan", "Lu Yuan", "陆远", NpcKind.Normal, "Dev", "开发"),
            C("wei_daye", "Uncle Wei", "魏大爷", NpcKind.SkinDog),
            C("an_an", "An'an", "安安", NpcKind.Normal),
            C("han_lu", "Han Lu", "韩露", NpcKind.SixFinger, "Reception", "前台"),
            C("gu_ying", "Gu Ying", "顾映", NpcKind.LookAway, "Brand", "品牌"),
            C("cheng_shu", "Cheng Shu", "程书", NpcKind.PhotoMismatch, "Compliance", "合规"),
        };
        return list;
    }

    static CharacterDef C(string id, string en, string zh, NpcKind kind, string titleEn = "", string titleZh = "", bool harmless = false) => new CharacterDef
    {
        charId = id,
        nameEn = en,
        nameZh = zh,
        artFolder = $"Characters/{id}",
        dialogueId = "generic",
        kind = kind,
        titleEn = titleEn,
        titleZh = titleZh,
        harmless = harmless
    };

    static List<PhaseDef> DefaultPhases() => new List<PhaseDef>
    {
        new PhaseDef { phaseId = "p1", order = 1, threshold = 0, nameEn = "Morning", nameZh = "上午" },
        new PhaseDef { phaseId = "p2", order = 2, threshold = 30, nameEn = "Afternoon", nameZh = "下午" },
        new PhaseDef { phaseId = "p3", order = 3, threshold = 60, nameEn = "Evening", nameZh = "晚上" }
    };

    static List<DialogueLineDef> L(params DialogueLineDef[] lines) => new List<DialogueLineDef>(lines);

    static DialogueLineDef D(string en, string zh, string portraitId = "") =>
        new DialogueLineDef { en = en, zh = zh, portraitId = portraitId };

    static Dictionary<string, List<DialogueLineDef>> DefaultDialogue() => new Dictionary<string, List<DialogueLineDef>>
    {
        ["generic"] = L(
            D("Nice weather today, just out for a stroll.", "排期表上写着六点收工，希望这次是真的。"),
            D("You're at the market too? Quite a crowd.", "他们老把「效率改革」挂在嘴边，也没人说清楚到底改什么。"),
            D("Need something? I'm a bit busy.", "好歹茶水间有免费零食，咖啡机记得去试试。"),
            D("I come to this street every day, know it well.", "你要拍素材的话，最好别进机房，容易惹麻烦。"))
    };

    static List<DialogueLineDef> FallbackGenericDialogue()
    {
        return DefaultDialogue()["generic"];
    }
}
