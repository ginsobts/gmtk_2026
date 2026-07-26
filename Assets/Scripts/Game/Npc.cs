using UnityEngine;

/// <summary>玩家可以指挥取景框内的 NPC 摆的动作。</summary>
public enum PoseType { None, Yeah, Smile }

/// <summary>
/// 一个 NPC（普通人或伪人）。挂在程序化生成的 2D 立绘上。
/// 负责：身份数据、镜头高亮、被指挥摆动作（所有 NPC 都支持）、以及各类伪人的露馅表现：
/// 六指（比耶）、一笑变可怕（笑）、掉帧（镜头抖动）、拼接（照片里露馅）、
/// 照片消失（照片里没有）、变瘪（玩家接触）。
/// </summary>
public class Npc : MonoBehaviour
{
    public string npcName;
    public string charId;       // 角色 id（如 an_an），用于特定角色逻辑（N5）
    public NpcKind kind;
    public string artFolder;    // 该角色的美术文件夹，例如 Characters/wei_daye
    public string dialogueId;   // 普通状态对话 id（可空）
    public bool marked;         // 玩家已标记为嫌疑人（未提交前不告知对错）
    public bool harmless;       // 无害正常人：错认也不算失败（策划 N2）
    public int dialogueVisitCount;  // count 模式对话已完成次数（GameContent.ResolveDialogue 用；安安第5次真心话即靠它）

    public bool IsImposter => kind != NpcKind.Normal;
    public PoseType CurrentPose => _pose;

    SpriteRenderer _renderer;
    Sprite _normalSprite;
    Sprite _stageSprite;        // 当前 stage 的正常立绘（T4：随时间轴换，如魏大爷扒皮）
    int _stage = 1;             // 当前时间轴 stage（1..N），供对话立绘按 phase 同步（如吴昂拼接演变）
    Sprite _activeSprite;       // 当前逻辑立绘（拍照异常恢复时用）
    Vector3 _baseScale;
    Vector3 _activeScale;
    Vector3 _basePortraitPos;

    bool _inFrame;
    PoseType _pose;
    bool _revealedByPose;       // 因摆动作而露馅（六指 / 可怕笑）
    bool _exposed;              // 已被抓到露馅(持久)：韩露被拍到鬼脸笑后，对话立绘持续显示六指伪人形态
    bool _deflated;             // 变瘪人已被接触
    float _lookAwayTimer;       // 顾映表情切换计时（N3）
    Sprite[] _lookAwayFrames;   // 顾映 look-away 循环帧：[面无, 诡异微笑, 极度悲伤]
    string[] _lookAwayExprs;    // 与 _lookAwayFrames 平行：每帧对应立绘表情后缀（null=neutral），供对话立绘同步
    int _lookAwayIndex;         // 当前表情帧下标

    // 头顶标记 / 靠近提示
    SpriteRenderer _marker;
    SpriteRenderer _feetRing;   // 脚下半透明白圈：提示"当前可交互的就是这个人"
    bool _nearest;
    float _pulseT;
    float _markerPhase;

    // 头顶名字/职位（默认隐藏，由右上角按钮全局开关）
    GameObject _labelGO;
    TextMesh _label;
    string _labelName;
    string _labelTitle;

    public void Setup(string name, string charId, NpcKind kind, string artFolder, string dialogueId, SpriteRenderer renderer, bool harmless = false)
    {
        npcName = name;
        this.charId = charId;
        this.kind = kind;
        this.artFolder = artFolder;
        this.dialogueId = dialogueId;
        this.harmless = harmless;
        dialogueVisitCount = 0;
        _renderer = renderer;
        _normalSprite = renderer != null ? renderer.sprite : null;
        _stageSprite = _normalSprite;
        _baseScale = renderer != null ? renderer.transform.localScale : Vector3.one;
        _basePortraitPos = renderer != null ? renderer.transform.localPosition : Vector3.zero;

        marked = false;
        _inFrame = false;
        _pose = PoseType.None;
        _revealedByPose = false;
        _exposed = false;
        _deflated = false;

        _activeSprite = _normalSprite;
        _activeScale = _baseScale;

        // 顾映 look-away（N3）：加载三表情帧 [面无(base)/诡异微笑/极度悲伤]，缺图自动跳过
        if (kind == NpcKind.LookAway)
        {
            var s1 = GeneratedArt.GetCharacterVariantSprite(artFolder, "reveal_smile");
            var s2 = GeneratedArt.GetCharacterVariantSprite(artFolder, "reveal_sad");
            int n = 1 + (s1 != null ? 1 : 0) + (s2 != null ? 1 : 0);
            _lookAwayFrames = new Sprite[n];
            _lookAwayExprs = new string[n];   // 平行记录每帧对应的立绘表情后缀，供对话立绘同步
            int idx = 0;
            _lookAwayFrames[idx] = _normalSprite; _lookAwayExprs[idx] = null; idx++;
            if (s1 != null) { _lookAwayFrames[idx] = s1; _lookAwayExprs[idx] = "reveal_smile"; idx++; }
            if (s2 != null) { _lookAwayFrames[idx] = s2; _lookAwayExprs[idx] = "reveal_sad"; idx++; }
            _lookAwayIndex = 0;
        }

        EnsureMarker();
        EnsureFeetRing();
        _markerPhase = Random.value * Mathf.PI * 2f;
        RefreshColor();
    }

    void EnsureMarker()
    {
        if (_marker != null) return;
        var go = new GameObject("Marker");
        go.transform.SetParent(transform, false);
        _marker = go.AddComponent<SpriteRenderer>();
        _marker.sortingOrder = 20;
        go.AddComponent<CameraFacingSprite>();
        _marker.enabled = false;
    }

    void EnsureFeetRing()
    {
        if (_feetRing != null) return;
        var go = new GameObject("FeetRing");
        go.transform.SetParent(transform, false);
        // 平铺在地面上（法线朝上），2.5D 斜视角下自然呈椭圆，像地面上的光圈
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        go.transform.localPosition = new Vector3(0f, 0.03f, 0f);   // 略高于脚下阴影，避免被盖住
        _feetRing = go.AddComponent<SpriteRenderer>();
        _feetRing.sprite = GeneratedArt.RingSprite;
        _feetRing.color = new Color(1f, 1f, 1f, 0.5f);
        _feetRing.sortingOrder = 5;   // 在脚下阴影(4)之上、角色身体之下
        _feetRing.enabled = false;
    }

    void Update()
    {
        if (_renderer == null) return;

        var gm = GameManager.Instance;

        // 掉帧人：在镜头里时不停抖动 / 瞬移（调试冻结时停下）
        if (kind == NpcKind.FrameDrop && !DebugControl.Frozen)
        {
            if (_inFrame && !_deflated)
            {
                float jx = (Random.value - 0.5f) * 0.28f;
                float jy = (Random.value - 0.5f) * 0.18f;
                _renderer.transform.localPosition = _basePortraitPos + new Vector3(jx, jy, 0f);
                _renderer.enabled = Random.value > 0.12f; // 偶尔闪一下
            }
            else if (_renderer.transform.localPosition != _basePortraitPos)
            {
                _renderer.transform.localPosition = _basePortraitPos;
                _renderer.enabled = true;
            }
        }

        // 变瘪人（策划案）：场景里不做变身。破绽在【对话立绘】(袖口露出一小截变瘪的手) + 【对话】(表现抗拒肢体接触)。
        // 原"靠近整体塌成空衣服"是占位错图(通用成人西装)，已按策划案去掉；_deflated 保留但不再由场景触发。

        // 顾映（look-away, N3）：镜头移开时在三表情间循环切换；对准取景框则定格「面无表情」(帧0)
        if (kind == NpcKind.LookAway && !DebugControl.Frozen && !_deflated && !_revealedByPose &&
            _lookAwayFrames != null && _lookAwayFrames.Length > 1 &&
            gm != null && (gm.State == GameState.Playing || gm.State == GameState.Camera))
        {
            if (_inFrame)
            {
                // 被镜头对准：立即定格面无表情，暂停循环
                if (_lookAwayIndex != 0)
                {
                    _lookAwayIndex = 0;
                    SetBodySprite(_lookAwayFrames[0], matchHeight: true);
                }
                _lookAwayTimer = 1.2f;
            }
            else
            {
                // 镜头移开：周期性切到下一表情（面无→诡异微笑→极度悲伤→面无…）
                _lookAwayTimer -= Time.deltaTime;
                if (_lookAwayTimer <= 0f)
                {
                    _lookAwayIndex = (_lookAwayIndex + 1) % _lookAwayFrames.Length;
                    SetBodySprite(_lookAwayFrames[_lookAwayIndex], matchHeight: true);
                    _lookAwayTimer = 1.2f;
                }
            }
        }

        UpdateMarker();
        UpdateFeetRing();
        UpdateLabel();
    }

    void UpdateMarker()
    {
        if (_marker == null) return;
        var gm = GameManager.Instance;
        bool playing = gm == null || gm.State == GameState.Playing;

        // 头顶标记只表示"已指认"。相机/对话/结算态一律隐藏，避免出现在照片里。
        // "当前可交互"改用脚下白圈提示（见 UpdateFeetRing）。
        bool show = playing && marked;
        if (_marker.enabled != show) _marker.enabled = show;
        if (!show) return;

        _marker.sprite = GeneratedArt.SoftDotSprite;
        _marker.color = new Color(1f, 0.12f, 0.1f, 1f);   // 更红更醒目

        // 头顶定位 + 上下浮动
        float bob = Mathf.Sin(Time.unscaledTime * 3.2f + _markerPhase) * 0.1f;
        Vector3 top = _renderer != null ? _renderer.bounds.center + Vector3.up * (_renderer.bounds.extents.y + 0.5f) : transform.position + Vector3.up * 2f;
        _marker.transform.position = top + Vector3.up * bob;

        if (_pulseT > 0f) _pulseT -= Time.unscaledDeltaTime;
        float pulse = Mathf.Max(0f, _pulseT) / 0.3f;
        // 标记态：更大 + 持续呼吸；点击瞬间再额外弹一下
        float breathe = 1f + 0.16f * Mathf.Sin(Time.unscaledTime * 4.2f + _markerPhase);
        float s = 0.62f * breathe * (1f + 0.6f * pulse);
        _marker.transform.localScale = Vector3.one * s;
    }

    void UpdateFeetRing()
    {
        if (_feetRing == null) return;
        var gm = GameManager.Instance;
        // 仅在探索态、且是"当前最近可交互"的 NPC 时显示脚下白圈
        bool show = (gm == null || gm.State == GameState.Playing) && _nearest;
        if (_feetRing.enabled != show) _feetRing.enabled = show;
        if (!show) return;

        // 圈的大小按角色实际脚下宽度自适应（root 缩放为 1，RingSprite 在 localScale=1 时世界直径约 1.28）
        float footW = _renderer != null ? _renderer.bounds.size.x : 1f;
        float diameter = Mathf.Clamp(footW * 1.35f, 0.7f, 2.2f);
        float baseScale = diameter / 1.28f;

        // 轻微呼吸（透明度 + 尺寸），让它更像"提示光圈"
        float t = Time.unscaledTime * 3f + _markerPhase;
        _feetRing.color = new Color(1f, 1f, 1f, 0.42f + 0.18f * (0.5f + 0.5f * Mathf.Sin(t)));
        _feetRing.transform.localScale = Vector3.one * baseScale * (1f + 0.05f * Mathf.Sin(t));
    }

    public void SetMarked(bool value)
    {
        marked = value;
        if (value) _pulseT = 0.3f;
        RefreshColor();
    }

    /// <summary>设置头顶显示的名字/职位（本地化后的文本）。默认隐藏，由右上角按钮全局开关。</summary>
    public void SetHeadLabel(string name, string title)
    {
        _labelName = name;
        _labelTitle = title;
        if (_label != null) _label.text = BuildLabelText();
    }

    string BuildLabelText()
    {
        // 名字在上，职位用小字灰色显示在下面
        if (string.IsNullOrEmpty(_labelTitle)) return _labelName;
        return $"{_labelName}\n<size=30><color=#B7BCC7>{_labelTitle}</color></size>";
    }

    void EnsureLabel()
    {
        if (_label != null) return;
        var ui = GameManager.Instance != null ? GameManager.Instance.UI : null;
        Font font = ui != null ? ui.Font : null;
        if (font == null) return;   // 字体未就绪，稍后再建

        _labelGO = new GameObject("HeadLabel");
        _labelGO.transform.SetParent(transform, false);
        _label = _labelGO.AddComponent<TextMesh>();
        _label.font = font;
        _label.fontSize = 56;
        _label.characterSize = 0.05f;
        _label.anchor = TextAnchor.LowerCenter;
        _label.alignment = TextAlignment.Center;
        _label.richText = true;
        _label.color = Color.white;
        _label.text = BuildLabelText();

        var mr = _labelGO.GetComponent<MeshRenderer>();
        mr.sharedMaterial = font.material;
        mr.sortingOrder = 30;

        _labelGO.AddComponent<CameraFacingSprite>();
        _labelGO.SetActive(false);
    }

    void UpdateLabel()
    {
        var gm = GameManager.Instance;
        bool show = gm != null && gm.ShowNpcLabels && gm.State == GameState.Playing && !string.IsNullOrEmpty(_labelName);

        if (show && _label == null) EnsureLabel();
        if (_labelGO == null) return;

        if (_labelGO.activeSelf != show) _labelGO.SetActive(show);
        if (!show) return;

        // 头顶定位：比标记再高一点，避免重叠
        Vector3 top = _renderer != null
            ? _renderer.bounds.center + Vector3.up * (_renderer.bounds.extents.y + 0.62f)
            : transform.position + Vector3.up * 2.2f;
        _labelGO.transform.position = top;
    }

    /// <summary>设置该 NPC 的朝向（供出生点 / 配置使用）。</summary>
    public void SetFacing(float yaw, bool faceCamera)
    {
        if (_renderer == null) return;
        var cf = _renderer.GetComponent<CameraFacingSprite>();
        if (cf != null) { cf.yawOffset = yaw; cf.faceCamera = faceCamera; }
    }

    /// <summary>读回当前朝向（供摆位工具保存）。</summary>
    public bool TryGetFacing(out float yaw, out bool faceCamera)
    {
        yaw = 0f; faceCamera = true;
        if (_renderer == null) return false;
        var cf = _renderer.GetComponent<CameraFacingSprite>();
        if (cf == null) return false;
        yaw = cf.yawOffset; faceCamera = cf.faceCamera; return true;
    }

    public void SetNearest(bool value)
    {
        _nearest = value;
    }

    public void SetInFrame(bool value)
    {
        if (_inFrame == value) return;
        _inFrame = value;
        RefreshColor();
    }

    /// <summary>
    /// 该 NPC 当前应显示的【对话立绘表情后缀】（如 "reveal_smile"）；neutral/无变体返回 null。
    /// 让对话立绘与场景棋子的露馅态同步：顾映按 look-away 当前表情帧返回，进对话时定格该帧。
    /// </summary>
    public string PortraitExpression()
    {
        if (kind == NpcKind.LookAway && _lookAwayExprs != null &&
            _lookAwayIndex >= 0 && _lookAwayIndex < _lookAwayExprs.Length)
            return _lookAwayExprs[_lookAwayIndex];
        // 拼接人（吴昂）：对话立绘随时间轴 stage 演变，与场景棋子(base/s2/s3)配套
        if (kind == NpcKind.Stitched && _stage >= 2)
            return "s" + _stage;
        // 六指人（韩露）：被抓到笑出鬼脸笑后(_exposed)，对话立绘持续显示六指伪人形态
        if (kind == NpcKind.SixFinger && _exposed)
            return "reveal";
        return null;
    }

    /// <summary>按时间轴 stage 切换该 NPC 的正常立绘（T4/N4：如魏大爷随 stage 被扒皮）。</summary>
    public void ApplyStage(int stage)
    {
        _stage = stage;
        if (_renderer == null) return;
        Sprite s = GeneratedArt.GetCharacterStageSprite(artFolder, stage);
        _stageSprite = s != null ? s : _normalSprite;
        if (_pose == PoseType.None && !_revealedByPose && !_deflated)
            SetBodySprite(_stageSprite, matchHeight: false);
    }

    /// <summary>
    /// 指挥该 NPC 摆拍照动作（比耶 / 笑）。所有 NPC 换成对应姿势差分立绘（美术对齐的等大叠加合成，
    /// Art/&lt;folder&gt;/{yeah,smile,grimace}.png）。韩露（六指人）命令"笑"时笑不出正常笑、露出【鬼脸笑】(红眼)当场露馅。
    /// </summary>
    public void SetPose(PoseType pose)
    {
        if (_deflated) return;         // 已变瘪则保持
        _pose = pose;
        _revealedByPose = false;

        if (pose == PoseType.None)
        {
            SetBodySprite(_stageSprite, matchHeight: false);
        }
        else if (pose == PoseType.Smile && kind == NpcKind.SixFinger)
        {
            // 韩露（六指人）：命令"笑"时露出鬼脸笑（红眼）—— 破绽。此刻拍照即拍到；六指伪人立绘在对话(han_c3)展示。
            _revealedByPose = true;
            _exposed = true;   // 持久标记：之后对话立绘持续显示六指伪人形态
            Sprite grim = GeneratedArt.GetCharacterVariantSprite(artFolder, "grimace");
            SetBodySprite(grim != null ? grim : _normalSprite, matchHeight: false);
        }
        else
        {
            // 通用拍照姿势差分：比耶=yeah.png / 正常笑=smile.png。缺图回退普通立绘。
            Sprite poseSprite = GeneratedArt.GetCharacterPoseSprite(artFolder, pose == PoseType.Smile);
            SetBodySprite(poseSprite != null ? poseSprite : _normalSprite, matchHeight: false);
        }

        RefreshColor();
    }

    /// <summary>切换身体立绘，可选按普通立绘的世界高度自动缩放（用于通用露馅图）。</summary>
    void SetBodySprite(Sprite sprite, bool matchHeight)
    {
        if (_renderer == null || sprite == null) return;
        _renderer.sprite = sprite;

        Vector3 scale = _baseScale;
        if (matchHeight && _normalSprite != null)
        {
            float baseH = _normalSprite.bounds.size.y;
            float newH = sprite.bounds.size.y;
            if (newH > 0.0001f) scale = _baseScale * (baseH / newH);
        }
        _renderer.transform.localScale = scale;

        _activeSprite = sprite;
        _activeScale = scale;
    }

    void RefreshColor()
    {
        if (_renderer == null) return;

        if (_revealedByPose || _deflated)
            _renderer.color = new Color(1f, 0.82f, 0.82f, 1f); // 露馅：略带红色警示
        else if (_inFrame)
            _renderer.color = new Color(0.75f, 1f, 1f, 1f);     // 在镜头中：轻微高亮
        else if (marked)
            _renderer.color = new Color(1f, 0.85f, 0.45f, 1f);  // 已标记为嫌疑人：橙黄
        else
            _renderer.color = Color.white;
    }

    /// <summary>NPC 在屏幕上的取景参考点（用身体中心）。</summary>
    public Vector3 GetScreenPoint(Camera cam)
    {
        Vector3 world = _renderer != null ? _renderer.bounds.center : transform.position + Vector3.up;
        return cam.WorldToScreenPoint(world);
    }

    // ---- 照片异常：拍照瞬间对离屏渲染生效（不改动 _active，便于之后还原）----
    public void ApplyPhotoState()
    {
        if (_renderer == null) return;
        if (kind == NpcKind.PhotoMismatch)
        {
            // 照片里「变成另外的样子」：换成 Art/<folder>/photo.png（本人在场景里不变），照片与本人不一致。
            Sprite alt = GeneratedArt.GetCharacterVariantSprite(artFolder, "photo");
            if (alt != null)
            {
                _renderer.sprite = alt;
                float baseH = _normalSprite != null ? _normalSprite.bounds.size.y : 0f;
                float newH = alt.bounds.size.y;
                _renderer.transform.localScale =
                    (baseH > 0.0001f && newH > 0.0001f) ? _baseScale * (baseH / newH) : _baseScale;
            }
        }
        else if (kind == NpcKind.Stitched && !_deflated)
        {
            Sprite stitched = GeneratedArt.StitchedRevealSprite;
            if (stitched != null)
            {
                _renderer.sprite = stitched;           // 照片里露出拼接
                float baseH = _normalSprite != null ? _normalSprite.bounds.size.y : 0f;
                float newH = stitched.bounds.size.y;
                _renderer.transform.localScale =
                    (baseH > 0.0001f && newH > 0.0001f) ? _baseScale * (baseH / newH) : _baseScale;
            }
        }
    }

    public void RestorePhotoState()
    {
        if (_renderer == null) return;
        _renderer.enabled = true;
        _renderer.sprite = _activeSprite;
        _renderer.transform.localScale = _activeScale;
    }
}
