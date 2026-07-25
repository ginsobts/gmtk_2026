using UnityEngine;

/// <summary>
/// 玩家控制器：WASD/方向键在 XZ 平面移动；靠近 NPC 按 E 发起【纯对话】。
/// 拍照与相册是独立系统，由 GameManager 的热键 / HUD 按钮触发。
/// 移动时按行进方向切换 正/侧/背 三视图棋子（2.5D 朝向），静止时保持上一次朝向。
/// </summary>
public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5.5f;
    public float interactRange = 2.6f;
    const float MapHalf = 17.5f;

    // 方向棋子：由 GameManager 生成主角时注入（缺某张图则回退到背面 back）。
    [Header("Directional pieces (2.5D facing)")]
    public SpriteRenderer body;
    public Sprite backSprite;   // 背：远离相机（+Z / W）
    public Sprite frontSprite;  // 正：朝向相机（-Z / S）
    public Sprite sideSprite;   // 侧：美术默认朝左（-X / A），向右走时水平翻转

    Npc _nearest;

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.State != GameState.Playing) return;

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        HandleMovement(h, v);
        UpdateFacing(h, v);
        FindNearestNpc();

        if (_nearest != null)
        {
            if (Input.GetKeyDown(KeyCode.E)) gm.BeginDialogue(_nearest);
            else if (Input.GetKeyDown(KeyCode.Q)) gm.ViewCharacterPhotos(_nearest);
            else if (Input.GetKeyDown(KeyCode.F)) gm.ToggleMark(_nearest);
        }
    }

    void HandleMovement(float h, float v)
    {
        Vector3 dir = new Vector3(h, 0f, v);
        if (dir.sqrMagnitude > 1f) dir.Normalize();

        Vector3 pos = transform.position + dir * moveSpeed * Time.deltaTime;
        pos.x = Mathf.Clamp(pos.x, -MapHalf, MapHalf);
        pos.z = Mathf.Clamp(pos.z, -MapHalf, MapHalf);
        transform.position = pos;
    }

    /// <summary>按行进方向切换棋子朝向：纵向为主取 背/正，横向为主取 侧（默认朝左，向右翻转）。静止时保持。</summary>
    void UpdateFacing(float h, float v)
    {
        if (body == null) return;
        if (Mathf.Abs(h) < 0.01f && Mathf.Abs(v) < 0.01f) return; // 静止：保持上一次朝向

        if (Mathf.Abs(v) >= Mathf.Abs(h))
        {
            // 纵向为主：+Z（远离相机）=背，-Z（朝向相机）=正
            var s = v > 0f ? backSprite : frontSprite;
            if (s != null) { body.sprite = s; body.flipX = false; }
        }
        else
        {
            // 横向为主：侧面。侧面美术默认朝左，向右走（h>0）时水平翻转
            if (sideSprite != null) { body.sprite = sideSprite; body.flipX = h > 0f; }
        }
    }

    void FindNearestNpc()
    {
        Npc best = null;
        float bestDist = interactRange;
        foreach (var npc in GameManager.Instance.Npcs)
        {
            if (npc == null) continue;
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
