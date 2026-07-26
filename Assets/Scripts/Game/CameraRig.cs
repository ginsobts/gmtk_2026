using UnityEngine;

/// <summary>
/// 2.5D 相机：固定俯角，只跟随目标在 XZ 平面平移，不旋转。
/// </summary>
public class CameraRig : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 13f, -10f);
    public float followLerp = 8f;

    // 相机跟随范围：优先用场景里的 CameraBounds 物体；没有则用下面这组默认值（来自 GameConfig）。
    public CameraBounds bounds;
    public bool useBounds;
    public float minX, maxX, minZ, maxZ;

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
        if (DebugControl.Frozen) return;
        if (target == null) return;

        // 把「注视点」夹进跟随范围：角色走到框外时注视点停在框边，镜头随之停住。
        Vector3 focus = target.position;
        if (bounds != null)
            focus = bounds.Clamp(focus);
        else if (useBounds)
        {
            focus.x = Mathf.Clamp(focus.x, Mathf.Min(minX, maxX), Mathf.Max(minX, maxX));
            focus.z = Mathf.Clamp(focus.z, Mathf.Min(minZ, maxZ), Mathf.Max(minZ, maxZ));
        }

        Vector3 desired = focus + offset;
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
