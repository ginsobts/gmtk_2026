using UnityEngine;

/// <summary>
/// 死亡演出里的追逐怪物（策划 5.2.3）：每帧朝玩家移动，进入 killRange 即触发死亡。
/// 仅在 GameState.Death 下活动；DebugControl.Frozen 时暂停（便于 Scene 视图摆弄）。
/// </summary>
public class DeathMonster : MonoBehaviour
{
    public Transform target;
    public float speed = 5.8f;
    public float killRange = 1.3f;

    bool _done;

    void Update()
    {
        if (_done || DebugControl.Frozen) return;
        var gm = GameManager.Instance;
        if (gm == null || gm.State != GameState.Death || target == null) return;

        Vector3 to = target.position - transform.position;
        to.y = 0f;
        float d = to.magnitude;
        if (d > 0.001f)
            transform.position += to.normalized * speed * Time.deltaTime;

        if (d <= killRange)
        {
            _done = true;
            gm.PlayerCaughtByMonster();
        }
    }
}
