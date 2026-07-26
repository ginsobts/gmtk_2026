using UnityEngine;

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
