using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 加载 Assets/Resources/Art 下的生成美术，并把固定布局的图集切成可直接使用的 Sprite。
/// 坐标使用从图片左上角开始的像素区域，方便对照图集调整。
/// </summary>
public static class GeneratedArt
{
    const float AtlasSize = 1024f;

    static Texture2D _uiIcons;
    static Texture2D _photoFrames;
    static Texture2D _ground;
    static Texture2D _townProps;
    static Texture2D _forestAndClouds;
    static Sprite _denseForestEdgeSprite;
    static Sprite _digitalCameraOverlaySprite;
    static Sprite _cameraShutterHandSprite;

    static readonly Dictionary<string, Sprite> _characterCache = new Dictionary<string, Sprite>();
    static readonly Dictionary<string, Sprite> _posesCache = new Dictionary<string, Sprite>();
    static Sprite[] _iconSprites;
    static Sprite[] _propSprites;
    static Sprite[] _forestSprites;
    static Sprite _playerSprite;
    static Sprite _sixFingerReveal, _scarySmileReveal, _stitchedReveal, _deflateReveal;

    // 程序化生成的表现用贴图（阴影/软点/箭头/暗角）
    static Sprite _blobShadow, _softDot, _downArrow, _vignette, _recDot;

    public static Texture2D GroundTexture =>
        _ground ??= Resources.Load<Texture2D>("Art/town_ground_texture");

    /// <summary>按角色美术文件夹加载默认立绘（artFolder 例如 Characters/npc_00）。</summary>
    public static Sprite GetCharacterSprite(string artFolder)
    {
        if (string.IsNullOrEmpty(artFolder)) return null;
        if (_characterCache.TryGetValue(artFolder, out var s)) return s;
        s = LoadWholeSprite($"Art/{artFolder}/base");
        _characterCache[artFolder] = s;
        return s;
    }

    /// <summary>该角色的姿势差分（比耶 / 笑）。没有对应美术时返回 null，调用方回退到普通立绘。</summary>
    public static Sprite GetCharacterPoseSprite(string artFolder, bool smile)
    {
        if (string.IsNullOrEmpty(artFolder)) return null;
        string suffix = smile ? "smile" : "yeah";
        string key = artFolder + "/" + suffix;
        if (_posesCache.TryGetValue(key, out var s)) return s;
        s = TryLoadWholeSprite($"Art/{artFolder}/{suffix}");
        _posesCache[key] = s;
        return s;
    }

    // ---- 伪人露馅立绘（通用，切换时由 Npc 自动匹配身高）----
    public static Sprite SixFingerRevealSprite =>
        _sixFingerReveal ??= LoadWholeSprite("Art/Imposters/six_finger_reveal");

    public static Sprite ScarySmileRevealSprite =>
        _scarySmileReveal ??= (TryLoadWholeSprite("Art/Imposters/scary_smile_reveal")
                               ?? LoadWholeSprite("Art/Imposters/pose_reveal_imposter"));

    public static Sprite StitchedRevealSprite =>
        _stitchedReveal ??= LoadWholeSprite("Art/Imposters/stitched_reveal");

    public static Sprite DeflateRevealSprite =>
        _deflateReveal ??= LoadWholeSprite("Art/Imposters/deflate_reveal");

    public static Sprite PlayerSprite =>
        _playerSprite ??= LoadWholeSprite("Art/Characters/player");

    /// <summary>用于填满地图边缘的宽幅树林卡片。</summary>
    public static Sprite DenseForestEdgeSprite =>
        _denseForestEdgeSprite ??= LoadWholeSprite("Art/dense_forest_edge_cluster");

    /// <summary>相机模式的数码傻瓜相机外壳（中央取景窗透明）。</summary>
    public static Sprite DigitalCameraOverlaySprite =>
        _digitalCameraOverlaySprite ??= LoadWholeSprite("Art/Camera/digital_camera_overlay");

    /// <summary>放在快门上的手，按快门时播放压下动画。</summary>
    public static Sprite CameraShutterHandSprite =>
        _cameraShutterHandSprite ??= LoadWholeSprite("Art/Camera/camera_shutter_hand");

    /// <summary>
    /// 0 相机、1 对话、2 放大镜、3 计时器、4 星星、5 照片、6 人物、7 返回。
    /// </summary>
    public static Sprite GetIconSprite(int index)
    {
        if (_iconSprites == null)
        {
            _uiIcons = Resources.Load<Texture2D>("Art/minimal_ui_icons");
            _iconSprites = new[]
            {
                CreateSprite(_uiIcons, 42, 261, 210, 190),
                CreateSprite(_uiIcons, 275, 265, 222, 183),
                CreateSprite(_uiIcons, 535, 250, 223, 212),
                CreateSprite(_uiIcons, 773, 245, 212, 222),
                CreateSprite(_uiIcons, 35, 553, 219, 224),
                CreateSprite(_uiIcons, 277, 558, 217, 215),
                CreateSprite(_uiIcons, 535, 546, 212, 223),
                CreateSprite(_uiIcons, 773, 560, 216, 194)
            };
        }

        return _iconSprites[Mathf.Abs(index) % _iconSprites.Length];
    }

    public static Sprite GetPhotoFrameSprite()
    {
        _photoFrames ??= Resources.Load<Texture2D>("Art/photo_evidence_frames");
        return CreateSprite(_photoFrames, 21, 71, 185, 226);
    }

    /// <summary>0 小商店、1 树、2 路灯、3 长椅。</summary>
    public static Sprite GetTownPropSprite(int index)
    {
        if (_propSprites == null)
        {
            _townProps = Resources.Load<Texture2D>("Art/town_prop_cards");
            _propSprites = new[]
            {
                CreateSprite(_townProps, 48, 35, 390, 465),
                CreateSprite(_townProps, 460, 42, 330, 430),
                CreateSprite(_townProps, 140, 500, 160, 390),
                CreateSprite(_townProps, 385, 525, 430, 300)
            };
        }

        return _propSprites[Mathf.Abs(index) % _propSprites.Length];
    }

    /// <summary>
    /// 0 密集树林、1 灌木、2 岩石、3 松树、4 小云、5 大云。
    /// </summary>
    public static Sprite GetForestSprite(int index)
    {
        if (_forestSprites == null)
        {
            _forestAndClouds = Resources.Load<Texture2D>("Art/forest_edge_and_cloud_cards");
            _forestSprites = new[]
            {
                CreateSprite(_forestAndClouds, 22, 55, 280, 350),
                CreateSprite(_forestAndClouds, 295, 230, 290, 185),
                CreateSprite(_forestAndClouds, 585, 240, 235, 170),
                CreateSprite(_forestAndClouds, 55, 515, 170, 410),
                CreateSprite(_forestAndClouds, 275, 625, 235, 175),
                CreateSprite(_forestAndClouds, 510, 605, 345, 190)
            };
        }

        return _forestSprites[Mathf.Abs(index) % _forestSprites.Length];
    }

    // ---------------- 程序化生成的表现贴图 ----------------

    /// <summary>脚下软阴影（黑色径向衰减）。</summary>
    public static Sprite BlobShadowSprite => _blobShadow ??= MakeRadialSprite(64, new Color(0f, 0f, 0f, 0.45f), 2.2f);

    /// <summary>白色软圆点（粒子 / 标记徽章 复用，按需染色）。</summary>
    public static Sprite SoftDotSprite => _softDot ??= MakeRadialSprite(64, Color.white, 1.6f);

    /// <summary>指向下方的实心三角（靠近可交互提示）。</summary>
    public static Sprite DownArrowSprite => _downArrow ??= MakeDownTriangleSprite(48);

    /// <summary>相机模式暗角（中间透明、四周变暗）。当前保留备用。</summary>
    public static Sprite VignetteSprite => _vignette ??= MakeVignetteSprite(256, 0.55f);

    /// <summary>REC 小红点（实心圆）。</summary>
    public static Sprite RecDotSprite => _recDot ??= MakeRadialSprite(32, new Color(1f, 0.25f, 0.25f, 1f), 6f);

    static Sprite MakeRadialSprite(int size, Color color, float falloff)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float r = size * 0.5f;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - r) / r;
                float dy = (y + 0.5f - r) / r;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                a = Mathf.Pow(a, falloff);
                px[y * size + x] = new Color(color.r, color.g, color.b, color.a * a);
            }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    static Sprite MakeDownTriangleSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // 顶宽底尖，指向下方（y 向上，顶部 y 大）
                float t = y / (float)(size - 1);              // 0 底 -> 1 顶
                float halfWidth = t * 0.5f;                    // 顶部最宽
                float cx = x / (float)(size - 1) - 0.5f;
                bool inside = Mathf.Abs(cx) <= halfWidth;
                px[y * size + x] = inside ? Color.white : new Color(1, 1, 1, 0);
            }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    static Sprite MakeVignetteSprite(int size, float strength)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float r = size * 0.5f;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - r) / r;
                float dy = (y + 0.5f - r) / r;
                float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                float a = Mathf.SmoothStep(0.55f, 1f, d) * strength;
                px[y * size + x] = new Color(0, 0, 0, a);
            }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    static Sprite CreateSprite(Texture2D texture, float x, float top, float width, float height)
    {
        if (texture == null)
        {
            Debug.LogError("找不到生成美术资源。请确认 Assets/Resources/Art 中的 PNG 已导入。");
            return null;
        }

        float scaleX = texture.width / AtlasSize;
        float scaleY = texture.height / AtlasSize;
        Rect rect = new Rect(
            x * scaleX,
            texture.height - (top + height) * scaleY,
            width * scaleX,
            height * scaleY);
        return Sprite.Create(texture, rect, new Vector2(0.5f, 0f), 100f);
    }

    static Sprite LoadWholeSprite(string resourcePath, float pixelsPerUnit = 100f)
    {
        var sprite = TryLoadWholeSprite(resourcePath, pixelsPerUnit);
        if (sprite == null) Debug.LogError($"找不到精灵资源：{resourcePath}");
        return sprite;
    }

    /// <summary>加载整张 PNG 为 Sprite，缺失时安静返回 null（用于可选的差分立绘）。</summary>
    static Sprite TryLoadWholeSprite(string resourcePath, float pixelsPerUnit = 100f)
    {
        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        if (texture == null) return null;

        return Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0f),
            pixelsPerUnit);
    }
}
