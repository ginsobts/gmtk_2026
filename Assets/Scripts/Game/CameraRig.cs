using UnityEngine;

/// <summary>
/// 2.5D 相机：固定俯角，只跟随目标在 XZ 平面平移，不旋转。
/// </summary>
public class CameraRig : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 13f, -10f);
    public float followLerp = 8f;

    void LateUpdate()
    {
        if (target == null) return;
        Vector3 desired = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desired, followLerp * Time.deltaTime);
    }
}
