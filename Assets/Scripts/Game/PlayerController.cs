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
    // 空气墙半宽（由 GameManager 从 GameConfig 注入；可在 Inspector / F1 面板调）
    public float mapHalfX = 17.5f;
    public float mapHalfZ = 17.5f;
    // 玩家碰撞半径：与森林/道具障碍（MapObstacles）求解时使用
    public float radius = 0.5f;

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
        if (gm == null) return;
        bool playing = gm.State == GameState.Playing;
        // 死亡演出中玩家仍可移动逃跑，但不能交互
        if (!playing && gm.State != GameState.Death) return;

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        HandleMovement(h, v);
        UpdateFacing(h, v);

        if (!playing) return;   // Death：只能逃，不做最近 NPC 查找 / 交互
        FindNearestNpc();
        // 交互热键 E/Q/F 统一在 GameManager.Update 的 Playing 分支处理，
        // 避免与相册等状态在同一帧内互相触发（例如按 Q 关闭又被立刻重新打开）。
    }

    void HandleMovement(float h, float v)
    {
        Vector3 dir = new Vector3(h, 0f, v);
        if (dir.sqrMagnitude > 1f) dir.Normalize();

        Vector3 pos = transform.position + dir * moveSpeed * Time.deltaTime;
        pos.x = Mathf.Clamp(pos.x, -mapHalfX, mapHalfX);
        pos.z = Mathf.Clamp(pos.z, -mapHalfZ, mapHalfZ);
        // 森林/树丛/建筑作为空气墙：把玩家推出障碍圆（可沿墙滑动），再夹回外框
        pos = MapObstacles.Resolve(pos, radius);
        pos.x = Mathf.Clamp(pos.x, -mapHalfX, mapHalfX);
        pos.z = Mathf.Clamp(pos.z, -mapHalfZ, mapHalfZ);
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
