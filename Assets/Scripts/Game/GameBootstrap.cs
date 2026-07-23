using UnityEngine;

/// <summary>
/// 自动启动：进入 Play 模式、场景加载完成后，自动创建 GameManager 并搭好整个游戏。
/// 好处：你不需要在编辑器里手动配置场景，编译完直接按播放键就能玩。
/// </summary>
public static class GameBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Launch()
    {
        if (GameManager.Instance != null) return;
        var go = new GameObject("GameManager");
        go.AddComponent<GameManager>();
    }
}
