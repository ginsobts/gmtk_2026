using UnityEngine;

/// <summary>手摆的玩家出生点（可选）。存在则玩家从这里开始。</summary>
public class PlayerSpawnPoint : MonoBehaviour
{
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.4f, 1f, 0.5f, 0.9f);
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.9f, Vector3.one * 0.8f);
    }
}
