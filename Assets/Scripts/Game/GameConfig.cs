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
    public bool cameraOrthographic = true;                // 正交投影（策划 1.1：2.5D 正交）
    public float cameraOrthographicSize = 8.5f;           // 正交视野半高（策划：size 8.5）
    public float cameraFieldOfView = 55f;                 // 透视时用（cameraOrthographic=false 时生效）
    public float cameraTilt = 35f;                        // 俯角（绕 X，策划：x 旋转 35）
    public Vector3 cameraOffset = new Vector3(0f, 13f, -10f);
    public float cameraFollowLerp = 8f;

    [Header("玩家")]
    public Vector3 playerStart = new Vector3(0f, 0f, -6f);
    public float playerScale = 1.0f;
    public float playerMoveSpeed = 5.5f;
    public float playerInteractRange = 2.6f;

    [Header("空气墙（玩家可走范围，半宽；以世界原点为中心的方框）")]
    public float mapHalfX = 17.5f;   // 左右边界：x ∈ [-mapHalfX, mapHalfX]
    public float mapHalfZ = 17.5f;   // 前后边界：z ∈ [-mapHalfZ, mapHalfZ]

    [Header("相机跟随范围（镜头只在这块框内跟随角色；走到框外镜头停住）")]
    public bool cameraBoundsEnabled = true;              // 关掉=镜头无限跟随（旧行为）
    public Vector2 cameraBoundsCenter = new Vector2(0f, 0f);
    public Vector2 cameraBoundsSize = new Vector2(16f, 12f);   // 全宽/全长（世界单位）。场景里放 CameraBounds 物体可覆盖它并可视化拖框

    [Header("NPC")]
    public float npcScale = 1f;
    [Tooltip("所有 NPC 绕世界 Y 轴的默认朝向偏移；0 = 正对相机。出生点可单独覆盖。")]
    public float npcDefaultYaw = 0f;
    [Tooltip("为每个 NPC 加一点随机朝向抖动，让他们不那么整齐。0 = 关闭。")]
    public float npcYawRandom = 0f;

    [Header("NPC 随机生成范围（没有出生点时使用）")]
    public Vector2 spawnAreaX = new Vector2(-14f, 14f);
    public Vector2 spawnAreaZ = new Vector2(-13f, 13f);

    [Header("时间点（策划 1.2/1.3）")]
    public int dialogueTimePoints = 3;   // 每完成一次对话 +N
    public int photoTimePoints = 1;      // 每次拍照 +M
    public int deathThreshold = 90;      // 时间点到 X 触发死亡演出（超过第 3 stage 之后，策划 5.1）

    [Header("时间轴 stage（策划 1.3）")]
    // 3 个调查 stage（遛狗/狗丢/真面目，对齐魏大爷 base/s2/s3），死亡在 deathThreshold：stage2=30 / stage3=60
    public int[] stageThresholds = new int[] { 30, 60 };

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
