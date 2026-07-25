using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 对话 UI 专用立绘加载（Art/Portraits/，与场景 Characters 资源分离）。
/// </summary>
public static class PortraitArt
{
    static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

    public static Sprite GetPortrait(string portraitId)
    {
        if (string.IsNullOrEmpty(portraitId)) return null;
        if (_cache.TryGetValue(portraitId, out var cached)) return cached;

        string path = GameContent.GetPortraitPath(portraitId);
        if (string.IsNullOrEmpty(path)) return null;

        var sprite = Resources.Load<Sprite>("Art/" + path);
        _cache[portraitId] = sprite;
        return sprite;
    }
}
