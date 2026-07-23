using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum GameState { Playing, Dialogue, Camera, Album, Result }

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

    public GameState State { get; private set; } = GameState.Playing;
    public List<Npc> Npcs { get; private set; } = new List<Npc>();
    public UIManager UI { get; private set; }
    public List<PhotoEntry> Album { get; private set; } = new List<PhotoEntry>();

    int _film;
    int _imposterFound;

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

        StartRound();
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
                new Vector3(i, 0f, 19f), scale, 1);
            BuildCard(forestRoot, "Forest South " + i, GeneratedArt.DenseForestEdgeSprite,
                new Vector3(i, 0f, -19f), scale, 1);
        }
        for (int i = -12; i <= 12; i += 6)
        {
            BuildCard(forestRoot, "Forest East " + i, GeneratedArt.DenseForestEdgeSprite,
                new Vector3(19f, 0f, i), 0.98f, 1);
            BuildCard(forestRoot, "Forest West " + i, GeneratedArt.DenseForestEdgeSprite,
                new Vector3(-19f, 0f, i), 0.98f, 1);
        }

        // 内层：低矮灌木和松树形成高—中—低三层，边缘不会显得像一堵平墙。
        for (int i = -15; i <= 15; i += 5)
        {
            BuildCard(forestRoot, "North Shrub " + i, GeneratedArt.GetForestSprite(1),
                new Vector3(i, 0f, 15.6f), 0.75f, 2);
            BuildCard(forestRoot, "South Shrub " + i, GeneratedArt.GetForestSprite(1),
                new Vector3(i, 0f, -15.6f), 0.75f, 2);
        }
        for (int i = -10; i <= 10; i += 5)
        {
            BuildCard(forestRoot, "East Shrub " + i, GeneratedArt.GetForestSprite(1),
                new Vector3(15.6f, 0f, i), 0.7f, 2);
            BuildCard(forestRoot, "West Shrub " + i, GeneratedArt.GetForestSprite(1),
                new Vector3(-15.6f, 0f, i), 0.7f, 2);
        }

        BuildCard(forestRoot, "Pine NW", GeneratedArt.GetForestSprite(3), new Vector3(-16f, 0f, 14f), 1.05f, 3);
        BuildCard(forestRoot, "Pine NE", GeneratedArt.GetForestSprite(3), new Vector3(16f, 0f, 14f), 1.05f, 3);
        BuildCard(forestRoot, "Pine SW", GeneratedArt.GetForestSprite(3), new Vector3(-16f, 0f, -14f), 1.05f, 3);
        BuildCard(forestRoot, "Pine SE", GeneratedArt.GetForestSprite(3), new Vector3(16f, 0f, -14f), 1.05f, 3);
    }

    void BuildCloudLayer()
    {
        var cloudRoot = new GameObject("Cloud Layer").transform;
        BuildCloud(cloudRoot, "Cloud A", GeneratedArt.GetForestSprite(4), new Vector3(-9f, 7f, 10f), 1.2f, 0.22f);
        BuildCloud(cloudRoot, "Cloud B", GeneratedArt.GetForestSprite(5), new Vector3(6f, 8f, 4f), 1.25f, 0.16f);
        BuildCloud(cloudRoot, "Cloud C", GeneratedArt.GetForestSprite(4), new Vector3(12f, 7.5f, -12f), 1f, 0.26f);
    }

    void BuildCard(Transform parent, string name, Sprite sprite, Vector3 position, float scale, int sortingOrder)
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

        var portraitGO = new GameObject("Portrait");
        portraitGO.transform.SetParent(root.transform, false);
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

        SpawnNpcs();
        _film = filmMax;
        _imposterFound = 0;
        State = GameState.Playing;

        UI.HideAllPanels();
        UI.SetHudVisible(true);
        UI.SetInteractPrompt(null);
        RefreshHud();
    }

    void SpawnNpcs()
    {
        foreach (var n in Npcs)
            if (n != null) Destroy(n.gameObject);
        Npcs.Clear();

        var kinds = new List<NpcKind>();
        var imposterPool = new List<NpcKind>
        {
            NpcKind.DouBao, NpcKind.SixFinger, NpcKind.ScarySmile,
            NpcKind.FrameDrop, NpcKind.Stitched, NpcKind.PhotoMissing, NpcKind.Deflate
        };
        Shuffle(imposterPool);
        for (int i = 0; i < imposterCount && i < imposterPool.Count; i++)
            kinds.Add(imposterPool[i]);
        while (kinds.Count < npcCount)
            kinds.Add(NpcKind.Normal);
        Shuffle(kinds);

        var names = new List<string>(GameContent.Names);
        Shuffle(names);

        for (int i = 0; i < npcCount; i++)
        {
            Sprite portrait = GeneratedArt.GetCharacterSprite(i);
            var go = BuildPerson("NPC_" + i, portrait, 1f, out var bodyR);
            go.transform.SetParent(_npcRoot, false);
            go.transform.position = RandomGroundPos();

            var npc = go.AddComponent<Npc>();
            npc.Setup(names[i % names.Count], kinds[i], i, bodyR);
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

    void RefreshHud() => UI.SetHud(_film, _imposterFound, imposterCount);

    // ---------------- 输入热键 ----------------

    void Update()
    {
        switch (State)
        {
            case GameState.Playing:
                if (Input.GetKeyDown(KeyCode.Space)) OpenCamera();
                else if (Input.GetKeyDown(KeyCode.Tab)) OpenAlbum();
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

            case GameState.Dialogue:
                if (Input.GetKeyDown(KeyCode.Escape)) EndDialogue();
                break;
        }
    }

    // ---------------- 纯对话 ----------------

    public void BeginDialogue(Npc npc)
    {
        if (State != GameState.Playing || npc == null || npc.caught) return;
        _dialogueNpc = npc;
        _dialogueLines = GameContent.GetDialogue(npc.kind);
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
            if (npc == null || npc.caught) continue;
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
        UI.ShowToast(smile ? "“大家笑一个～”" : "“来，比个耶！”", true);
    }

    public void OnShutter()
    {
        if (State != GameState.Camera || _capturing) return;
        if (_film <= 0)
        {
            UI.ShowToast("没有胶卷了！", false);
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
            if (npc == null || npc.caught) continue;
            Vector3 sp = npc.GetScreenPoint(_mainCamera);
            if (sp.z > 0f && vf.Contains(new Vector2(sp.x, sp.y)))
                framedNow.Add(npc);
        }

        // 先让本帧正常渲染到屏幕（玩家看到的画面里人都在）
        yield return new WaitForEndOfFrame();

        // 再把“照片异常”应用到离屏渲染：PhotoMissing 从照片里消失
        foreach (var npc in Npcs) npc.ApplyPhotoState();

        int sw = Screen.width, sh = Screen.height;
        RenderTexture rt = RenderTexture.GetTemporary(sw, sh, 24);
        RenderTexture prevActive = RenderTexture.active;
        RenderTexture prevTarget = _mainCamera.targetTexture;

        _mainCamera.targetTexture = rt;
        _mainCamera.Render();
        RenderTexture.active = rt;

        int x = Mathf.Clamp(Mathf.RoundToInt(vf.x), 0, sw - 1);
        int y = Mathf.Clamp(Mathf.RoundToInt(vf.y), 0, sh - 1);
        int w = Mathf.Clamp(Mathf.RoundToInt(vf.width), 1, sw - x);
        int h = Mathf.Clamp(Mathf.RoundToInt(vf.height), 1, sh - y);

        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(x, y, w, h), 0, 0);
        tex.Apply();

        _mainCamera.targetTexture = prevTarget;
        RenderTexture.active = prevActive;
        RenderTexture.ReleaseTemporary(rt);

        foreach (var npc in Npcs) npc.RestorePhotoState();

        Album.Add(new PhotoEntry { image = tex, framed = framedNow });
        _film--;
        RefreshHud();
        UI.ShowToast($"咔嚓！进相册了（剩余胶卷 {_film}）", true);

        _capturing = false;
        CheckLose();
    }

    // ---------------- 靠近角色的交互（探索态） ----------------

    /// <summary>由 PlayerController 每帧上报最近的可交互 NPC。</summary>
    public void UpdateNearest(Npc npc)
    {
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

    public void AccuseNearest()
    {
        if (State == GameState.Playing && _nearestNpc != null) Accuse(_nearestNpc);
    }

    // ---------------- 相册 / 照片查看 ----------------

    public void OpenAlbum()
    {
        if (State != GameState.Playing && State != GameState.Camera) return;
        if (State == GameState.Camera) CloseCamera();
        State = GameState.Album;
        UI.SetHudVisible(false);
        UI.ShowAlbum(Album, $"相册 —— 全部照片（{Album.Count} 张）");
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
        UI.ShowAlbum(shots, $"{npc.npcName} 的照片（{shots.Count} 张）");
    }

    public void CloseAlbum()
    {
        if (State != GameState.Album) return;
        State = GameState.Playing;
        UI.HideAlbum();
        UI.SetHudVisible(true);
    }

    // ---------------- 指认（靠近角色，直接指认） ----------------

    /// <summary>指认某个角色为伪人。正确抓获，错误浪费一张胶卷。</summary>
    public void Accuse(Npc npc)
    {
        if (State != GameState.Playing || npc == null || npc.caught || npc.accusedWrong) return;

        if (npc.IsImposter)
        {
            npc.MarkCaught();
            _imposterFound++;
            UI.ShowToast($"指认成功！{npc.npcName} 是伪人（{KindLabel(npc.kind)}）", true);
            RefreshHud();
            UI.ShowInteract(null);

            if (_imposterFound >= imposterCount)
                EndRound(true);
        }
        else
        {
            npc.MarkAccusedWrong();
            _film--;
            UI.ShowToast($"指认错误！{npc.npcName} 是普通人，浪费一张胶卷", false);
            RefreshHud();
            CheckLose();
        }
    }

    void CheckLose()
    {
        if (_film <= 0 && _imposterFound < imposterCount)
        {
            if (State == GameState.Album) CloseAlbum();
            EndRound(false);
        }
    }

    void EndRound(bool win)
    {
        State = GameState.Result;
        UI.HideAllPanels();
        UI.SetHudVisible(false);
        UI.ShowResult(win, _imposterFound, imposterCount, Album.Count);
    }

    public static string KindLabel(NpcKind k) => GameContent.KindLabel(k);
}
