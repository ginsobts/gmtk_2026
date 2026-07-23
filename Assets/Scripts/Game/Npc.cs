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
    public NpcKind kind;
    public int characterIndex;
    public bool marked;         // 玩家已标记为嫌疑人（未提交前不告知对错）

    public bool IsImposter => kind != NpcKind.Normal;
    public PoseType CurrentPose => _pose;

    const float DeflateContactRadius = 1.15f;

    SpriteRenderer _renderer;
    Sprite _normalSprite;
    Sprite _activeSprite;       // 当前逻辑立绘（拍照异常恢复时用）
    Vector3 _baseScale;
    Vector3 _activeScale;
    Vector3 _basePortraitPos;

    bool _inFrame;
    PoseType _pose;
    bool _revealedByPose;       // 因摆动作而露馅（六指 / 可怕笑）
    bool _deflated;             // 变瘪人已被接触

    public void Setup(string name, NpcKind kind, int characterIndex, SpriteRenderer renderer)
    {
        npcName = name;
        this.kind = kind;
        this.characterIndex = characterIndex;
        _renderer = renderer;
        _normalSprite = renderer != null ? renderer.sprite : null;
        _baseScale = renderer != null ? renderer.transform.localScale : Vector3.one;
        _basePortraitPos = renderer != null ? renderer.transform.localPosition : Vector3.zero;

        marked = false;
        _inFrame = false;
        _pose = PoseType.None;
        _revealedByPose = false;
        _deflated = false;

        _activeSprite = _normalSprite;
        _activeScale = _baseScale;
        RefreshColor();
    }

    void Update()
    {
        if (_renderer == null) return;

        var gm = GameManager.Instance;

        // 掉帧人：在镜头里时不停抖动 / 瞬移
        if (kind == NpcKind.FrameDrop)
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

        // 变瘪人：玩家靠近接触时变瘪（一旦变瘪保持）
        if (kind == NpcKind.Deflate && !_deflated && gm != null &&
            gm.State == GameState.Playing && gm.PlayerTransform != null)
        {
            float d = Vector3.Distance(gm.PlayerTransform.position, transform.position);
            if (d < DeflateContactRadius)
            {
                _deflated = true;
                SetBodySprite(GeneratedArt.DeflateRevealSprite, matchHeight: false);
                RefreshColor();
            }
        }
    }

    public void SetMarked(bool value)
    {
        marked = value;
        RefreshColor();
    }

    public void SetInFrame(bool value)
    {
        if (_inFrame == value) return;
        _inFrame = value;
        RefreshColor();
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
            SetBodySprite(_normalSprite, matchHeight: false);
        }
        else
        {
            // 普通姿势差分（同一角色），没有对应美术就保持普通立绘
            Sprite poseSprite = GeneratedArt.GetCharacterPoseSprite(characterIndex, pose == PoseType.Smile);
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
