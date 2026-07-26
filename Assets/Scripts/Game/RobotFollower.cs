using UnityEngine;

/// <summary>
/// 无交互的“小我”跟随机器人。
/// 与玩家拉开距离后直接朝玩家当前位置靠近；经过空气墙求解，避免穿过树木。
/// </summary>
public class RobotFollower : MonoBehaviour
{
    public Transform target;
    public SpriteRenderer body;
    public Sprite frontSprite;
    public Sprite backSprite;
    public Sprite sideSprite;
    public float followDistance = 1.1f;
    public float moveSpeed = 4.8f;

    public void Setup(Transform followTarget, SpriteRenderer renderer, Sprite[] frames)
    {
        target = followTarget;
        body = renderer;
        if (frames != null)
        {
            if (frames.Length > 0) frontSprite = frames[0];
            if (frames.Length > 1) backSprite = frames[1];
            if (frames.Length > 2) sideSprite = frames[2];
        }
    }

    void Update()
    {
        if (target == null || DebugControl.Frozen) return;

        Vector3 playerPos = target.position;
        playerPos.y = 0f;
        Vector3 here = transform.position;
        here.y = 0f;
        Vector3 toPlayer = playerPos - here;
        float distance = toPlayer.magnitude;
        if (distance <= followDistance) return;

        Vector3 before = transform.position;
        // 本帧最多走到停止距离边缘，不会贴进玩家棋子内部。
        float step = Mathf.Min(moveSpeed * Time.deltaTime, distance - followDistance);
        Vector3 next = here + toPlayer / distance * step;
        next.y = 0f;
        next = MapObstacles.Resolve(next, 0.28f);
        transform.position = next;
        UpdateFacing(next - before);
    }

    void UpdateFacing(Vector3 delta)
    {
        if (body == null || delta.sqrMagnitude < 0.000001f) return;
        if (Mathf.Abs(delta.z) >= Mathf.Abs(delta.x))
        {
            Sprite s = delta.z > 0f ? backSprite : frontSprite;
            if (s != null) body.sprite = s;
            body.flipX = false;
        }
        else
        {
            if (sideSprite != null) body.sprite = sideSprite;
            body.flipX = delta.x > 0f;
        }
    }
}
