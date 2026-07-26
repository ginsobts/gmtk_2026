using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 给面向相机的 Sprite 立绘挂一个「只投阴影、不可见」的竖直网格，
/// 让 Built-in 管线下 Directional Light 的阴影能落到 3D 地面上。
/// 由 GameManager.BuildPerson / BuildCard 自动挂载，无需手动添加。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]   // 晚于 CameraFacingSprite，确保读到最新朝向
public class SpriteShadowCaster : MonoBehaviour
{
    const float AlphaCutoff = 0.35f;

    static Material _matTemplate;
    static Mesh _unitQuad;

    SpriteRenderer _source;
    Transform _visual;
    Transform _shadowTransform;
    Material _mat;
    Sprite _lastSprite;
    Vector3 _lastLossyScale;
    bool _lastFlipX;

    public void Init(SpriteRenderer source)
    {
        _source = source;
        _visual = source != null ? source.transform : null;
        EnsureShadowMesh();
        Sync(true);
        UpdateTransform();
    }

    void LateUpdate()
    {
        if (_source == null || _visual == null || _shadowTransform == null) return;
        if (_source.sprite != _lastSprite || _visual.lossyScale != _lastLossyScale || _source.flipX != _lastFlipX)
            Sync(false);
        UpdateTransform();
    }

    void EnsureShadowMesh()
    {
        if (_shadowTransform != null) return;

        var go = new GameObject("GroundShadowMesh");
        go.transform.SetParent(transform, false);

        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = UnitQuad();

        var mr = go.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        mr.receiveShadows = false;
        mr.lightProbeUsage = LightProbeUsage.Off;
        mr.reflectionProbeUsage = ReflectionProbeUsage.Off;

        if (_matTemplate == null)
        {
            var shader = Shader.Find("GMTK/SpriteShadowCaster")
                         ?? Shader.Find("Legacy Shaders/Transparent/Cutout/Diffuse");
            if (shader == null)
            {
                Debug.LogWarning("SpriteShadowCaster: 找不到阴影 Shader，地面阴影不会显示。");
                return;
            }

            _matTemplate = new Material(shader);
            if (_matTemplate.HasProperty("_Cutoff"))
                _matTemplate.SetFloat("_Cutoff", AlphaCutoff);
        }

        _mat = new Material(_matTemplate);
        mr.sharedMaterial = _mat;
        _shadowTransform = go.transform;
    }

    void Sync(bool forceScale)
    {
        if (_shadowTransform == null || _mat == null) return;

        var sprite = _source.sprite;
        if (sprite == null)
        {
            _shadowTransform.gameObject.SetActive(false);
            return;
        }

        _shadowTransform.gameObject.SetActive(true);
        bool spriteChanged = sprite != _lastSprite;
        bool scaleChanged = _visual.lossyScale != _lastLossyScale;
        bool flipChanged = _source.flipX != _lastFlipX;
        _lastSprite = sprite;
        _lastLossyScale = _visual.lossyScale;
        _lastFlipX = _source.flipX;

        var tex = sprite.texture;
        var r = sprite.textureRect;
        _mat.mainTexture = tex;
        _mat.mainTextureOffset = new Vector2(r.x / tex.width, r.y / tex.height);
        _mat.mainTextureScale = new Vector2(r.width / tex.width, r.height / tex.height);

        if (forceScale || spriteChanged || scaleChanged || flipChanged)
            ApplyScale(sprite);
    }

    void ApplyScale(Sprite sprite)
    {
        var size = sprite.bounds.size;
        var ws = _visual.lossyScale;
        float flipX = _source.flipX ? -1f : 1f;
        _shadowTransform.localScale = new Vector3(size.x * ws.x * flipX, size.y * ws.y, 1f);
    }

    void UpdateTransform()
    {
        if (_shadowTransform == null) return;

        _shadowTransform.position = _visual.position;
        // 与立绘同向：宽图/双人并排等不对称 sprite 若朝向太阳而非相机，阴影会左右颠倒
        _shadowTransform.rotation = _visual.rotation;
    }

    static Mesh UnitQuad()
    {
        if (_unitQuad != null) return _unitQuad;
        _unitQuad = new Mesh { name = "SpriteShadowUnitQuad" };
        _unitQuad.vertices = new[]
        {
            new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f),
            new Vector3(-0.5f, 1f, 0f), new Vector3(0.5f, 1f, 0f)
        };
        _unitQuad.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
        _unitQuad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        _unitQuad.RecalculateNormals();
        return _unitQuad;
    }

    void OnDestroy()
    {
        if (_mat != null) Destroy(_mat);
    }
}
