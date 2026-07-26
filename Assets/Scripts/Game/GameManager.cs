using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public enum GameState { MainMenu, Briefing, Tutorial, Playing, Dialogue, RobotReward, Camera, Album, MarkList, Result, Death }

/// <summary>相册里的一张照片：截图 + 拍摄时处于取景框内的 NPC 名单。</summary>
public class PhotoEntry
{
    public Texture2D image;
    public List<Npc> framed = new List<Npc>();
}

/// <summary>
/// 游戏总控。程序化搭建场景，管理状态机、时间轴、拍照截图、相册指认与胜负。
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("关卡参数")]
    public int imposterCount = 0;   // 本局炼化人数量：StartRound 按 characters 表 kind!=Normal 统计

    public GameState State { get; private set; } = GameState.MainMenu;
    /// <summary>Boss 死亡演出进行中（场景染暗/染红）。</summary>
    public bool IsDeathPerforming => _deathPerforming;
    /// <summary>Boss 阶段 NPC/植物统一色调。</summary>
    public static Color BossPhaseTint => FoliageTintDeath;
    public bool ShowNpcLabels { get; private set; }   // 是否在 NPC 头顶显示名字/职位（右上角按钮切换，默认关）
    bool _creditsOpen;
    bool _tutorialSeen;    // 世界观介绍后只演示一次新手引导
    int _tutorialStep;
    public List<Npc> Npcs { get; private set; } = new List<Npc>();
    public UIManager UI { get; private set; }
    public List<PhotoEntry> Album { get; private set; } = new List<PhotoEntry>();

    // 时间轴（合并 wxc 数据驱动 phase）：对话/拍照累加，跨 phases.txt 阈值推进阶段（策划 1.3）
    public int TimelineValue { get; private set; }
    public int CurrentPhase { get; private set; } = 1;
    Light _sun;
    Material _groundMat;
    readonly List<SpriteRenderer> _foliageRenderers = new List<SpriteRenderer>();
    ParticleSystem _ambientDust;
    Material _dustMaterial;
    BloomPostEffect _bloom;
    GameObject _monster;       // 死亡演出追逐怪物
    bool _deathPerforming;     // 死亡演出幂等标记（避免重复进入）
    bool _submitPending;   // 指认列表里“提交”确认弹窗是否打开

    Transform _player;
    Transform _npcRoot;
    Camera _mainCamera;

    /// <summary>供 NPC（变瘪人）检测玩家接触。</summary>
    public Transform PlayerTransform => _player;

    // 靠近的角色（探索态）
    Npc _nearestNpc;

    // 对话态
    Npc _dialogueNpc;
    DialogueLine[] _dialogueLines;
    int _dialogueIndex;
    bool _dialogueCompleted;
    string _dialogueId;        // 当前这组台词的 id（用于查分支 choices）
    bool _awaitingChoice;      // 台词播完、正在等玩家选分支

    // 豆包人分支奖励：“小我”机器人（仅本局，纯跟随、无交互）
    bool _robotRewardPending;
    bool _hasRobot;
    GameObject _robotCompanion;

    // 取景态
    readonly List<Npc> _framed = new List<Npc>();
    bool _capturing;

    void Awake()
    {
        Instance = this;
        CleanupExistingScene();
        BuildEnvironment();
        BuildPlayerAndCamera();

        gameObject.AddComponent<AudioManager>();

        UI = gameObject.AddComponent<UIManager>();
        UI.Build();

        EnterMainMenu();
    }

    // ---------------- 主菜单 ----------------

    public void EnterMainMenu()
    {
        State = GameState.MainMenu;
        _creditsOpen = false;
        UI.HideAllPanels();
        UI.SetHudVisible(false);
        UI.ShowMainMenu();
        AudioManager.Instance?.PlayBgm("menu");
    }

    /// <summary>点“开始游戏”：先展示目标说明，玩家确认后才进入场景。</summary>
    public void StartGame()
    {
        UI.HideMainMenu();
        State = GameState.Briefing;
        UI.SetHudVisible(false);
        UI.ShowBriefing();
    }

    /// <summary>目标说明看完点“确认”：真正开始一局。</summary>
    public void ConfirmBriefing()
    {
        if (State != GameState.Briefing) return;
        UI.HideBriefing();
        StartRound();
        // 进入场景后，第一次分三步高亮教学（再玩一次不再重复）
        if (!_tutorialSeen) BeginTutorial();
    }

    /// <summary>进入场景后开始分步高亮引导：第1步场景/第2步时间/第3步指认列表。</summary>
    void BeginTutorial()
    {
        _tutorialStep = 0;
        State = GameState.Tutorial;
        UI.SetInteractPrompt(null);
        UI.ShowTutorial(_tutorialStep);
    }

    /// <summary>点“继续”或按 空格/回车 进入下一步；三步走完结束引导，回到正常游玩。</summary>
    public void TutorialNext()
    {
        if (State != GameState.Tutorial) return;
        _tutorialStep++;
        if (_tutorialStep >= 3)
        {
            _tutorialSeen = true;
            UI.HideTutorial();
            State = GameState.Playing;
            return;
        }
        UI.ShowTutorial(_tutorialStep);
    }

    public void OpenCredits()
    {
        if (State != GameState.MainMenu) return;
        _creditsOpen = true;
        UI.ShowCredits();
    }

    public void CloseCredits()
    {
        _creditsOpen = false;
        UI.HideCredits();
    }

    /// <summary>切换中英文（本地化事件会自动重刷 UI）。</summary>
    public void ToggleLanguage() => Loc.Toggle();

    /// <summary>UIManager 在语言切换后回调，用于刷新动态文案。</summary>
    public void OnLanguageChanged()
    {
        if (State == GameState.Playing || State == GameState.Camera) RefreshHud();
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------------- 场景搭建（占位） ----------------

    void CleanupExistingScene()
    {
        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            Destroy(cam.gameObject);
        foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            Destroy(light.gameObject);
    }

    void BuildEnvironment()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.rotation = Quaternion.Euler(0f, 45f, 0f);   // 策划 S2：地面绕 Y 轴转 45°
        ground.transform.localScale = new Vector3(4f, 1f, 4f);
        var groundRenderer = ground.GetComponent<MeshRenderer>();
        var groundMaterial = new Material(Shader.Find("Standard"));
        groundMaterial.mainTexture = GeneratedArt.GroundTexture;
        groundMaterial.mainTextureScale = new Vector2(2f, 2f);
        groundMaterial.color = Color.white;
        groundMaterial.SetFloat("_Glossiness", 0.12f);
        groundMaterial.SetFloat("_Metallic", 0f);
        groundRenderer.material = groundMaterial;
        groundRenderer.receiveShadows = true;
        groundRenderer.shadowCastingMode = ShadowCastingMode.Off;
        _groundMat = groundMaterial;

        var lightGO = new GameObject("Sun");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.72f;
        light.shadowBias = 0.05f;
        light.shadowNormalBias = 0.4f;
        lightGO.transform.rotation = Quaternion.Euler(SunEulerMorning);
        RenderSettings.ambientLight = new Color(0.42f, 0.42f, 0.46f);
        _sun = light;
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowDistance = 80f;

        _npcRoot = new GameObject("NPCs").transform;
        MapObstacles.Clear();   // 重建场景：清空上一局的空气墙，随后由森林/道具/树丛重新登记
        // 参考图是「纯森林空地」，没有建筑/长椅/垃圾桶/健身器材，故先不摆城镇道具。
        // 若之后想加回，取消下一行注释即可（会连同空气墙一起登记）。
        // BuildTownProps();
        BuildForestBoundary();
        BuildAmbientParticles();
    }

    void BuildAmbientParticles()
    {
        var go = new GameObject("Ambient Dust");
        go.transform.position = new Vector3(0f, 3f, 0f);
        _ambientDust = go.AddComponent<ParticleSystem>();

        var main = _ambientDust.main;
        main.loop = true;
        main.startLifetime = 9f;
        main.startSpeed = 0.15f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.24f);
        main.startColor = new Color(0.92f, 0.90f, 0.86f, 0.42f);
        main.maxParticles = 120;
        main.gravityModifier = -0.01f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = _ambientDust.emission;
        emission.rateOverTime = 14f;

        var shape = _ambientDust.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(34f, 6f, 34f);

        var vel = _ambientDust.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
        vel.y = new ParticleSystem.MinMaxCurve(-0.05f, 0.08f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);

        var col = _ambientDust.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        var shader = Shader.Find("GMTK/HDRAdditiveParticle")
                     ?? Shader.Find("Mobile/Particles/Additive")
                     ?? Shader.Find("Legacy Shaders/Particles/Additive");
        if (shader != null && shader.isSupported)
        {
            _dustMaterial = new Material(shader);
            _dustMaterial.mainTexture = GeneratedArt.SoftDotSprite.texture;
            if (_dustMaterial.HasProperty("_HdrIntensity"))
                _dustMaterial.SetFloat("_HdrIntensity", DustHdrIntensityMorning);
            renderer.material = _dustMaterial;
        }
        else
        {
            // 构建时 Shader 被剥离/设备不支持时，宁可隐藏灰尘，也不能让启动流程在 UI 创建前中断。
            Debug.LogWarning("Ambient Dust: 没有可用的粒子 Shader，已安全关闭粒子渲染。");
            renderer.enabled = false;
        }
        renderer.sortingOrder = 6;
        _ambientDust.Play();
    }

    void BuildTownProps()
    {
        var propsRoot = new GameObject("Town Props").transform;

        // 新场景素材（Art/Props/*），缺图时回退到旧图集，保证任何情况下都不空
        Sprite tree = GeneratedArt.PropFileSprite("tree") ?? GeneratedArt.GetTownPropSprite(1);
        Sprite chair = GeneratedArt.PropFileSprite("chair") ?? GeneratedArt.GetTownPropSprite(3);
        Sprite trash = GeneratedArt.PropFileSprite("trashcan");
        Sprite gym = GeneratedArt.PropFileSprite("gym");

        // 道具同时登记为空气墙障碍：建筑大、树中、长椅/垃圾桶/器材小
        BuildProp(propsRoot, "Corner Shop", GeneratedArt.GetTownPropSprite(0), new Vector3(-14f, 0f, 12f), 1.15f, 2, false, 1.7f);
        BuildProp(propsRoot, "Tree North", tree, new Vector3(11f, 0f, 13f), 1.15f, 2, true, 0.9f);
        BuildProp(propsRoot, "Tree West", tree, new Vector3(-15f, 0f, -8f), 0.95f, 2, true, 0.9f);
        BuildProp(propsRoot, "Tree East", tree, new Vector3(15f, 0f, -8f), 0.95f, 2, true, 0.9f);
        BuildProp(propsRoot, "Park Bench", chair, new Vector3(11f, 0f, 5f), 0.9f, 2, false, 0.8f);
        BuildProp(propsRoot, "Bench West", chair, new Vector3(-10f, 0f, -12f), 0.8f, 2, false, 0.8f);

        // 新增装饰：垃圾桶 / 健身器材（有新素材才摆，缺则跳过）
        if (trash != null)
        {
            BuildProp(propsRoot, "Trash Can A", trash, new Vector3(-7.5f, 0f, 4f), 0.7f, 2, false, 0.5f);
            BuildProp(propsRoot, "Trash Can B", trash, new Vector3(8.5f, 0f, -3f), 0.7f, 2, false, 0.5f);
        }
        if (gym != null)
        {
            BuildProp(propsRoot, "Gym Gear A", gym, new Vector3(-4f, 0f, 9f), 0.95f, 2, false, 0.7f);
            BuildProp(propsRoot, "Gym Gear B", gym, new Vector3(3.5f, 0f, -10f), 0.95f, 2, false, 0.7f);
        }
    }

    /// <summary>摆一张卡片道具，并把它登记为空气墙障碍（obstacleRadius&gt;0 时）。</summary>
    void BuildProp(Transform parent, string name, Sprite sprite, Vector3 position, float scale, int sortingOrder, bool sway, float obstacleRadius)
    {
        BuildCard(parent, name, sprite, position, scale, sortingOrder, sway);
        if (obstacleRadius > 0f) MapObstacles.Add(position, obstacleRadius);
    }

    /// <summary>一棵树/灌木的布局定义。运行时生成 + 编辑器「生成可拖动标记」共用这一份数据结构。</summary>
    public struct TreeDef
    {
        public Vector3 pos;
        public bool isBush;    // true=灌木(bush.png)；false=松树(tree.png)
        public float scale;
        public int sorting;    // 排序层级（外圈/远处更小，画在后面）
        public bool obstacle;  // 是否登记为空气墙（挡玩家）
        public float radius;   // 空气墙半径
    }

    void BuildForestBoundary()
    {
        var forestRoot = new GameObject("Forest Boundary").transform;

        // 松树/灌木一律用新场景素材（浅绿），缺图才回退旧图集。旧深绿占位密林已弃用。
        Sprite bush = GeneratedArt.PropFileSprite("bush") ?? GeneratedArt.GetForestSprite(1);
        Sprite tree = GeneratedArt.PropFileSprite("tree") ?? GeneratedArt.GetForestSprite(3);

        // 若场景里手摆了 TreeMarker（用 GMTK 菜单一键生成再拖动），就完全按标记来；否则用内置默认布局。
        var markers = Object.FindObjectsByType<TreeMarker>(FindObjectsSortMode.None);
        if (markers != null && markers.Length > 0)
        {
            for (int i = 0; i < markers.Length; i++)
                BuildOneTree(forestRoot, "Tree " + i, markers[i].ToDef(), tree, bush);
            return;
        }

        int n = 0;
        foreach (var d in DefaultTreeLayout())
            BuildOneTree(forestRoot, "Tree " + (n++), d, tree, bush);
    }

    /// <summary>按一个 TreeDef 生成一张树/灌木卡片，并按需登记空气墙。</summary>
    void BuildOneTree(Transform root, string name, TreeDef d, Sprite tree, Sprite bush)
    {
        BuildCard(root, name, d.isBush ? bush : tree, d.pos, d.scale, d.sorting, sway: true, foliageTint: true);
        if (d.obstacle && d.radius > 0f) MapObstacles.Add(d.pos, d.radius);
    }

    /// <summary>
    /// 内置默认树木布局（浅绿新素材）：外圈围边(纯装饰) + 内圈(空气墙) + 四角 + 西侧密林树丛。
    /// 运行时与「GMTK/树木：生成可调标记」编辑器菜单共用这份唯一数据，保证两边一致。
    /// </summary>
    public static System.Collections.Generic.List<TreeDef> DefaultTreeLayout()
    {
        var list = new System.Collections.Generic.List<TreeDef>();
        void Add(Vector3 p, bool bush, float scale, int sorting, bool obstacle, float radius)
            => list.Add(new TreeDef { pos = p, isBush = bush, scale = scale, sorting = sorting, obstacle = obstacle, radius = radius });

        // 外圈：浅绿松树两排交错围边（都在 mapHalf 之外，纯装饰，不做空气墙）
        for (int i = -18; i <= 18; i += 3)
        {
            float s = 1.15f + (Mathf.Abs(i) % 6 == 0 ? 0.15f : 0f);
            Add(new Vector3(i, 0f, 19f), false, s, 1, false, 0f);
            Add(new Vector3(i + 1.5f, 0f, 20.8f), false, s * 0.92f, 0, false, 0f);
            Add(new Vector3(i, 0f, -19f), false, s, 1, false, 0f);
            Add(new Vector3(i + 1.5f, 0f, -20.8f), false, s * 0.92f, 0, false, 0f);
            Add(new Vector3(19f, 0f, i), false, 1.15f, 1, false, 0f);
            Add(new Vector3(20.8f, 0f, i + 1.5f), false, 1.06f, 0, false, 0f);
            Add(new Vector3(-19f, 0f, i), false, 1.15f, 1, false, 0f);
            Add(new Vector3(-20.8f, 0f, i + 1.5f), false, 1.06f, 0, false, 0f);
        }

        // 内圈：北松树、南/东/西灌木（作为空气墙，把玩家挡在边缘外）
        for (int i = -15; i <= 15; i += 5)
        {
            Add(new Vector3(i, 0f, 15.6f), false, 0.95f, 2, true, 1.0f);
            Add(new Vector3(i, 0f, -15.6f), true, 0.75f, 2, true, 0.9f);
        }
        for (int i = -10; i <= 10; i += 5)
        {
            Add(new Vector3(15.6f, 0f, i), true, 0.7f, 2, true, 0.9f);
            Add(new Vector3(-15.6f, 0f, i), true, 0.7f, 2, true, 0.9f);
        }

        // 四角松树
        Add(new Vector3(-16f, 0f, 14f), false, 1.05f, 3, true, 1.0f);
        Add(new Vector3(16f, 0f, 14f), false, 1.05f, 3, true, 1.0f);
        Add(new Vector3(-16f, 0f, -14f), false, 1.05f, 3, true, 1.0f);
        Add(new Vector3(16f, 0f, -14f), false, 1.05f, 3, true, 1.0f);

        // 西侧 / 左下密林树丛（对照参考图；每丛随机散布并互相重叠，拼成实心空气墙）
        var groves = new (float x, float z, float r)[]
        {
            (-9f,  -1f, 2.6f), (-10f, -6f, 2.8f), (-6f,  -7f, 2.5f),
            (-10f,-10f, 2.2f), (-8f,  -3f, 2.4f), (-6f,   1f, 2.2f),
            (-9f,   3f, 2.0f), (-13f,  7f, 2.2f), (-13f, 11f, 2.2f),
        };
        foreach (var g in groves)
        {
            var center = new Vector3(g.x, 0f, g.z);
            var rng = new System.Random($"Grove_{g.x}_{g.z}".GetHashCode());
            int count = Mathf.Max(6, Mathf.RoundToInt(g.r * g.r * 1.4f));
            for (int i = 0; i < count; i++)
            {
                double a = rng.NextDouble() * System.Math.PI * 2.0;
                double rr = g.r * System.Math.Sqrt(rng.NextDouble());
                var p = center + new Vector3((float)(System.Math.Cos(a) * rr), 0f, (float)(System.Math.Sin(a) * rr));
                bool isTree = rng.NextDouble() > 0.4;
                Add(p, !isTree, isTree ? 1.0f : 0.72f, isTree ? 3 : 2, true, isTree ? 1.0f : 0.85f);
            }
        }

        return list;
    }

    void BuildCard(Transform parent, string name, Sprite sprite, Vector3 position, float scale, int sortingOrder, bool sway = false, bool foliageTint = false)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.position = position;

        var card = new GameObject("Card");
        card.transform.SetParent(root.transform, false);
        card.transform.localScale = Vector3.one * scale;
        var renderer = card.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
        card.AddComponent<CameraFacingSprite>();
        if (sway) card.AddComponent<Sway>();
        AttachGroundShadow(root.transform, renderer);
        if (foliageTint) _foliageRenderers.Add(renderer);
    }

    void BuildPlayerAndCamera()
    {
        var cfg = GameConfig.Instance;

        var playerGO = BuildPerson("Player", GeneratedArt.PlayerSprite, cfg.playerScale, out var playerBody, castGroundShadow: true);
        // 场景里若手摆了玩家出生点则用它，否则用配置里的起点
        var playerSpawn = Object.FindFirstObjectByType<PlayerSpawnPoint>();
        playerGO.transform.position = playerSpawn != null ? playerSpawn.transform.position : cfg.playerStart;
        var pc = playerGO.AddComponent<PlayerController>();
        pc.moveSpeed = cfg.playerMoveSpeed;
        pc.interactRange = cfg.playerInteractRange;
        pc.mapHalfX = cfg.mapHalfX;
        pc.mapHalfZ = cfg.mapHalfZ;
        // 2.5D 方向棋子：默认背面(=PlayerSprite)，正/侧缺图时回退到背面
        pc.body = playerBody;
        pc.backSprite = GeneratedArt.PlayerSprite;
        pc.frontSprite = GeneratedArt.PlayerFrontSprite;
        pc.sideSprite = GeneratedArt.PlayerSideSprite;
        _player = playerGO.transform;

        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        _mainCamera = camGO.AddComponent<Camera>();
        _mainCamera.clearFlags = CameraClearFlags.SolidColor;
        _mainCamera.backgroundColor = new Color(0.14f, 0.16f, 0.2f);
        if (cfg.cameraOrthographic)
        {
            _mainCamera.orthographic = true;
            _mainCamera.orthographicSize = cfg.cameraOrthographicSize;
        }
        else
        {
            _mainCamera.fieldOfView = cfg.cameraFieldOfView;
        }
        _mainCamera.allowHDR = true;
        _bloom = camGO.AddComponent<BloomPostEffect>();
        camGO.AddComponent<AudioListener>();
        camGO.transform.rotation = Quaternion.Euler(cfg.cameraTilt, 0f, 0f);
        var rig = camGO.AddComponent<CameraRig>();
        rig.target = _player;
        rig.offset = cfg.cameraOffset;
        rig.followLerp = cfg.cameraFollowLerp;

        // 相机跟随范围：场景里放了 CameraBounds 就用它（可视化拖框）；否则用 GameConfig 默认框。
        rig.bounds = Object.FindFirstObjectByType<CameraBounds>();
        rig.useBounds = cfg.cameraBoundsEnabled;
        rig.minX = cfg.cameraBoundsCenter.x - cfg.cameraBoundsSize.x * 0.5f;
        rig.maxX = cfg.cameraBoundsCenter.x + cfg.cameraBoundsSize.x * 0.5f;
        rig.minZ = cfg.cameraBoundsCenter.y - cfg.cameraBoundsSize.y * 0.5f;
        rig.maxZ = cfg.cameraBoundsCenter.y + cfg.cameraBoundsSize.y * 0.5f;

        // 初始位置也按范围夹一次，避免开局镜头跳动
        Vector3 focus0 = _player.position;
        if (rig.bounds != null) focus0 = rig.bounds.Clamp(focus0);
        else if (rig.useBounds)
        {
            focus0.x = Mathf.Clamp(focus0.x, Mathf.Min(rig.minX, rig.maxX), Mathf.Max(rig.minX, rig.maxX));
            focus0.z = Mathf.Clamp(focus0.z, Mathf.Min(rig.minZ, rig.maxZ), Mathf.Max(rig.minZ, rig.maxZ));
        }
        camGO.transform.position = focus0 + rig.offset;

        // 运行时调试面板（F1 呼出）+ 摆位模式（F2 拖棋子），仅在编辑器 / 开发包里挂载
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        gameObject.AddComponent<DebugPanel>().Init(_mainCamera, rig);
        gameObject.AddComponent<SceneArranger>().Init(_mainCamera);
        gameObject.AddComponent<MapObstacleGizmo>();   // Scene 视图里画出空气墙障碍圈，便于调走位
#endif
    }

    GameObject BuildPerson(string name, Sprite portrait, float scale, out SpriteRenderer body, bool castGroundShadow = false)
    {
        var root = new GameObject(name);

        // 脚下软阴影（平铺在地面上）
        var shadowGO = new GameObject("Shadow");
        shadowGO.transform.SetParent(root.transform, false);
        shadowGO.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        shadowGO.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        shadowGO.transform.localScale = Vector3.one * scale * 0.8f;
        var shadow = shadowGO.AddComponent<SpriteRenderer>();
        shadow.sprite = GeneratedArt.BlobShadowSprite;
        shadow.sortingOrder = 4;

        // 立绘包装物（Npc 的位移/缩放逻辑作用在 Portrait 上，互不干扰）。
        // 原来的 IdleBob 上下浮动已按需求去掉，棋子保持静止。
        var bob = new GameObject("Bob");
        bob.transform.SetParent(root.transform, false);

        var portraitGO = new GameObject("Portrait");
        portraitGO.transform.SetParent(bob.transform, false);
        portraitGO.transform.localScale = Vector3.one * scale;
        body = portraitGO.AddComponent<SpriteRenderer>();
        body.sprite = portrait;
        body.color = Color.white;
        body.sortingOrder = 10;
        portraitGO.AddComponent<CameraFacingSprite>();
        if (castGroundShadow) AttachGroundShadow(root.transform, body);
        return root;
    }

    static void AttachGroundShadow(Transform root, SpriteRenderer renderer)
    {
        var caster = root.gameObject.AddComponent<SpriteShadowCaster>();
        caster.Init(renderer);
    }

    // ---------------- 回合流程 ----------------

    public void StartRound()
    {
        foreach (var entry in Album)
            if (entry.image != null) Destroy(entry.image);
        Album.Clear();
        if (_robotCompanion != null) { Destroy(_robotCompanion); _robotCompanion = null; }
        _robotRewardPending = false;
        _hasRobot = false;

        // 本局炼化人数量 = characters 表里 kind != Normal 的角色数（wxc 固定剧本，身份写死在表）
        imposterCount = 0;
        foreach (var c in GameContent.Characters)
            if (c.kind != NpcKind.Normal) imposterCount++;

        _submitPending = false;
        if (_monster != null) { Destroy(_monster); _monster = null; }
        _deathPerforming = false;
        TimelineValue = 0;
        CurrentPhase = GameContent.GetPhaseForTimeline(0);
        if (_ambientDust != null) _ambientDust.Clear();
        ApplyPhaseGroundTexture();
        ApplyTimelineVisuals();
        if (_ambientDust != null) _ambientDust.Play();

        SpawnNpcs();
        AudioManager.Instance?.PlayBgm("phase" + CurrentPhase);
        State = GameState.Playing;

        UI.HideAllPanels();
        UI.SetHudVisible(true);
        UI.SetInteractPrompt(null);
        RefreshHud();
        UI.PlayFadeIn();
    }

    void SpawnNpcs()
    {
        foreach (var n in Npcs)
            if (n != null) Destroy(n.gameObject);
        Npcs.Clear();

        // 固定剧本（wxc）：全部 characters 出场，身份直接取表里的 kind，不随机。
        var chosen = new List<CharacterDef>(GameContent.Characters);
        var cfg = GameConfig.Instance;

        for (int i = 0; i < chosen.Count; i++)
        {
            var def = chosen[i];

            Sprite portrait = GeneratedArt.GetCharacterSprite(def.artFolder);
            var go = BuildPerson("NPC_" + def.charId, portrait, cfg.npcScale, out var bodyR, castGroundShadow: true);
            go.transform.SetParent(_npcRoot, false);

            var npc = go.AddComponent<Npc>();
            npc.Setup(def.DisplayLabel, def.charId, def.kind, def.artFolder, def.dialogueId, bodyR, def.harmless);
            npc.SetHeadLabel(def.DisplayName, def.DisplayTitle);   // 头顶名字/职位（默认隐藏）

            // 每个角色的固定坐标来自 spawns.txt（可用运行时 F2「摆位模式」调好再保存）。
            // 没配到的角色回退到确定性网格（每局一致，不再随机）。
            var spawn = GameContent.GetSpawn(def.charId);
            if (spawn != null)
            {
                go.transform.position = new Vector3(spawn.x, 0f, spawn.z);
                npc.SetFacing(spawn.yaw, spawn.faceCamera);
            }
            else
            {
                go.transform.position = FixedFallbackPos(i, chosen.Count);
                npc.SetFacing(cfg.npcDefaultYaw, true);
            }

            Npcs.Add(npc);
        }
    }

    /// <summary>没有 spawns.txt 记录时的确定性布局（每局一致）：按索引在出生范围里铺成网格。</summary>
    Vector3 FixedFallbackPos(int index, int count)
    {
        var cfg = GameConfig.Instance;
        int cols = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(count)));
        int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)cols));
        int col = index % cols;
        int row = index / cols;
        float tx = cols <= 1 ? 0.5f : col / (float)(cols - 1);
        float tz = rows <= 1 ? 0.5f : row / (float)(rows - 1);
        return new Vector3(
            Mathf.Lerp(cfg.spawnAreaX.x, cfg.spawnAreaX.y, tx), 0f,
            Mathf.Lerp(cfg.spawnAreaZ.x, cfg.spawnAreaZ.y, tz));
    }

    static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    void RefreshHud()
    {
        UI.SetHud(MarkedCount, imposterCount, CurrentPhase, TimelineValue);
    }

    /// <summary>当前时间轴值（对话/拍照累加）。</summary>
    public int TimePoints => TimelineValue;

    /// <summary>累加时间轴（对话 +N/拍照 +M）：跨阈值推进 phase → 触发场景演变；满格 → 死亡演出（策划 1.3/5.1）。</summary>
    void AdvanceTimeline(int delta)
    {
        if (delta <= 0 || _deathPerforming) return;
        int maxT = GameContent.GetTimelineMax();
        int oldPhase = CurrentPhase;
        TimelineValue = Mathf.Min(TimelineValue + delta, maxT);
        CurrentPhase = GameContent.GetPhaseForTimeline(TimelineValue);
        RefreshHud();
        ApplyTimelineVisuals();
        if (CurrentPhase != oldPhase) OnStageChanged();   // 立绘/移位/地面贴图切换 + 阶段旁白
        // 对话在 Playing 态即时结算死亡；拍照在 Camera 态，延到关相机回 Playing（见 CloseCamera）
        if (State == GameState.Playing) TryEnterDeathByTime();
    }

    /// <summary>快进时间轴到下一阶段 threshold（调试/便捷；末阶段「晚上」不可用）。</summary>
    public void SkipToNextPhase()
    {
        if (State != GameState.Playing || _deathPerforming) return;
        if (GameContent.IsLastPhase(CurrentPhase)) return;
        int nextThreshold = GameContent.GetNextPhaseThreshold(CurrentPhase);
        if (nextThreshold < 0) return;
        int delta = nextThreshold - TimelineValue;
        if (delta <= 0) return;
        AdvanceTimeline(delta);
    }

    /// <summary>时间轴满格（超末阶段）且未在死亡演出中，则进入死亡演出（策划 5.1）。</summary>
    void TryEnterDeathByTime()
    {
        if (!_deathPerforming && TimelineValue >= GameContent.GetTimelineMax())
            EnterDeathPerformance();
    }

    /// <summary>进入新 phase：NPC 立绘随阶段(T4) + 光暗地色(T6/T9) + 阶段旁白。对话组随 phase 由 ResolveDialogue 自动处理(T5)。位置保持不变（不再随阶段移位）。</summary>
    void OnStageChanged()
    {
        foreach (var n in Npcs)
        {
            if (n == null) continue;
            n.ApplyStage(CurrentPhase);   // T4/N4：立绘随阶段（魏大爷扒皮）
            // T3：每阶段可指定坐标（phase_spawns.txt）；没配则位置不变（默认全阶段一致）
            var ps = GameContent.GetPhaseSpawn(n.charId, CurrentPhase);
            if (ps != null)
            {
                n.transform.position = new Vector3(ps.x, 0f, ps.z);
                n.SetFacing(ps.yaw, ps.faceCamera);
            }
        }
        ApplyPhaseGroundTexture();
        var phaseDef = GameContent.GetPhaseDef(CurrentPhase);
        if (phaseDef != null) UI.ShowToast(Loc.Format("phase.enter", phaseDef.DisplayName), true);
        AudioManager.Instance?.PlaySfx("phase_enter");   // 首次进入新阶段音效（T5）
        AudioManager.Instance?.PlayBgm("phase" + CurrentPhase);
    }

    static readonly Color AmbientMorning = new Color(0.42f, 0.42f, 0.46f);
    static readonly Color AmbientEvening = new Color(0.2f, 0.2f, 0.28f);
    static readonly Color GroundTintEvening = new Color(0.62f, 0.58f, 0.66f);
    static readonly Color FoliageTintEvening = new Color(0.26f, 0.30f, 0.52f);
    static readonly Color FoliageTintDeath = new Color(0.18f, 0.14f, 0.20f);
    static readonly Color DustColorMorning = new Color(0.92f, 0.90f, 0.86f, 0.42f);
    static readonly Color DustColorEvening = new Color(0.94f, 0.95f, 0.98f, 0.72f);   // 中性偏冷白，不靠发黄
    static readonly Color DustColorDeath = new Color(1f, 0.28f, 0.20f, 0.78f);
    const float DustHdrIntensityMorning = 1.4f;
    const float DustHdrIntensityEvening = 3.2f;
    const float DustHdrIntensityDeath = 3.5f;
    static readonly Vector3 SunEulerMorning = new Vector3(50f, -40f, 0f);
    static readonly Vector3 SunEulerEvening = new Vector3(16f, 95f, 0f);

    float TimelineNormalized()
    {
        int maxT = GameContent.GetTimelineMax();
        return maxT > 0 ? Mathf.Clamp01(TimelineValue / (float)maxT) : 0f;
    }

    /// <summary>按时间轴线性插值：阳光强度、环境光、地面色调、植物颜色。</summary>
    void ApplyTimelineVisuals()
    {
        if (_deathPerforming) return;
        float t = TimelineNormalized();
        if (_sun != null)
        {
            _sun.color = Color.white;
            _sun.intensity = Mathf.Lerp(1.1f, 0.35f, t);
            _sun.transform.rotation = Quaternion.Slerp(
                Quaternion.Euler(SunEulerMorning),
                Quaternion.Euler(SunEulerEvening),
                t);
        }
        RenderSettings.ambientLight = Color.Lerp(AmbientMorning, AmbientEvening, t);
        if (_groundMat != null)
            _groundMat.color = Color.Lerp(Color.white, GroundTintEvening, t);
        ApplyFoliageTint(t);
        ApplyDustVisuals(t);
        ApplyBloomVisuals(t);
    }

    void ApplyPhaseGroundTexture()
    {
        if (_groundMat == null) return;
        var tex = GeneratedArt.GroundTextureNamed("phase" + CurrentPhase) ?? GeneratedArt.GroundTexture;
        _groundMat.mainTexture = tex;
    }

    void ApplyFoliageTint(float t)
    {
        var color = Color.Lerp(Color.white, FoliageTintEvening, t);
        SetFoliageColor(color);
    }

    void SetFoliageColor(Color color)
    {
        for (int i = 0; i < _foliageRenderers.Count; i++)
            if (_foliageRenderers[i] != null) _foliageRenderers[i].color = color;
    }

    void ApplyDustVisuals(float t)
    {
        if (_ambientDust == null) return;
        var main = _ambientDust.main;
        main.startColor = Color.Lerp(DustColorMorning, DustColorEvening, t);
        if (_dustMaterial != null && _dustMaterial.HasProperty("_HdrIntensity"))
            _dustMaterial.SetFloat("_HdrIntensity", Mathf.Lerp(DustHdrIntensityMorning, DustHdrIntensityEvening, t));
    }

    void ApplyDeathDustVisuals()
    {
        if (_ambientDust == null) return;
        var main = _ambientDust.main;
        main.startColor = DustColorDeath;
        if (_dustMaterial != null && _dustMaterial.HasProperty("_HdrIntensity"))
            _dustMaterial.SetFloat("_HdrIntensity", DustHdrIntensityDeath);
    }

    void ApplyBloomVisuals(float t)
    {
        if (_bloom == null) return;
        // 阈值 > 1：普通 Sprite 白部(max=1)不进 Bloom，只有灰尘 HDR 乘数(≈3.2)会发光
        _bloom.SetBloom(Mathf.Lerp(0f, 1.35f, t), Mathf.Lerp(1.5f, 1.12f, t));
    }

    /// <summary>当前被玩家标记为嫌疑人的数量。</summary>
    public int MarkedCount
    {
        get
        {
            int c = 0;
            foreach (var n in Npcs) if (n != null && n.marked) c++;
            return c;
        }
    }

    /// <summary>当前所有被标记的 NPC。</summary>
    public List<Npc> MarkedNpcs
    {
        get
        {
            var list = new List<Npc>();
            foreach (var n in Npcs) if (n != null && n.marked) list.Add(n);
            return list;
        }
    }

    // ---------------- 输入热键 ----------------

    void Update()
    {
        switch (State)
        {
            case GameState.MainMenu:
                if (_creditsOpen && Input.GetKeyDown(KeyCode.Escape)) CloseCredits();
                break;

            case GameState.Briefing:
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
                    ConfirmBriefing();
                else if (Input.GetKeyDown(KeyCode.Escape))
                    EnterMainMenu();
                break;

            case GameState.Tutorial:
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
                    TutorialNext();
                break;

            case GameState.Playing:
                if (Input.GetKeyDown(KeyCode.Space)) OpenCamera();
                else if (Input.GetKeyDown(KeyCode.Tab)) OpenAlbum();
                else if (Input.GetKeyDown(KeyCode.M)) OpenMarkList();
                else if (Input.GetKeyDown(KeyCode.E)) TalkNearest();
                else if (Input.GetKeyDown(KeyCode.Q)) ViewNearestPhotos();
                else if (Input.GetKeyDown(KeyCode.F)) ToggleMarkNearest();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                else if (Input.GetKeyDown(KeyCode.T)) AdvanceTimeline(1);   // 测试：时间轴 +1
#endif
                break;

            case GameState.Camera:
                UpdateFraming();
                if (Input.GetKeyDown(KeyCode.Space)) OnShutter();
                else if (Input.GetKeyDown(KeyCode.Alpha1)) OnCameraPose(false); // 比耶
                else if (Input.GetKeyDown(KeyCode.Alpha2)) OnCameraPose(true);  // 笑
                else if (Input.GetKeyDown(KeyCode.Escape)) CloseCamera();
                break;

            case GameState.Album:
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.Q))
                    CloseAlbum();
                break;

            case GameState.MarkList:
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    if (_submitPending) CancelSubmit();
                    else CloseMarkList();
                }
                break;

            case GameState.Dialogue:
                if (Input.GetKeyDown(KeyCode.Escape)) EndDialogue();
                break;

            case GameState.RobotReward:
                break;   // 奖励弹窗必须点击确认，避免误按热键跳过

            case GameState.Death:
                break;   // 死亡演出：无热键，玩家只能逃（移动在 PlayerController）
        }
    }

    // ---------------- 纯对话 ----------------

    public void BeginDialogue(Npc npc)
    {
        if (State != GameState.Playing || npc == null) return;
        // 特殊死亡（策划 5.3 / 时间轴 phase3「真面目」）：phase3 起与人皮狗互动即触发
        if (npc.kind == NpcKind.SkinDog && CurrentPhase >= 3)
        {
            TriggerSpecialDeath();
            return;
        }
        _dialogueNpc = npc;
        // 对话组随 phase/count 由 ResolveDialogueId 解析（安安第5次真心话即走 count 表；带每句立绘）
        _dialogueId = GameContent.ResolveDialogueId(npc, CurrentPhase);
        // 已经获得机器人后再次在第一阶段找林采，不再重复发放，只回应机器人近况。
        if (_hasRobot && _dialogueId == "lin_p1") _dialogueId = "lin_robot_repeat";
        _dialogueLines = GameContent.GetLinesById(_dialogueId, npc.charId);
        _dialogueIndex = 0;
        _dialogueCompleted = false;
        _awaitingChoice = false;
        State = GameState.Dialogue;
        UI.SetInteractPrompt(null);
        UI.SetHudVisible(false);
        AudioManager.Instance?.PlaySfx("dialogue_open");
        if (_dialogueLines.Length > 0)
            UI.ShowDialogue(npc.npcName, _dialogueLines[0]);
        else
            TryShowChoicesOrEnd();
    }

    public void DialogueNext()
    {
        if (State != GameState.Dialogue || _awaitingChoice) return;
        if (UI.CompleteDialogueTyping()) return;
        _dialogueIndex++;
        if (_dialogueIndex >= _dialogueLines.Length)
        {
            TryShowChoicesOrEnd();
            return;
        }
        UI.ShowDialogue(_dialogueNpc.npcName, _dialogueLines[_dialogueIndex]);
    }

    /// <summary>台词播完：若当前 dialogueId 配了分支就弹选项，否则正常结束。</summary>
    void TryShowChoicesOrEnd()
    {
        var choices = GameContent.GetChoices(_dialogueId);
        if (choices != null)
        {
            _awaitingChoice = true;
            var labels = new List<string>();
            foreach (var c in choices) labels.Add(c.Label);
            UI.ShowDialogueChoices(labels);
            return;
        }
        _dialogueCompleted = true;
        EndDialogue();
    }

    /// <summary>玩家点了某个分支选项。</summary>
    public void DialogueChoose(int index)
    {
        if (State != GameState.Dialogue || !_awaitingChoice) return;
        var choices = GameContent.GetChoices(_dialogueId);
        if (choices == null || index < 0 || index >= choices.Count) return;
        var ch = choices[index];
        _awaitingChoice = false;

        if (ch.effect == "special_death") { TriggerSpecialDeath(); return; }
        if (ch.effect == "grant_robot" && !_hasRobot) _robotRewardPending = true;

        if (!string.IsNullOrEmpty(ch.gotoId))
        {
            // 续接到另一段对话（其台词播完后也会再查它自己的 choices，可链式分支）
            _dialogueId = ch.gotoId;
            _dialogueLines = GameContent.GetLinesById(_dialogueId, _dialogueNpc != null ? _dialogueNpc.charId : null);
            _dialogueIndex = 0;
            if (_dialogueLines.Length > 0) UI.ShowDialogue(_dialogueNpc != null ? _dialogueNpc.npcName : "", _dialogueLines[0]);
            else TryShowChoicesOrEnd();
            return;
        }

        // effect == end 或空：完整读完，正常结束
        _dialogueCompleted = true;
        EndDialogue();
    }

    public void EndDialogue()
    {
        var npc = _dialogueNpc;
        bool completed = _dialogueCompleted;
        bool wasInDialogue = State == GameState.Dialogue;
        bool showRobotReward = wasInDialogue && _robotRewardPending && !_hasRobot;
        _dialogueNpc = null;
        _dialogueCompleted = false;
        _awaitingChoice = false;
        if (State == GameState.Dialogue)
            State = showRobotReward ? GameState.RobotReward : GameState.Playing;
        UI.HideDialogue();
        UI.SetHudVisible(!showRobotReward);
        // 完整读完一段对话才计：count 模式递增到访次数（安安等靠它推进）+ 时间轴 +N
        if (wasInDialogue && completed)
        {
            if (npc != null && GameContent.GetDialogueMode(npc.charId) == NpcDialogueMode.Count)
                npc.dialogueVisitCount++;
            AdvanceTimeline(GameConfig.Instance.dialogueTimePoints);
        }
        if (showRobotReward) UI.ShowRobotReward();
    }

    public void ConfirmRobotReward()
    {
        if (State != GameState.RobotReward) return;
        _robotRewardPending = false;
        _hasRobot = true;
        UI.HideRobotReward();
        SpawnRobotCompanion();
        State = GameState.Playing;
        UI.SetHudVisible(true);
        RefreshHud();
        TryEnterDeathByTime();
    }

    void SpawnRobotCompanion()
    {
        if (_robotCompanion != null || _player == null) return;
        var frames = GeneratedArt.RobotFrames;
        if (frames == null || frames.Length == 0) return;
        Sprite start = frames.Length > 1 && frames[1] != null ? frames[1] : frames[0];
        if (start == null) return;

        var cfg = GameConfig.Instance;
        var go = BuildPerson("LittleMeRobot", start, cfg.playerScale * 0.75f, out var body);
        Vector3 spawn = _player.position + new Vector3(-1.25f, 0f, 0.25f);
        spawn = MapObstacles.Resolve(spawn, 0.28f);
        spawn.x = Mathf.Clamp(spawn.x, -cfg.mapHalfX, cfg.mapHalfX);
        spawn.z = Mathf.Clamp(spawn.z, -cfg.mapHalfZ, cfg.mapHalfZ);
        go.transform.position = spawn;

        var follower = go.AddComponent<RobotFollower>();
        follower.Setup(_player, body, frames);
        _robotCompanion = go;
    }

    // ---------------- 相机 / 取景 / 拍照 ----------------

    public void OpenCamera()
    {
        if (State != GameState.Playing) return;
        State = GameState.Camera;
        _framed.Clear();
        UI.SetInteractPrompt(null);
        UI.SetHudVisible(false);
        UI.ShowCamera();
    }

    public void CloseCamera()
    {
        if (State != GameState.Camera) return;
        foreach (var npc in Npcs)
        {
            if (npc == null) continue;
            npc.SetInFrame(false);
            npc.SetPose(PoseType.None);
        }
        _framed.Clear();
        State = GameState.Playing;
        UI.HideCamera();
        UI.SetHudVisible(true);
        TryEnterDeathByTime();   // 相机内拍照攒过阈值的死亡，回 Playing 时结算
    }

    /// <summary>每帧根据取景框刷新“在镜头中”的 NPC。</summary>
    void UpdateFraming()
    {
        Rect vf = UI.ViewfinderScreenRect;
        _framed.Clear();
        foreach (var npc in Npcs)
        {
            if (npc == null) continue;
            Vector3 sp = npc.GetScreenPoint(_mainCamera);
            bool inside = sp.z > 0f && vf.Contains(new Vector2(sp.x, sp.y));
            npc.SetInFrame(inside);
            if (inside) _framed.Add(npc);
        }
        UI.SetFramedChips(_framed);
    }

    /// <summary>指挥取景框内所有 NPC 摆动作，部分伪人会当场露馅。</summary>
    public void OnCameraPose(bool smile)
    {
        if (State != GameState.Camera) return;
        PoseType pose = smile ? PoseType.Smile : PoseType.Yeah;
        foreach (var npc in _framed)
            npc.SetPose(pose);
        UI.ShowToast(Loc.Get(smile ? "cam.poseSmile" : "cam.posePeace"), true);
    }

    public void OnShutter()
    {
        if (State != GameState.Camera || _capturing) return;
        UI.PlayShutterPress();
        AudioManager.Instance?.PlaySfx("shutter");
        StartCoroutine(CapturePhoto());
    }

    IEnumerator CapturePhoto()
    {
        _capturing = true;

        // 记录取景框内的 NPC（在应用照片异常之前）
        Rect vf = UI.ViewfinderScreenRect;
        var framedNow = new List<Npc>();
        foreach (var npc in Npcs)
        {
            if (npc == null) continue;
            Vector3 sp = npc.GetScreenPoint(_mainCamera);
            if (sp.z > 0f && vf.Contains(new Vector2(sp.x, sp.y)))
                framedNow.Add(npc);
        }

        // 关键：不再自己离屏重渲染（那会和屏幕上真正看到的画面不一致），
        // 而是直接抓取“玩家此刻屏幕上的画面”，再从中裁出取景开口那块区域，
        // 天然做到所见即所拍。为此先应用照片异常、并把相机外壳/准星藏起来，
        // 让这一帧屏幕上只剩下场景本身。
        foreach (var npc in Npcs) npc.ApplyPhotoState();
        UI.SetCameraOverlayVisible(false);

        yield return new WaitForEndOfFrame(); // 渲染出这一帧“干净场景”

        Texture2D full = ScreenCapture.CaptureScreenshotAsTexture();

        // 立即恢复外壳与 NPC 状态
        UI.SetCameraOverlayVisible(true);
        foreach (var npc in Npcs) npc.RestorePhotoState();

        // 截图分辨率可能与逻辑屏幕分辨率不同（超采样等），按比例换算取景矩形。
        int sw = full.width, sh = full.height;
        float scaleX = sw / (float)Screen.width;
        float scaleY = sh / (float)Screen.height;
        int x = Mathf.Clamp(Mathf.RoundToInt(vf.x * scaleX), 0, sw - 1);
        int y = Mathf.Clamp(Mathf.RoundToInt(vf.y * scaleY), 0, sh - 1);
        int w = Mathf.Clamp(Mathf.RoundToInt(vf.width * scaleX), 1, sw - x);
        int h = Mathf.Clamp(Mathf.RoundToInt(vf.height * scaleY), 1, sh - y);

        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.SetPixels(full.GetPixels(x, y, w, h)); // GetPixels 与屏幕同为左下原点
        tex.Apply();
        Destroy(full);

        Album.Add(new PhotoEntry { image = tex, framed = framedNow });
        AdvanceTimeline(GameConfig.Instance.photoTimePoints);   // 拍照 +M 时间轴（无限张，无胶卷）
        RefreshHud();
        UI.ShowToast(Loc.Get("cam.shot"), true);

        // 拍照手感：先来一下震屏（此时已抓完帧，不影响成片），
        // 再把刚拍的照片“飞”出去，最后短暂定格一下。
        var rig = _mainCamera != null ? _mainCamera.GetComponent<CameraRig>() : null;
        if (rig != null) rig.Shake(0.16f, 0.18f);
        UI.PlayPhotoFly(tex, vf);

        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(0.06f);
        Time.timeScale = 1f;

        _capturing = false;
    }

    // ---------------- 靠近角色的交互（探索态） ----------------

    /// <summary>由 PlayerController 每帧上报最近的可交互 NPC。</summary>
    public void UpdateNearest(Npc npc)
    {
        if (_nearestNpc != npc)
        {
            if (_nearestNpc != null) _nearestNpc.SetNearest(false);
            if (npc != null) npc.SetNearest(true);
        }
        _nearestNpc = npc;
        UI.ShowInteract(npc);
    }

    public void TalkNearest()
    {
        if (State == GameState.Playing && _nearestNpc != null) BeginDialogue(_nearestNpc);
    }

    public void ViewNearestPhotos()
    {
        if (State == GameState.Playing && _nearestNpc != null) ViewCharacterPhotos(_nearestNpc);
    }

    public void ToggleMarkNearest()
    {
        if (State == GameState.Playing && _nearestNpc != null) ToggleMark(_nearestNpc);
    }

    // ---------------- 相册 / 照片查看 ----------------

    public void OpenAlbum()
    {
        if (State != GameState.Playing) return;
        State = GameState.Album;
        UI.SetHudVisible(false);
        UI.ShowAlbum(Album, Loc.Format("album.titleAll", Album.Count));
    }

    /// <summary>查看某个角色出现过的照片（靠近该角色时触发）。</summary>
    public void ViewCharacterPhotos(Npc npc)
    {
        if (State != GameState.Playing || npc == null) return;
        var shots = new List<PhotoEntry>();
        foreach (var e in Album)
            if (e.framed.Contains(npc)) shots.Add(e);

        State = GameState.Album;
        UI.SetHudVisible(false);
        UI.ShowAlbum(shots, Loc.Format("album.titleChar", npc.npcName, shots.Count));
    }

    public void CloseAlbum()
    {
        if (State != GameState.Album) return;
        State = GameState.Playing;
        UI.HideAlbum();
        UI.SetHudVisible(true);
    }

    /// <summary>
    /// 相册指认（策划 1.4）：用勾选的照片判定是否揪出全部伪人。
    /// 判定①勾选≥1；判定②勾选照片覆盖的 NPC == 全部伪人（不漏伪人、不错勾非豁免正常人）。
    /// </summary>
    public void AccuseWithPhotos(List<Npc> covered) => AccuseCovered(covered);

    public void AccuseWithPhotos(List<PhotoEntry> selected)
    {
        if (State != GameState.Album) return;
        if (selected == null || selected.Count < 1)
        {
            UI.ShowToast(Loc.Get("accuse.needphoto"), false);   // 判定①失败
            return;
        }

        // 勾选照片覆盖的 NPC 并集
        var covered = new HashSet<Npc>();
        foreach (var e in selected)
            if (e != null)
                foreach (var n in e.framed)
                    if (n != null) covered.Add(n);
        AccuseCovered(new List<Npc>(covered));
    }

    void AccuseCovered(List<Npc> covered)
    {
        int correctImposters = 0, wrongInnocents = 0;
        if (covered != null)
            foreach (var n in covered)
            {
                if (n == null) continue;
                if (n.IsImposter) correctImposters++;
                else if (!n.harmless) wrongInnocents++;   // 无害正常人(N2)不计错
            }

        int totalImposters = 0;
        foreach (var n in Npcs) if (n != null && n.IsImposter) totalImposters++;

        bool win = correctImposters >= totalImposters && wrongInnocents == 0;

        // 先弹旁白过场（策划 1.4），State=Result 挡住旁白期间输入（EnterDeathPerformance 会再切到 Death）
        State = GameState.Result;
        UI.HideAlbum();
        UI.SetHudVisible(false);
        int c = correctImposters, w = wrongInnocents;
        if (win)
        {
            UI.ShowNarration(Loc.Get("narrate.win"), () => EndRound(true, c, w));   // 胜利结局图留待阶段七
        }
        else
        {
            // 指认错误（策划 4.5.2.1）：旁白「你已经没有机会了」→ 时间轴拉满 → 死亡演出
            UI.ShowNarration(Loc.Get("narrate.lose"), () =>
            {
                TimelineValue = GameContent.GetTimelineMax();
                ApplyTimelineVisuals();   // 时间拉满后再进死亡，避免光色/植物与进度不同步
                EnterDeathPerformance();
            });
        }
    }

    // ---------------- 标记嫌疑人（不告知对错） ----------------

    /// <summary>标记 / 取消标记某个角色为嫌疑人。仅做标记，不透露正确与否。</summary>
    public void ToggleMark(Npc npc)
    {
        if (State != GameState.Playing || npc == null) return;
        npc.SetMarked(!npc.marked);
        AudioManager.Instance?.PlaySfx(npc.marked ? "mark" : "unmark");
        UI.ShowToast(Loc.Format(npc.marked ? "toast.marked" : "toast.unmarked", npc.npcName), npc.marked);
        RefreshHud();
        UI.ShowInteract(npc); // 刷新按钮文案（标记 / 取消标记）
    }

    // ---------------- 指认列表 / 提交 ----------------

    /// <summary>右上角按钮：切换所有 NPC 头顶名字/职位显示。各 NPC 在 Update 里读取该开关。</summary>
    public void ToggleNpcLabels()
    {
        ShowNpcLabels = !ShowNpcLabels;
        UI?.UpdateNpcLabelButton(ShowNpcLabels);
    }

    public void OpenMarkList()
    {
        if (State != GameState.Playing) return;
        State = GameState.MarkList;
        _submitPending = false;
        UI.SetHudVisible(false);
        UI.ShowMarkList(MarkedNpcs);
    }

    public void CloseMarkList()
    {
        if (State != GameState.MarkList) return;
        _submitPending = false;
        State = GameState.Playing;
        UI.HideMarkList();
        UI.SetHudVisible(true);
    }

    /// <summary>在列表里移除某个标记，然后刷新列表。</summary>
    public void UnmarkFromList(Npc npc)
    {
        if (State != GameState.MarkList || npc == null) return;
        npc.SetMarked(false);
        RefreshHud();
        UI.ShowMarkList(MarkedNpcs);
    }

    /// <summary>点击“提交”：弹出确认框，告知玩家提交后游戏结束。</summary>
    public void RequestSubmit()
    {
        if (State != GameState.MarkList) return;
        _submitPending = true;
        UI.ShowSubmitConfirm(MarkedNpcs);
    }

    public void CancelSubmit()
    {
        if (State != GameState.MarkList) return;
        _submitPending = false;
        UI.HideSubmitConfirm();
    }

    /// <summary>确认提交：结算所有标记，游戏结束。</summary>
    public void ConfirmSubmit()
    {
        if (State != GameState.MarkList) return;
        _submitPending = false;

        int correct = 0, wrong = 0;
        foreach (var n in Npcs)
        {
            if (n == null || !n.marked) continue;
            if (n.IsImposter) correct++; else wrong++;
        }
        bool win = correct == imposterCount && wrong == 0;
        EndRound(win, correct, wrong);
    }

    void EndRound(bool win, int correct, int wrong)
    {
        State = GameState.Result;
        if (win) { AudioManager.Instance?.StopBgm(); AudioManager.Instance?.PlaySfx("victory"); }

        // 只显示胜/负 + 猜对个数，不揭示哪些角色是伪人（策划新需求）
        UI.HideAllPanels();
        UI.SetHudVisible(false);
        UI.ShowResult(win, correct, imposterCount);
    }

    // ---------------- 死亡演出（策划 5） ----------------

    /// <summary>进入死亡演出：染红场景 + 生成追逐怪物；玩家此后只能逃。幂等。</summary>
    void EnterDeathPerformance()
    {
        if (_deathPerforming) return;
        _deathPerforming = true;
        State = GameState.Death;

        UI.HideAllPanels();
        UI.SetHudVisible(false);
        UI.SetInteractPrompt(null);

        // 场景变红 + 地面贴图变色/换图（策划 5.2.1/5.2.2）
        if (_sun != null)
        {
            _sun.transform.rotation = Quaternion.Euler(SunEulerEvening);   // 保持傍晚角度，不再「变亮」
            _sun.color = new Color(1f, 0.16f, 0.12f);
            _sun.intensity = 0.55f;   // 比之前 1.35 低，避免整体反差让植物显得突然变亮
        }
        RenderSettings.ambientLight = new Color(0.34f, 0.03f, 0.05f);
        if (_groundMat != null)
        {
            _groundMat.color = new Color(0.5f, 0.08f, 0.08f);
            var tex = GeneratedArt.GroundTextureNamed("death");
            if (tex != null) _groundMat.mainTexture = tex;   // 有专属死亡地面就换，否则保留当前贴图+染红
        }
        SetFoliageColor(FoliageTintDeath);
        foreach (var n in Npcs)
            if (n != null) n.ApplyBossPhaseTint();
        ApplyDeathDustVisuals();
        ApplyBloomVisuals(1f);

        AudioManager.Instance?.PlayBgm("death");
        SpawnDeathMonster();
        AudioManager.Instance?.PlaySfx("monster");
        UI.ShowToast(Loc.Get("death.flee"), false);
    }

    void SpawnDeathMonster()
    {
        if (_monster != null || _player == null) return;
        var cfg = GameConfig.Instance;
        var walkFrames = GeneratedArt.DeathMonsterWalkFrames;
        var go = BuildPerson("DeathMonster", walkFrames[0], cfg.npcScale, out var body);
        // 在玩家对角远处出生，留出追逐距离
        Vector3 pp = _player.position;
        float sx = pp.x >= 0 ? -1f : 1f, sz = pp.z >= 0 ? -1f : 1f;
        go.transform.position = new Vector3(sx * 14f, 0f, sz * 12f);
        var m = go.AddComponent<DeathMonster>();
        m.Setup(body, walkFrames, 10f);
        m.target = _player;
        m.speed = cfg.playerMoveSpeed * 1.05f;   // 略快于玩家：可逃一阵，终被逼死
        m.killRange = 1.3f;
        _monster = go;
    }

    /// <summary>怪物抓到玩家（策划 5.2.3）。</summary>
    public void PlayerCaughtByMonster()
    {
        if (State != GameState.Death) return;
        PlayerDied(false);
    }

    /// <summary>特殊死亡（策划 5.3）：对话分支 / stage3 真面目互动触发，直接结束。</summary>
    public void TriggerSpecialDeath()
    {
        if (State == GameState.Result) return;
        PlayerDied(true);
    }

    /// <summary>死亡结算：清理怪物后直接进入全屏失败图，不再显示“它抓住了你”文字过场。</summary>
    void PlayerDied(bool special)
    {
        if (_monster != null) { Destroy(_monster); _monster = null; }
        State = GameState.Result;
        UI.HideAllPanels();
        UI.SetHudVisible(false);
        AudioManager.Instance?.PlaySfx(special ? "special_death" : "death");

        // 即使因时间耗尽/怪物抓住而失败，底部仍显示玩家当前猜对的数量。
        int correct = 0, wrong = 0;
        foreach (var n in Npcs)
        {
            if (n == null || !n.marked) continue;
            if (n.IsImposter) correct++; else wrong++;
        }
        EndRound(false, correct, wrong);
    }

    public static string KindLabel(NpcKind k) => GameContent.KindLabel(k);
}
