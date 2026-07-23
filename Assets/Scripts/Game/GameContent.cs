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

/// <summary>集中存放占位文案（姓名、对话）。</summary>
public static class GameContent
{
    public static readonly string[] Names =
    {
        "小李", "阿强", "老王", "翠花", "大壮",
        "阿珍", "胖虎", "丽丽", "二狗", "春妮"
    };

    static readonly string[] NormalTalk =
    {
        "今天天气不错，出来走走。",
        "你也在逛集市啊？人挺多的。",
        "有事吗？我还挺忙的。",
        "这条街我天天来，熟得很。"
    };

    static readonly string[] DouBaoTalk =
    {
        "您好！我是本地居民，很高兴能为您提供帮助。",
        "这个问题很有意思，我们可以从多个维度来分析它。",
        "总的来说，具体情况需要具体分析，希望以上回答对您有帮助。",
        "抱歉，作为一名普通市民，我暂时无法提供更多相关信息。"
    };

    static readonly string[] SixFingerTalk =
    {
        "你好呀，握手就……还是算了吧。",
        "我这人怕生，手就不伸出来了哈。",
        "拍照可以，比耶什么的就免了吧？"
    };

    static readonly string[] ScarySmileTalk =
    {
        "嗨，你好呀。",
        "我笑起来可好看了，要不要给我拍张照？",
        "别光顾着聊天，来，笑一个拍张照嘛。"
    };

    static readonly string[] FrameDropTalk =
    {
        "呃……你、你好。",
        "我……我刚才是不是卡了一下？",
        "别、别老盯着我看，怪不自在的。"
    };

    static readonly string[] StitchedTalk =
    {
        "你好，今天天气真不错呢。",
        "我这身衣服？东拼西凑随便穿的啦。",
        "别离太近……离远点拍照更好看。"
    };

    static readonly string[] PhotoMissingTalk =
    {
        "你好。",
        "我可上镜了，快给我拍张照。",
        "拍照的时候一定要把我拍进去哦。"
    };

    static readonly string[] DeflateTalk =
    {
        "你好呀，别、别撞过来。",
        "我皮肤比较敏感，最好别碰我。",
        "保持距离哈，谢谢配合。"
    };

    /// <summary>返回该 NPC 的一组对话（逐句显示）。</summary>
    public static string[] GetDialogue(NpcKind kind)
    {
        switch (kind)
        {
            case NpcKind.DouBao: return DouBaoTalk;
            case NpcKind.SixFinger: return SixFingerTalk;
            case NpcKind.ScarySmile: return ScarySmileTalk;
            case NpcKind.FrameDrop: return FrameDropTalk;
            case NpcKind.Stitched: return StitchedTalk;
            case NpcKind.PhotoMissing: return PhotoMissingTalk;
            case NpcKind.Deflate: return DeflateTalk;
            default: return NormalTalk;
        }
    }

    /// <summary>用于结算/指认提示的中文名。</summary>
    public static string KindLabel(NpcKind kind)
    {
        switch (kind)
        {
            case NpcKind.DouBao: return "豆包人";
            case NpcKind.SixFinger: return "六指人";
            case NpcKind.ScarySmile: return "一笑变可怕人";
            case NpcKind.FrameDrop: return "掉帧人";
            case NpcKind.Stitched: return "拼接人";
            case NpcKind.PhotoMissing: return "照片消失人";
            case NpcKind.Deflate: return "变瘪人";
            default: return "普通人";
        }
    }
}
