using UnityEngine;

/// <summary>
/// 手摆的 NPC 出生点。把这种空物体放进场景，SpawnNpcs 会优先按它们摆 NPC，
/// 没有任何出生点时才回退到随机生成。
/// - 位置：直接在 Scene 视图里拖。
/// - 朝向：旋转该物体的 Y 轴（yaw）即可让对应 NPC 有独立朝向。
/// </summary>
public class NpcSpawnPoint : MonoBehaviour
{
    [Tooltip("是否始终正对相机（billboard）。取消勾选则用下面的朝向。")]
    public bool faceCamera = true;

    [Tooltip("额外朝向偏移（度，绕世界 Y）。留空则用物体自身的 Y 旋转。")]
    public float extraYaw = 0f;

    /// <summary>最终 yaw = 物体自身 Y 旋转 + extraYaw。</summary>
    public float ResolvedYaw => transform.eulerAngles.y + extraYaw;

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.9f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, 0.45f);
        // 朝向指示
        Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.9f);
        Vector3 dir = Quaternion.Euler(0f, ResolvedYaw, 0f) * Vector3.forward;
        Gizmos.DrawLine(transform.position + Vector3.up * 0.9f, transform.position + Vector3.up * 0.9f + dir * 1.2f);
    }
}

/// <summary>手摆的玩家出生点（可选）。存在则玩家从这里开始。</summary>
public class PlayerSpawnPoint : MonoBehaviour
{
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.4f, 1f, 0.5f, 0.9f);
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.9f, Vector3.one * 0.8f);
    }
}

/// <summary>
/// 手摆的树/灌木。用菜单「GMTK/树木：生成可调标记」一键铺一份默认布局，然后在 Scene 视图里拖动/增删。
/// 只要场景里存在任意一个 TreeMarker，运行时森林就【完全】按这些标记生成（否则用内置默认布局）。
/// - 位置：直接拖该物体（y 会被当作贴地）。
/// - kind：松树 / 灌木。scale：大小。obstacle + obstacleRadius：是否当空气墙及其半径。
/// </summary>
public class TreeMarker : MonoBehaviour
{
    public enum Kind { Tree, Bush }
    public Kind kind = Kind.Tree;
    [Min(0.05f)] public float scale = 1f;
    [Tooltip("排序层级，越大越靠前显示")] public int sorting = 3;
    [Tooltip("是否作为空气墙挡住玩家")] public bool obstacle = true;
    [Min(0f)] public float obstacleRadius = 1.0f;

    /// <summary>转成运行时布局定义（y 归零贴地）。</summary>
    public GameManager.TreeDef ToDef() => new GameManager.TreeDef
    {
        pos = new Vector3(transform.position.x, 0f, transform.position.z),
        isBush = kind == Kind.Bush,
        scale = scale,
        sorting = sorting,
        obstacle = obstacle,
        radius = obstacleRadius,
    };

    void OnDrawGizmos()
    {
        Vector3 basePos = new Vector3(transform.position.x, 0f, transform.position.z);
        Gizmos.color = kind == Kind.Bush ? new Color(0.6f, 0.9f, 0.4f, 0.95f) : new Color(0.2f, 0.7f, 0.3f, 0.95f);
        Gizmos.DrawLine(basePos, basePos + Vector3.up * 1.4f * scale);
        Gizmos.DrawWireSphere(basePos + Vector3.up * 1.4f * scale, 0.5f * scale);
        if (obstacle && obstacleRadius > 0f)
        {
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(basePos, obstacleRadius);
        }
    }
}
