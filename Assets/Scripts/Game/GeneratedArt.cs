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

    const int CharacterCount = 8;

    static Sprite[] _characterSprites;
    static Sprite[] _yeahSprites;
    static Sprite[] _smileSprites;
    static Sprite[] _iconSprites;
    static Sprite[] _propSprites;
    static Sprite[] _forestSprites;
    static Sprite _playerSprite;
    static Sprite _sixFingerReveal, _scarySmileReveal, _stitchedReveal, _deflateReveal;

    public static Texture2D GroundTexture =>
        _ground ??= Resources.Load<Texture2D>("Art/town_ground_texture");

    public static Sprite GetCharacterSprite(int index)
    {
        if (_characterSprites == null)
        {
            _characterSprites = new Sprite[CharacterCount];
            for (int i = 0; i < _characterSprites.Length; i++)
                _characterSprites[i] = LoadWholeSprite($"Art/Characters/npc_{i:00}");
        }

        return _characterSprites[Mathf.Abs(index) % _characterSprites.Length];
    }

    /// <summary>该 NPC 的姿势差分（比耶 / 笑）。没有对应美术时返回 null，调用方回退到普通立绘。</summary>
    public static Sprite GetCharacterPoseSprite(int index, bool smile)
    {
        _yeahSprites ??= new Sprite[CharacterCount];
        _smileSprites ??= new Sprite[CharacterCount];
        int i = Mathf.Abs(index) % CharacterCount;
        var cache = smile ? _smileSprites : _yeahSprites;
        string suffix = smile ? "smile" : "yeah";
        return cache[i] ??= TryLoadWholeSprite($"Art/Characters/npc_{i:00}_{suffix}");
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
