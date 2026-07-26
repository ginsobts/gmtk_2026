using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全局地图障碍（空气墙）：森林 / 树丛 / 建筑等不可穿越区域，用一组圆表示（XZ 平面）。
/// 玩家移动时被推出这些圆 —— 于是"只有空地能走，树木/建筑就是空气墙"。
/// 每次重建场景时先 Clear()，再由 GameManager 在摆放森林/道具/树丛时逐个 Add()。
/// 只影响玩家移动，不影响 NPC 查找与交互。
/// </summary>
public static class MapObstacles
{
    public struct Circle { public Vector2 center; public float radius; }

    static readonly List<Circle> _circles = new List<Circle>();

    public static IReadOnlyList<Circle> Circles => _circles;

    public static void Clear() => _circles.Clear();

    public static void Add(float x, float z, float radius)
        => _circles.Add(new Circle { center = new Vector2(x, z), radius = radius });

    public static void Add(Vector3 worldPos, float radius) => Add(worldPos.x, worldPos.z, radius);

    /// <summary>把一个 XZ 位置推出所有障碍圆（含移动体自身半径 agentRadius）。多次迭代以处理夹缝。</summary>
    public static Vector3 Resolve(Vector3 pos, float agentRadius)
    {
        if (_circles.Count == 0) return pos;
        for (int iter = 0; iter < 3; iter++)
        {
            bool moved = false;
            for (int i = 0; i < _circles.Count; i++)
            {
                var c = _circles[i];
                Vector2 p = new Vector2(pos.x, pos.z);
                Vector2 d = p - c.center;
                float min = c.radius + agentRadius;
                float dist = d.magnitude;
                if (dist < min)
                {
                    Vector2 n = dist > 1e-4f ? d / dist : new Vector2(1f, 0f);
                    Vector2 np = c.center + n * min;
                    pos.x = np.x; pos.z = np.y;
                    moved = true;
                }
            }
            if (!moved) break;
        }
        return pos;
    }
}

/// <summary>
/// 在 Scene 视图里把所有空气墙障碍圈画成红色，便于可视化调走位（仅编辑器/开发包挂载）。
/// 用 OnDrawGizmosSelected：默认不画，只有在 Hierarchy 里选中 GameManager 时才显示，避免平时挡视线。
/// （注意：真正打包出来的游戏永远不会出现这些圈。）
/// </summary>
public class MapObstacleGizmo : MonoBehaviour
{
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.15f, 0.8f);
        foreach (var c in MapObstacles.Circles)
            DrawCircleXZ(new Vector3(c.center.x, 0.05f, c.center.y), c.radius);
    }

    static void DrawCircleXZ(Vector3 center, float radius, int seg = 24)
    {
        Vector3 prev = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2f;
            Vector3 cur = center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            Gizmos.DrawLine(prev, cur);
            prev = cur;
        }
    }
}
