using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum GameState { MainMenu, Playing, Dialogue, Camera, Album, MarkList, Result }

/// <summary>相册里的一张照片：截图 + 拍摄时处于取景框内的 NPC 名单。</summary>
public class PhotoEntry
{
    public Texture2D image;
    public List<Npc> framed = new List<Npc>();
}

/// <summary>
/// 游戏总控。程序化搭建场景，管理胶卷、状态机、拍照截图、相册指认与胜负。
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("关卡参数")]
    public int npcCount = 8;
    public int imposterCount = 3;
    public int filmMax = 10;

    public GameState State { get; private set; } = GameState.MainMenu;
    bool _creditsOpen;
    public List<Npc> Npcs { get; private set; } = new List<Npc>();
    public UIManager UI { get; private set; }
    public List<PhotoEntry> Album { get; private set; } = new List<PhotoEntry>();

    int _film;
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
    string[] _dialogueLines;
    int _dialogueIndex;

    // 取景态
    readonly List<Npc> _framed = new List<Npc>();
    bool _capturing;

    void Awake()
    {
        Instance = this;
        CleanupExistingScene();
        BuildEnvironment();
        BuildPlayerAndCamera();

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
    }

    /// <summary>点“开始游戏”。</summary>
    public void StartGame()
    {
        UI.HideMainMenu();
        StartRound();
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
        ground.transform.localScale = new Vector3(4f, 1f, 4f);
        var groundMaterial = ground.GetComponent<Renderer>().material;
        groundMaterial.mainTexture = GeneratedArt.GroundTexture;
        groundMaterial.mainTextureScale = new Vector2(5f, 5f);
        groundMaterial.color = Color.white;

        var lightGO = new GameObject("Sun");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        lightGO.transform.rotation = Quaternion.Euler(50f, -40f, 0f);
        RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.6f);

        _npcRoot = new GameObject("NPCs").transform;
        BuildTownProps();
        BuildForestBoundary();
        BuildCloudLayer();
        BuildAmbientParticles();
    }

    void BuildAmbientParticles()
    {
        var go = new GameObject("Ambient Dust");
        go.transform.position = new Vector3(0f, 3f, 0f);
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.loop = true;
        main.startLifetime = 9f;
        main.startSpeed = 0.15f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
        main.startColor = new Color(1f, 1f, 0.95f, 0.25f);
        main.maxParticles = 120;
        main.gravityModifier = -0.01f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 14f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(34f, 6f, 34f);

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
        vel.y = new ParticleSystem.MinMaxCurve(-0.05f, 0.08f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        // 用 Sprites/Default（已在 Always Included），避免打包时粒子材质被剥离变粉。
        var mat = new Material(Shader.Find("Sprites/Default"));
        var dotTex = GeneratedArt.SoftDotSprite.texture;
        mat.mainTexture = dotTex;
        renderer.material = mat;
        renderer.sortingOrder = 6;
        ps.Play();
    }

    void BuildTownProps()
    {
        var propsRoot = new GameObject("Town Props").transform;
        BuildCard(propsRoot, "Corner Shop", GeneratedArt.GetTownPropSprite(0), new Vector3(-14f, 0f, 12f), 1.15f, 2);
        BuildCard(propsRoot, "Tree North", GeneratedArt.GetTownPropSprite(1), new Vector3(11f, 0f, 13f), 1.15f, 2);
        BuildCard(propsRoot, "Tree West", GeneratedArt.GetTownPropSprite(1), new Vector3(-15f, 0f, -8f), 0.95f, 2);
        BuildCard(propsRoot, "Tree East", GeneratedArt.GetTownPropSprite(1), new Vector3(15f, 0f, -8f), 0.95f, 2);
        BuildCard(propsRoot, "Street Lamp A", GeneratedArt.GetTownPropSprite(2), new Vector3(-8f, 0f, 4f), 0.8f, 2);
        BuildCard(propsRoot, "Street Lamp B", GeneratedArt.GetTownPropSprite(2), new Vector3(8f, 0f, -3f), 0.8f, 2);
        BuildCard(propsRoot, "Park Bench", GeneratedArt.GetTownPropSprite(3), new Vector3(11f, 0f, 5f), 0.85f, 2);
        BuildCard(propsRoot, "Bench West", GeneratedArt.GetTownPropSprite(3), new Vector3(-10f, 0f, -12f), 0.75f, 2);
    }

    void BuildForestBoundary()
    {
        var forestRoot = new GameObject("Forest Boundary").transform;

        // 外层：宽幅密林连续覆盖四条边，彻底遮住正方形地图边缘。
        for (int i = -18; i <= 18; i += 6)
        {
            float scale = i % 12 == 0 ? 1.05f : 0.9f;
            BuildCard(forestRoot, "Forest North " + i, GeneratedArt.DenseForestEdgeSprite,
                new Vector3(i, 0f, 19f), scale, 1, true);
            BuildCard(forestRoot, "Forest South " + i, GeneratedArt.DenseForestEdgeSprite,
                new Vector3(i, 0f, -19f), scale, 1, true);
        }
        for (int i = -12; i <= 12; i += 6)
        {
            BuildCard(forestRoot, "Forest East " + i, GeneratedArt.DenseForestEdgeSprite,
                new Vector3(19f, 0f, i), 0.98f, 1, true);
            BuildCard(forestRoot, "Forest West " + i, GeneratedArt.DenseForestEdgeSprite,
                new Vector3(-19f, 0f, i), 0.98f, 1, true);
        }

        // 内层：低矮灌木和松树形成高—中—低三层，边缘不会显得像一堵平墙。
        for (int i = -15; i <= 15; i += 5)
        {
            BuildCard(forestRoot, "North Shrub " + i, GeneratedArt.GetForestSprite(1),
                new Vector3(i, 0f, 15.6f), 0.75f, 2, true);
            BuildCard(forestRoot, "South Shrub " + i, GeneratedArt.GetForestSprite(1),
                new Vector3(i, 0f, -15.6f), 0.75f, 2, true);
        }
        for (int i = -10; i <= 10; i += 5)
        {
            BuildCard(forestRoot, "East Shrub " + i, GeneratedArt.GetForestSprite(1),
                new Vector3(15.6f, 0f, i), 0.7f, 2, true);
            BuildCard(forestRoot, "West Shrub " + i, GeneratedArt.GetForestSprite(1),
                new Vector3(-15.6f, 0f, i), 0.7f, 2, true);
        }

        BuildCard(forestRoot, "Pine NW", GeneratedArt.GetForestSprite(3), new Vector3(-16f, 0f, 14f), 1.05f, 3, true);
        BuildCard(forestRoot, "Pine NE", GeneratedArt.GetForestSprite(3), new Vector3(16f, 0f, 14f), 1.05f, 3, true);
        BuildCard(forestRoot, "Pine SW", GeneratedArt.GetForestSprite(3), new Vector3(-16f, 0f, -14f), 1.05f, 3, true);
        BuildCard(forestRoot, "Pine SE", GeneratedArt.GetForestSprite(3), new Vector3(16f, 0f, -14f), 1.05f, 3, true);
    }

    void BuildCloudLayer()
    {
        var cloudRoot = new GameObject("Cloud Layer").transform;
        BuildCloud(cloudRoot, "Cloud A", GeneratedArt.GetForestSprite(4), new Vector3(-9f, 7f, 10f), 1.2f, 0.22f);
        BuildCloud(cloudRoot, "Cloud B", GeneratedArt.GetForestSprite(5), new Vector3(6f, 8f, 4f), 1.25f, 0.16f);
        BuildCloud(cloudRoot, "Cloud C", GeneratedArt.GetForestSprite(4), new Vector3(12f, 7.5f, -12f), 1f, 0.26f);
    }

    void BuildCard(Transform parent, string name, Sprite sprite, Vector3 position, float scale, int sortingOrder, bool sway = false)
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
    }

    void BuildCloud(Transform parent, string name, Sprite sprite, Vector3 position, float scale, float speed)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.position = position;
        var drift = root.AddComponent<CloudDrift>();
        drift.speed = speed;
        drift.direction = new Vector3(1f, 0f, 0.15f);

        var card = new GameObject("Card");
        card.transform.SetParent(root.transform, false);
        card.transform.localScale = Vector3.one * scale;
        var renderer = card.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = new Color(1f, 1f, 1f, 0.72f);
        renderer.sortingOrder = 3;
        card.AddComponent<CameraFacingSprite>();
    }

    void BuildPlayerAndCamera()
    {
        var playerGO = BuildPerson("Player", GeneratedArt.PlayerSprite, 0.47f, out _);
        playerGO.transform.position = new Vector3(0f, 0f, -6f);
        playerGO.AddComponent<PlayerController>();
        _player = playerGO.transform;

        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        _mainCamera = camGO.AddComponent<Camera>();
        _mainCamera.clearFlags = CameraClearFlags.SolidColor;
        _mainCamera.backgroundColor = new Color(0.14f, 0.16f, 0.2f);
        _mainCamera.fieldOfView = 55f;
        camGO.AddComponent<AudioListener>();
        camGO.transform.rotation = Quaternion.Euler(52f, 0f, 0f);
        var rig = camGO.AddComponent<CameraRig>();
        rig.target = _player;
        camGO.transform.position = _player.position + rig.offset;
    }

    GameObject BuildPerson(string name, Sprite portrait, float scale, out SpriteRenderer body)
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

        // 呼吸浮动包装物（Npc 的位移/缩放逻辑作用在 Portrait 上，互不干扰）
        var bob = new GameObject("Bob");
        bob.transform.SetParent(root.transform, false);
        bob.AddComponent<IdleBob>();

        var portraitGO = new GameObject("Portrait");
        portraitGO.transform.SetParent(bob.transform, false);
        portraitGO.transform.localScale = Vector3.one * scale;
        body = portraitGO.AddComponent<SpriteRenderer>();
        body.sprite = portrait;
        body.color = Color.white;
        body.sortingOrder = 10;
        portraitGO.AddComponent<CameraFacingSprite>();
        return root;
    }

    // ---------------- 回合流程 ----------------

    public void StartRound()
    {
        foreach (var entry in Album)
            if (entry.image != null) Destroy(entry.image);
        Album.Clear();

        // 关卡参数来自 rounds.txt（缺省沿用 Inspector 里的默认值）。
        var round = GameContent.GetDefaultRound();
        if (round != null)
        {
            npcCount = round.npcCount;
            imposterCount = round.imposterCount;
            filmMax = round.film;
        }

        SpawnNpcs(round);
        _film = filmMax;
        _submitPending = false;
        State = GameState.Playing;

        UI.HideAllPanels();
        UI.SetHudVisible(true);
        UI.SetInteractPrompt(null);
        RefreshHud();
        UI.PlayFadeIn();
    }

    void SpawnNpcs(RoundDef round)
    {
        foreach (var n in Npcs)
            if (n != null) Destroy(n.gameObject);
        Npcs.Clear();

        // 选出参与本关的角色（表里可能多于 npcCount，随机取一批）。
        var pool = new List<CharacterDef>(GameContent.Characters);
        Shuffle(pool);
        int count = Mathf.Min(npcCount, pool.Count);
        var chosen = pool.GetRange(0, count);

        // 决定每个角色的身份：优先用关卡表的 assign，其余按随机补足伪人。
        var kindByChar = new Dictionary<string, NpcKind>();
        int assigned = 0;
        if (round != null)
        {
            foreach (var c in chosen)
                if (round.assign.TryGetValue(c.charId, out var k) && k != NpcKind.Normal)
                {
                    kindByChar[c.charId] = k;
                    assigned++;
                }
        }

        int need = Mathf.Max(0, imposterCount - assigned);
        if (need > 0)
        {
            var impostorPool = new List<NpcKind>
            {
                NpcKind.DouBao, NpcKind.SixFinger, NpcKind.ScarySmile,
                NpcKind.FrameDrop, NpcKind.Stitched, NpcKind.PhotoMissing, NpcKind.Deflate
            };
            Shuffle(impostorPool);
            var free = new List<CharacterDef>();
            foreach (var c in chosen) if (!kindByChar.ContainsKey(c.charId)) free.Add(c);
            Shuffle(free);
            for (int i = 0; i < need && i < free.Count && i < impostorPool.Count; i++)
                kindByChar[free[i].charId] = impostorPool[i];
        }

        for (int i = 0; i < chosen.Count; i++)
        {
            var def = chosen[i];
            NpcKind kind = kindByChar.TryGetValue(def.charId, out var k) ? k : NpcKind.Normal;

            Sprite portrait = GeneratedArt.GetCharacterSprite(def.artFolder);
            var go = BuildPerson("NPC_" + def.charId, portrait, 1f, out var bodyR);
            go.transform.SetParent(_npcRoot, false);
            go.transform.position = RandomGroundPos();

            var npc = go.AddComponent<Npc>();
            npc.Setup(def.DisplayName, kind, def.artFolder, def.dialogueId, bodyR);
            Npcs.Add(npc);
        }
    }

    Vector3 RandomGroundPos() => new Vector3(Random.Range(-14f, 14f), 0f, Random.Range(-13f, 13f));

    static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    void RefreshHud() => UI.SetHud(_film, MarkedCount, imposterCount);

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

            case GameState.Playing:
                if (Input.GetKeyDown(KeyCode.Space)) OpenCamera();
                else if (Input.GetKeyDown(KeyCode.Tab)) OpenAlbum();
                else if (Input.GetKeyDown(KeyCode.M)) OpenMarkList();
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
        }
    }

    // ---------------- 纯对话 ----------------

    public void BeginDialogue(Npc npc)
    {
        if (State != GameState.Playing || npc == null) return;
        _dialogueNpc = npc;
        _dialogueLines = GameContent.GetDialogue(npc.kind, npc.dialogueId);
        _dialogueIndex = 0;
        State = GameState.Dialogue;
        UI.SetInteractPrompt(null);
        UI.SetHudVisible(false);
        UI.ShowDialogue(npc.npcName, _dialogueLines[0]);
    }

    public void DialogueNext()
    {
        if (State != GameState.Dialogue) return;
        _dialogueIndex++;
        if (_dialogueIndex >= _dialogueLines.Length)
        {
            EndDialogue();
            return;
        }
        UI.ShowDialogue(_dialogueNpc.npcName, _dialogueLines[_dialogueIndex]);
    }

    public void EndDialogue()
    {
        _dialogueNpc = null;
        if (State == GameState.Dialogue) State = GameState.Playing;
        UI.HideDialogue();
        UI.SetHudVisible(true);
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
        if (_film <= 0)
        {
            UI.ShowToast(Loc.Get("cam.nofilm"), false);
            return;
        }
        UI.PlayShutterPress();
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
        _film--;
        RefreshHud();
        UI.ShowToast(Loc.Format("cam.shot", _film), true);

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

    // ---------------- 标记嫌疑人（不告知对错） ----------------

    /// <summary>标记 / 取消标记某个角色为嫌疑人。仅做标记，不透露正确与否。</summary>
    public void ToggleMark(Npc npc)
    {
        if (State != GameState.Playing || npc == null) return;
        npc.SetMarked(!npc.marked);
        UI.ShowToast(Loc.Format(npc.marked ? "toast.marked" : "toast.unmarked", npc.npcName), npc.marked);
        RefreshHud();
        UI.ShowInteract(npc); // 刷新按钮文案（标记 / 取消标记）
    }

    // ---------------- 指认列表 / 提交 ----------------

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

        // 结算时揭示全部真正的伪人
        var imposters = new List<string>();
        foreach (var n in Npcs)
            if (n != null && n.IsImposter)
                imposters.Add($"{n.npcName}  ({KindLabel(n.kind)})");

        UI.HideAllPanels();
        UI.SetHudVisible(false);
        UI.ShowResult(win, correct, wrong, imposterCount, imposters, Album.Count);
    }

    public static string KindLabel(NpcKind k) => GameContent.KindLabel(k);
}
