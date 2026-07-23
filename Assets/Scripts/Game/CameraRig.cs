using UnityEngine;

/// <summary>
/// 2.5D 相机：固定俯角，只跟随目标在 XZ 平面平移，不旋转。
/// </summary>
public class CameraRig : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 13f, -10f);
    public float followLerp = 8f;

    float _shakeTime;
    float _shakeDur;
    float _shakeMag;

    /// <summary>触发一次震屏。</summary>
    public void Shake(float duration = 0.18f, float magnitude = 0.25f)
    {
        _shakeDur = duration;
        _shakeTime = duration;
        _shakeMag = magnitude;
    }

    void LateUpdate()
    {
        if (target == null) return;
        Vector3 desired = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desired, followLerp * Time.deltaTime);

        if (_shakeTime > 0f)
        {
            _shakeTime -= Time.unscaledDeltaTime;
            float k = _shakeDur > 0f ? Mathf.Clamp01(_shakeTime / _shakeDur) : 0f;
            float amt = _shakeMag * k * k;
            transform.position += new Vector3(
                (Random.value * 2f - 1f) * amt,
                (Random.value * 2f - 1f) * amt,
                0f);
        }
    }
}
