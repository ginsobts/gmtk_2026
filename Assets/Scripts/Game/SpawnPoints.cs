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
