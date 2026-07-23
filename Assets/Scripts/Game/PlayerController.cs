using UnityEngine;

/// <summary>
/// 玩家控制器：WASD/方向键在 XZ 平面移动；靠近 NPC 按 E 发起【纯对话】。
/// 拍照与相册是独立系统，由 GameManager 的热键 / HUD 按钮触发。
/// </summary>
public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5.5f;
    public float interactRange = 2.6f;
    const float MapHalf = 17.5f;

    Npc _nearest;

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.State != GameState.Playing) return;

        HandleMovement();
        FindNearestNpc();

        if (_nearest != null)
        {
            if (Input.GetKeyDown(KeyCode.E)) gm.BeginDialogue(_nearest);
            else if (Input.GetKeyDown(KeyCode.Q)) gm.ViewCharacterPhotos(_nearest);
            else if (Input.GetKeyDown(KeyCode.F)) gm.Accuse(_nearest);
        }
    }

    void HandleMovement()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 dir = new Vector3(h, 0f, v);
        if (dir.sqrMagnitude > 1f) dir.Normalize();

        Vector3 pos = transform.position + dir * moveSpeed * Time.deltaTime;
        pos.x = Mathf.Clamp(pos.x, -MapHalf, MapHalf);
        pos.z = Mathf.Clamp(pos.z, -MapHalf, MapHalf);
        transform.position = pos;
    }

    void FindNearestNpc()
    {
        Npc best = null;
        float bestDist = interactRange;
        foreach (var npc in GameManager.Instance.Npcs)
        {
            if (npc == null || npc.caught) continue;
            float d = Vector3.Distance(transform.position, npc.transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = npc;
            }
        }

        _nearest = best;
        GameManager.Instance.UpdateNearest(_nearest);
    }
}
