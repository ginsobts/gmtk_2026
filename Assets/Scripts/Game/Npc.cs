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
    Sprite _activeSprite;       // 当前逻辑立绘（拍照异常恢复时用）
    Vector3 _baseScale;
    Vector3 _activeScale;
    Vector3 _basePortraitPos;

    bool _inFrame;
    PoseType _pose;
    bool _revealedByPose;       // 因摆动作而露馅（六指 / 可怕笑）
    bool _deflated;             // 变瘪人已被接触
    float _lookAwayTimer;       // 顾映表情切换计时（N3）
    Sprite[] _lookAwayFrames;   // 顾映 look-away 循环帧：[面无, 诡异微笑, 极度悲伤]
    int _lookAwayIndex;         // 当前表情帧下标

    // 头顶标记 / 靠近提示
    SpriteRenderer _marker;
    bool _nearest;
    float _pulseT;
    float _markerPhase;

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
            int idx = 0;
            _lookAwayFrames[idx++] = _normalSprite;
            if (s1 != null) _lookAwayFrames[idx++] = s1;
            if (s2 != null) _lookAwayFrames[idx++] = s2;
            _lookAwayIndex = 0;
        }

        EnsureMarker();
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
    }

    void UpdateMarker()
    {
        if (_marker == null) return;
        var gm = GameManager.Instance;
        bool playing = gm == null || gm.State == GameState.Playing;

        // 相机/对话/结算态一律隐藏，避免出现在照片里。
        bool show = playing && (marked || _nearest);
        if (_marker.enabled != show) _marker.enabled = show;
        if (!show) return;

        if (marked)
        {
            _marker.sprite = GeneratedArt.SoftDotSprite;
            _marker.color = new Color(1f, 0.55f, 0.2f, 1f);
        }
        else
        {
            _marker.sprite = GeneratedArt.DownArrowSprite;
            _marker.color = new Color(0.9f, 1f, 1f, 0.95f);
        }

        // 头顶定位 + 上下浮动
        float bob = Mathf.Sin(Time.unscaledTime * 3.2f + _markerPhase) * 0.08f;
        Vector3 top = _renderer != null ? _renderer.bounds.center + Vector3.up * (_renderer.bounds.extents.y + 0.45f) : transform.position + Vector3.up * 2f;
        _marker.transform.position = top + Vector3.up * bob;

        if (_pulseT > 0f) _pulseT -= Time.unscaledDeltaTime;
        float pulse = Mathf.Max(0f, _pulseT) / 0.3f;
        float s = 0.32f * (1f + 0.6f * pulse);
        _marker.transform.localScale = Vector3.one * s;
    }

    public void SetMarked(bool value)
    {
        marked = value;
        if (value) _pulseT = 0.3f;
        RefreshColor();
    }

    /// <summary>设置该 NPC 的朝向（供出生点 / 配置使用）。</summary>
    public void SetFacing(float yaw, bool faceCamera)
    {
        if (_renderer == null) return;
        var cf = _renderer.GetComponent<CameraFacingSprite>();
        if (cf != null) { cf.yawOffset = yaw; cf.faceCamera = faceCamera; }
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

    /// <summary>按时间轴 stage 切换该 NPC 的正常立绘（T4/N4：如魏大爷随 stage 被扒皮）。</summary>
    public void ApplyStage(int stage)
    {
        if (_renderer == null) return;
        Sprite s = GeneratedArt.GetCharacterStageSprite(artFolder, stage);
        _stageSprite = s != null ? s : _normalSprite;
        if (_pose == PoseType.None && !_revealedByPose && !_deflated)
            SetBodySprite(_stageSprite, matchHeight: false);
    }

    /// <summary>指挥该 NPC 摆动作。所有 NPC 都会换成对应姿势立绘；部分伪人会当场露馅。</summary>
    public void SetPose(PoseType pose)
    {
        if (_deflated) return;         // 已变瘪则保持
        _pose = pose;
        _revealedByPose = false;

        if (kind == NpcKind.SixFinger && pose == PoseType.Yeah)
        {
            _revealedByPose = true;
            SetBodySprite(GeneratedArt.SixFingerRevealSprite, matchHeight: true);
        }
        else if (kind == NpcKind.ScarySmile && pose == PoseType.Smile)
        {
            _revealedByPose = true;
            SetBodySprite(GeneratedArt.ScarySmileRevealSprite, matchHeight: true);
        }
        else if (pose == PoseType.None)
        {
            SetBodySprite(_stageSprite, matchHeight: false);
        }
        else
        {
            // 普通姿势差分（同一角色），没有对应美术就保持普通立绘
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
        if (kind == NpcKind.PhotoMissing)
        {
            _renderer.enabled = false;                 // 照片里消失
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
