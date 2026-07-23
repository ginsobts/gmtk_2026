using UnityEngine;

/// <summary>
/// 让世界里的 2D 卡片始终与相机平行。
/// 这样固定俯视相机下的角色、建筑不会被看成一条薄片。
/// </summary>
public class CameraFacingSprite : MonoBehaviour
{
    Camera _mainCamera;

    void LateUpdate()
    {
        if (_mainCamera == null) _mainCamera = Camera.main;
        if (_mainCamera != null)
            transform.rotation = _mainCamera.transform.rotation;
    }
}
