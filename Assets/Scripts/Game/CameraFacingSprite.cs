using UnityEngine;

/// <summary>
/// 让世界里的 2D 卡片朝向相机。默认始终正对相机（billboard），
/// 也支持每个对象一个 yawOffset（绕世界 Y 轴偏转），从而让不同 NPC 有各自朝向、
/// 不再整齐地全部正对镜头。faceCamera 取消后则用固定世界朝向（大角度会显得偏薄，属正常）。
/// DebugControl.Frozen 为真时暂停转向，便于在 Scene 视图里手动摆弄。
/// </summary>
public class CameraFacingSprite : MonoBehaviour
{
    public bool faceCamera = true;
    public float yawOffset = 0f;

    Camera _mainCamera;

    void LateUpdate()
    {
        if (DebugControl.Frozen) return;
        if (_mainCamera == null) _mainCamera = Camera.main;

        Quaternion baseRot;
        if (faceCamera && _mainCamera != null)
            baseRot = _mainCamera.transform.rotation;
        else if (_mainCamera != null)
            // 保留相机的俯角，只让卡片按世界 Y 独立朝向（避免完全躺平）
            baseRot = Quaternion.Euler(_mainCamera.transform.eulerAngles.x, 0f, 0f);
        else
            baseRot = Quaternion.identity;

        transform.rotation = Quaternion.AngleAxis(yawOffset, Vector3.up) * baseRot;
    }
}
