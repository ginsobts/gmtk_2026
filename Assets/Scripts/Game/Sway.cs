using UnityEngine;

/// <summary>让植被卡片左右轻摆，制造风吹感。挂在卡片上（CameraFacingSprite 只改旋转，不影响位置）。</summary>
public class Sway : MonoBehaviour
{
    public float amplitude = 0.05f;
    public float speed = 1.1f;

    float _phase;
    Vector3 _baseLocal;

    void Start()
    {
        _phase = Random.value * Mathf.PI * 2f;
        _baseLocal = transform.localPosition;
        speed *= Random.Range(0.85f, 1.15f);
    }

    void Update()
    {
        if (DebugControl.Frozen) return;
        var p = _baseLocal;
        p.x += Mathf.Sin(Time.time * speed + _phase) * amplitude;
        transform.localPosition = p;
    }
}
