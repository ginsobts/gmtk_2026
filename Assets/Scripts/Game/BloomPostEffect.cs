using UnityEngine;

/// <summary>Built-in 管线简易 Bloom：提取高亮 → 模糊 → 叠加。强度由 GameManager 按时间轴驱动。</summary>
[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public class BloomPostEffect : MonoBehaviour
{
    [Range(0f, 2.5f)] public float intensity;
    [Range(0.3f, 1f)] public float threshold = 0.72f;

    Material _mat;

    static readonly int BloomTexId = Shader.PropertyToID("_BloomTex");
    static readonly int ThresholdId = Shader.PropertyToID("_Threshold");
    static readonly int IntensityId = Shader.PropertyToID("_Intensity");

    Material Material
    {
        get
        {
            if (_mat == null)
            {
                var shader = Shader.Find("Hidden/GMTK/Bloom");
                if (shader != null) _mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }
            return _mat;
        }
    }

    public void SetBloom(float bloomIntensity, float bloomThreshold)
    {
        intensity = bloomIntensity;
        threshold = bloomThreshold;
    }

    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        var mat = Material;
        if (!SystemInfo.supportsImageEffects || mat == null || mat.shader == null ||
            !mat.shader.isSupported || intensity <= 0.001f)
        {
            Graphics.Blit(src, dest);
            return;
        }

        int w = Mathf.Max(1, src.width / 2);
        int h = Mathf.Max(1, src.height / 2);
        var fmt = src.format;
        var rtBright = RenderTexture.GetTemporary(w, h, 0, fmt);
        var rtBlurA = RenderTexture.GetTemporary(w, h, 0, fmt);
        var rtBlurB = RenderTexture.GetTemporary(w, h, 0, fmt);

        mat.SetFloat(ThresholdId, threshold);
        mat.SetFloat(IntensityId, intensity);

        Graphics.Blit(src, rtBright, mat, 0);
        Graphics.Blit(rtBright, rtBlurA, mat, 1);
        Graphics.Blit(rtBlurA, rtBlurB, mat, 2);

        mat.SetTexture(BloomTexId, rtBlurB);
        Graphics.Blit(src, dest, mat, 3);

        RenderTexture.ReleaseTemporary(rtBright);
        RenderTexture.ReleaseTemporary(rtBlurA);
        RenderTexture.ReleaseTemporary(rtBlurB);
    }

    void OnDestroy()
    {
        if (_mat != null) Destroy(_mat);
    }
}
