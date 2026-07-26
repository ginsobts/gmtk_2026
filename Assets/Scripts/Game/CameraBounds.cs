using UnityEngine;

/// <summary>
/// 相机跟随范围框（XZ 平面的矩形）。把这种物体放进场景，CameraRig 就只在这个框内跟随角色：
/// 角色走到框外时，镜头的「注视点」停在框边，于是靠边时镜头不再移动。
/// - 中心 = 物体自身位置（直接在 Scene 视图里拖动即可平移整个框）。
/// - 大小 = size（全宽 x / 全长 z）；可在 Inspector 改，或用自定义编辑器拖四条边来"画框"。
/// 场景里没有该物体时，CameraRig 回退到 GameConfig 里的默认框。
/// </summary>
public class CameraBounds : MonoBehaviour
{
    [Tooltip("框的全宽/全长（世界单位，XZ）。")]
    public Vector2 size = new Vector2(16f, 12f);

    public float MinX => transform.position.x - size.x * 0.5f;
    public float MaxX => transform.position.x + size.x * 0.5f;
    public float MinZ => transform.position.z - size.y * 0.5f;
    public float MaxZ => transform.position.z + size.y * 0.5f;

    /// <summary>把一个世界坐标的 XZ 夹进框内（Y 不变）。框太小则退化为中心点。</summary>
    public Vector3 Clamp(Vector3 world)
    {
        world.x = Mathf.Clamp(world.x, Mathf.Min(MinX, MaxX), Mathf.Max(MinX, MaxX));
        world.z = Mathf.Clamp(world.z, Mathf.Min(MinZ, MaxZ), Mathf.Max(MinZ, MaxZ));
        return world;
    }

    void OnDrawGizmos()
    {
        Vector3 c = new Vector3(transform.position.x, 0.05f, transform.position.z);
        Vector3 s = new Vector3(Mathf.Abs(size.x), 0.02f, Mathf.Abs(size.y));
        Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.9f);
        Gizmos.DrawWireCube(c, s);
        Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.08f);
        Gizmos.DrawCube(c, s);
    }
}
