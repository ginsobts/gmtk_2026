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
    static Sprite _playerSideSprite;
    static Sprite _playerFrontSprite;
    static Sprite _deathMonsterSprite;
    static Sprite[] _deathMonsterWalkFrames;
    static Sprite _sixFingerReveal, _scarySmileReveal, _stitchedReveal, _deflateReveal;

    // 程序化生成的表现用贴图（阴影/软点/箭头/暗角）
    static Sprite _blobShadow, _softDot, _downArrow, _vignette, _recDot, _ring;

    public static Texture2D GroundTexture =>
        _ground ??= Resources.Load<Texture2D>("Art/town_ground_texture");

    static readonly Dictionary<string, Texture2D> _groundVariants = new Dictionary<string, Texture2D>();
    static readonly Dictionary<string, Sprite> _propFileCache = new Dictionary<string, Sprite>();
    static readonly Dictionary<string, Sprite> _endingCache = new Dictionary<string, Sprite>();

    /// <summary>加载独立的场景道具 PNG：Resources/Art/Props/&lt;name&gt;（树/灌木/椅子/垃圾桶/健身器材）。缺失静默返回 null。</summary>
    public static Sprite PropFileSprite(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (_propFileCache.TryGetValue(name, out var s)) return s;
        s = TryLoadWholeSprite("Art/Props/" + name);
        _propFileCache[name] = s;
        return s;
    }

    /// <summary>结算大图：Resources/Art/Endings/win 或 lose。缺失返回 null（结果面板据此隐藏图片）。</summary>
    public static Sprite EndingSprite(bool win)
    {
        string name = win ? "win" : "lose";
        if (_endingCache.TryGetValue(name, out var s)) return s;
        s = TryLoadWholeSprite("Art/Endings/" + name);
        _endingCache[name] = s;
        return s;
    }

    /// <summary>按后缀取地面贴图变体：Resources/Art/ground_&lt;suffix&gt;（如 ground_phase2 / ground_death）。缺则返回 null（调用方回退到基础地面）。</summary>
    public static Texture2D GroundTextureNamed(string suffix)
    {
        if (string.IsNullOrEmpty(suffix)) return null;
        if (_groundVariants.TryGetValue(suffix, out var t)) return t;
        t = Resources.Load<Texture2D>("Art/ground_" + suffix);
        _groundVariants[suffix] = t;
        return t;
    }

    /// <summary>按角色美术文件夹加载默认立绘（artFolder 例如 Characters/npc_00）。</summary>
    public static Sprite GetCharacterSprite(string artFolder)
    {
        string key = string.IsNullOrEmpty(artFolder) ? "<none>" : artFolder;
        if (_characterCache.TryGetValue(key, out var s)) return s;
        s = TryLoadWholeSprite($"Art/{artFolder}/base");   // 缺图安静返回 null
        if (s == null) s = MakeCharacterPlaceholder(key);  // 兜底：程序占位立绘（缺美术图也可见、不崩）
        _characterCache[key] = s;
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

    /// <summary>该角色在某 stage 的正常立绘（T4：Art/&lt;folder&gt;/s{stage}.png）。缺失时回退到 base 立绘。</summary>
    public static Sprite GetCharacterStageSprite(string artFolder, int stage)
    {
        if (stage <= 1 || string.IsNullOrEmpty(artFolder)) return GetCharacterSprite(artFolder);
        string key = artFolder + "/s" + stage;
        if (_posesCache.TryGetValue(key, out var s)) return s != null ? s : GetCharacterSprite(artFolder);
        s = TryLoadWholeSprite($"Art/{artFolder}/s{stage}");
        _posesCache[key] = s;
        return s != null ? s : GetCharacterSprite(artFolder);
    }

    /// <summary>角色命名差分棋子（如顾映 look-away 的 reveal_smile/reveal_sad）。缺失返回 null，调用方自行回退。</summary>
    public static Sprite GetCharacterVariantSprite(string artFolder, string variant)
    {
        if (string.IsNullOrEmpty(artFolder) || string.IsNullOrEmpty(variant)) return null;
        string key = artFolder + "/" + variant;
        if (_posesCache.TryGetValue(key, out var s)) return s;
        s = TryLoadWholeSprite($"Art/{artFolder}/{variant}");
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

    /// <summary>主角侧面棋子（默认朝左；缺图返回 null，调用方回退到背面 PlayerSprite）。</summary>
    public static Sprite PlayerSideSprite =>
        _playerSideSprite ??= TryLoadWholeSprite("Art/Characters/player_side");

    /// <summary>主角正面棋子（缺图返回 null，调用方回退到背面 PlayerSprite）。</summary>
    public static Sprite PlayerFrontSprite =>
        _playerFrontSprite ??= TryLoadWholeSprite("Art/Characters/player_front");

    /// <summary>死亡演出追逐怪物行走帧：Resources/Art/Anim/gui1..gui7。缺帧时回退到单张立绘。</summary>
    public static Sprite[] DeathMonsterWalkFrames
    {
        get
        {
            if (_deathMonsterWalkFrames != null) return _deathMonsterWalkFrames;

            var list = new List<Sprite>();
            for (int i = 1; i <= 7; i++)
            {
                var frame = TryLoadWholeSprite($"Art/Anim/gui{i}");
                if (frame != null) list.Add(frame);
            }

            _deathMonsterWalkFrames = list.Count > 0
                ? list.ToArray()
                : new[] { DeathMonsterSpriteFallback() };
            return _deathMonsterWalkFrames;
        }
    }

    /// <summary>死亡演出追逐怪物默认立绘（行走帧首帧，或旧占位图）。</summary>
    public static Sprite DeathMonsterSprite =>
        _deathMonsterSprite ??= DeathMonsterWalkFrames[0];

    static Sprite DeathMonsterSpriteFallback() =>
        TryLoadWholeSprite("Art/Imposters/death_monster") ?? MakeMonsterPlaceholder();

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

    /// <summary>白色空心圆环（脚下"可交互"提示，铺在地面上，按需染色/调透明）。</summary>
    public static Sprite RingSprite => _ring ??= MakeRingSprite(128, Color.white, 0.72f, 0.97f);

    /// <summary>指向下方的实心三角（靠近可交互提示）。</summary>
    public static Sprite DownArrowSprite => _downArrow ??= MakeDownTriangleSprite(48);

    /// <summary>相机模式暗角（中间透明、四周变暗）。当前保留备用。</summary>
    public static Sprite VignetteSprite => _vignette ??= MakeVignetteSprite(256, 0.55f);

    /// <summary>REC 小红点（实心圆）。</summary>
    public static Sprite RecDotSprite => _recDot ??= MakeRadialSprite(32, new Color(1f, 0.25f, 0.25f, 1f), 6f);

    /// <summary>缺角色美术图时的程序占位立绘：按名字上色的纸片小人（头+身），脚底为轴。</summary>
    static Sprite MakeCharacterPlaceholder(string seed)
    {
        int w = 96, h = 192;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        int hash = seed != null ? seed.GetHashCode() : 0;
        float hue = (Mathf.Abs(hash) % 360) / 360f;
        Color body = Color.HSVToRGB(hue, 0.45f, 0.85f);
        Color head = Color.HSVToRGB(hue, 0.30f, 0.95f);
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float cx = x / (float)(w - 1) - 0.5f;
                float fy = y / (float)(h - 1);
                bool inside; Color c;
                if (fy > 0.70f)
                {
                    float hy = (fy - 0.85f) / 0.15f;             // 头部椭圆
                    inside = (cx * cx) / (0.22f * 0.22f) + hy * hy < 1f;
                    c = head;
                }
                else
                {
                    float bw = 0.34f - 0.10f * (fy / 0.70f);     // 身体：上宽下窄
                    inside = Mathf.Abs(cx) < bw && fy > 0.02f;
                    c = body;
                }
                px[y * w + x] = inside ? c : new Color(0, 0, 0, 0);
            }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 100f);
    }

    /// <summary>死亡演出怪物占位：暗红近黑的诡异人形 + 两点发光眼，脚底为轴。</summary>
    static Sprite MakeMonsterPlaceholder()
    {
        int w = 128, h = 208;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color bodyDark = new Color(0.06f, 0.02f, 0.03f, 1f);   // 近黑带暗红
        Color eye = new Color(1f, 0.82f, 0.35f, 1f);           // 发光眼
        float exOff = 0.085f, eyeY = 0.83f, eyeR = 0.032f;
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float cx = x / (float)(w - 1) - 0.5f;
                float fy = y / (float)(h - 1);
                bool inside;
                if (fy > 0.66f)
                {
                    float hy = (fy - 0.86f) / 0.16f;                          // 头部椭圆
                    inside = (cx * cx) / (0.19f * 0.19f) + hy * hy < 1f;
                }
                else
                {
                    // 身体：上宽下窄 + 轻微起伏轮廓（诡异感）
                    float bw = (0.30f - 0.10f * (fy / 0.66f)) + 0.02f * Mathf.Sin(fy * 22f);
                    inside = Mathf.Abs(cx) < bw && fy > 0.02f;
                }
                Color c = new Color(0, 0, 0, 0);
                if (inside)
                {
                    c = bodyDark;
                    float dy = fy - eyeY;
                    float dxL = cx + exOff, dxR = cx - exOff;
                    if (dxL * dxL + dy * dy < eyeR * eyeR || dxR * dxR + dy * dy < eyeR * eyeR) c = eye;
                }
                px[y * w + x] = c;
            }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 100f);
    }

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

    /// <summary>生成一个空心圆环贴图：innerFrac~outerFrac 之间为实心，两侧边缘做柔和过渡。</summary>
    static Sprite MakeRingSprite(int size, Color color, float innerFrac, float outerFrac)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float r = size * 0.5f;
        const float edge = 0.06f; // 归一化半径下的柔化宽度
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - r) / r;
                float dy = (y + 0.5f - r) / r;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float aOuter = Mathf.Clamp01((outerFrac - d) / edge);   // 外沿渐隐
                float aInner = Mathf.Clamp01((d - innerFrac) / edge);   // 内沿渐隐
                float a = Mathf.Min(aOuter, aInner);
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
