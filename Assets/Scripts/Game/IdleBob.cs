using UnityEngine;

/// <summary>让立绘轻微上下浮动，制造“呼吸”感。挂在立绘的父级包装物上。</summary>
public class IdleBob : MonoBehaviour
{
    public float amplitude = 0.06f;
    public float speed = 1.8f;

    float _phase;
    Vector3 _baseLocal;

    void Start()
    {
        _phase = Random.value * Mathf.PI * 2f;
        _baseLocal = transform.localPosition;
    }

    void Update()
    {
        var p = _baseLocal;
        p.y += Mathf.Sin(Time.time * speed + _phase) * amplitude;
        transform.localPosition = p;
    }
}
