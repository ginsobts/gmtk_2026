using UnityEngine;

/// <summary>
/// 可视化可调的运行时参数（相机 / 玩家 / NPC）。
/// 资产放在 Resources/GameData/GameConfig.asset，开局读取；
/// 找不到资产时用内置默认值（与旧硬编码一致），所以缺资产也能正常跑。
/// 用菜单 GMTK/创建 GameConfig 资产 生成它，然后在 Inspector 里改，改完下次 Play 生效；
/// 也可在运行时用 F1 调试面板实时调并「保存到 GameConfig」。
/// </summary>
[CreateAssetMenu(menuName = "GMTK/Game Config", fileName = "GameConfig")]
public class GameConfig : ScriptableObject
{
    [Header("相机")]
    public float cameraFieldOfView = 55f;
    public float cameraTilt = 52f;                        // 俯角（绕 X）
    public Vector3 cameraOffset = new Vector3(0f, 13f, -10f);
    public float cameraFollowLerp = 8f;

    [Header("玩家")]
    public Vector3 playerStart = new Vector3(0f, 0f, -6f);
    public float playerScale = 0.47f;
    public float playerMoveSpeed = 5.5f;
    public float playerInteractRange = 2.6f;

    [Header("NPC")]
    public float npcScale = 1f;
    [Tooltip("所有 NPC 绕世界 Y 轴的默认朝向偏移；0 = 正对相机。出生点可单独覆盖。")]
    public float npcDefaultYaw = 0f;
    [Tooltip("为每个 NPC 加一点随机朝向抖动，让他们不那么整齐。0 = 关闭。")]
    public float npcYawRandom = 0f;

    [Header("NPC 随机生成范围（没有出生点时使用）")]
    public Vector2 spawnAreaX = new Vector2(-14f, 14f);
    public Vector2 spawnAreaZ = new Vector2(-13f, 13f);

    static GameConfig _instance;

    /// <summary>全局配置。资产缺失时返回一份内存默认，不写盘。</summary>
    public static GameConfig Instance
    {
        get
        {
            if (_instance == null) _instance = Resources.Load<GameConfig>("GameData/GameConfig");
            if (_instance == null)
            {
                _instance = CreateInstance<GameConfig>();
                _instance.name = "GameConfig (runtime default)";
            }
            return _instance;
        }
    }

    /// <summary>让编辑器工具能把新建/加载到的资产设为当前实例。</summary>
    public static void SetInstance(GameConfig cfg) { if (cfg != null) _instance = cfg; }
}
