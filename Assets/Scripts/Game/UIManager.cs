using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;

/// <summary>
/// 全部 UI 程序化生成（占位）。使用 legacy UI.Text + 动态系统字体显示中文。
/// 面板：HUD / 纯对话 / 相机取景 / 相册 / 结算。
/// </summary>
public class UIManager : MonoBehaviour
{
    Font _font;
    Canvas _canvas;
    RectTransform _canvasRect;

    // 本地化：登记所有静态文案（Text, key），语言切换时统一重刷
    readonly List<(Text text, string key)> _locStatic = new List<(Text, string)>();

    // 主菜单 / 制作者名单
    GameObject _menuRoot;
    GameObject _creditsRoot;
    Text _menuLangLabel;

    // HUD
    GameObject _hudRoot;
    Text _filmText, _foundText, _promptText;

    // 靠近角色的交互面板
    GameObject _interactRoot;
    Text _interactName;
    Button _interactMarkBtn;
    Text _interactMarkLabel;

    // 指认列表（已标记嫌疑人） + 提交确认弹窗
    GameObject _markListRoot;
    Transform _markListContainer;
    Text _markListHint;
    Button _markSubmitBtn;
    readonly List<GameObject> _markDynamic = new List<GameObject>();
    GameObject _submitConfirmRoot;
    Text _submitConfirmText;

    // 对话
    GameObject _dialogueRoot;
    Text _dialogueName, _dialogueLine;

    // 相机
    GameObject _cameraRoot;
    RectTransform _cameraUnit;   // 相机外壳 + 取景开口 + 手，整体跟随鼠标
    RectTransform _viewfinder;   // 截图区域，正好等于外壳的透明开口
    Text _framedText;
    Image _cameraBody;
    RectTransform _shutterHand;
    Vector2 _shutterHandRestPosition;
    Coroutine _shutterHandCo;

    // 相机外壳按其原始比例(1.5)铺满 BodyW×BodyH。
    const float BodyW = 913f;               // 外壳宽（画布参考单位）
    const float BodyH = 609f;               // 外壳高

    // 取景开口尺寸/偏移（画布单位）。运行时从外壳贴图的透明窟窿自动量取，
    // 使“截图矩形”与玩家透过的开口严格重合（所见即所拍）。
    // 量取失败时回退到实测值：开口≈64.0%宽 × 63.8%高，中心上移约 8。
    float _vfW = 0.640f * BodyW;
    float _vfH = 0.638f * BodyH;
    Vector2 _vfOffset = new Vector2(0f, 8f);

    // 照片查看器（全部 / 单角色）
    GameObject _albumRoot;
    RawImage _albumBigImage;
    AspectRatioFitter _albumFitter;
    Text _albumTitle;
    Text _albumHint;
    Text _albumCaption;
    Transform _thumbRow;
    readonly List<GameObject> _albumDynamic = new List<GameObject>();
    List<PhotoEntry> _albumEntries;
    int _selectedPhoto = -1;

    // 结算
    GameObject _resultRoot;
    Text _resultTitle, _resultDetail;

    // Toast
    Text _toastText;
    CanvasGroup _toastGroup;
    Coroutine _toastCo;

    // 表现：黑场过渡 / 相机 REC / HUD 数字跳动
    CanvasGroup _fadeGroup;
    Coroutine _fadeCo;
    Image _recDot;
    Text _recLabel;
    int _lastFilm = int.MinValue, _lastMarked = int.MinValue;

    // ---------------- 构建 ----------------

    public void Build()
    {
        _font = Font.CreateDynamicFontFromOSFont(
            new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "PingFang SC", "Noto Sans CJK SC", "Arial Unicode MS" }, 24);
        if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Arial", 24);

        EnsureEventSystem();

        var canvasGO = new GameObject("UICanvas");
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();
        _canvasRect = canvasGO.GetComponent<RectTransform>();

        BuildHud();
        BuildDialogue();
        BuildCamera();
        BuildAlbum();
        BuildMarkList();
        BuildResult();
        BuildToast();
        BuildMainMenu();
        BuildCredits();
        BuildFadeOverlay();

        Loc.OnChanged += Relocalize;
    }

    // ---------------- 主菜单 / 制作者名单 ----------------

    void BuildMainMenu()
    {
        _menuRoot = MakePanel(_canvas.transform, "MainMenu", new Color(0.05f, 0.06f, 0.1f, 0.96f));
        SetRect(_menuRoot.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        var title = MakeLocText(_menuRoot.transform, "Title", "menu.title", 96, TextAnchor.MiddleCenter);
        SetRect(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -180), new Vector2(1600, 130));
        title.color = new Color(1f, 0.9f, 0.55f);

        var sub = MakeLocText(_menuRoot.transform, "Subtitle", "menu.subtitle", 34, TextAnchor.MiddleCenter);
        SetRect(sub.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -300), new Vector2(1400, 50));
        sub.color = new Color(0.8f, 0.85f, 0.95f);

        MakeButton(_menuRoot.transform, "menu.start", 36, () => GameManager.Instance.StartGame(),
            new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(420, 96));
        MakeButton(_menuRoot.transform, "menu.credits", 32, () => GameManager.Instance.OpenCredits(),
            new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(420, 84));

        var langBtn = MakeButton(_menuRoot.transform, Loc.Format("menu.language", Loc.LanguageName), 32,
            () => GameManager.Instance.ToggleLanguage(),
            new Vector2(0.5f, 0.5f), new Vector2(0, -170), new Vector2(420, 84), register: false);
        SetButtonColor(langBtn, new Color(0.3f, 0.42f, 0.5f));
        _menuLangLabel = langBtn.GetComponentInChildren<Text>();

        var quitBtn = MakeButton(_menuRoot.transform, "menu.quit", 32, () => GameManager.Instance.QuitGame(),
            new Vector2(0.5f, 0.5f), new Vector2(0, -280), new Vector2(420, 84));
        SetButtonColor(quitBtn, new Color(0.5f, 0.3f, 0.3f));

        _menuRoot.SetActive(false);
    }

    void BuildCredits()
    {
        _creditsRoot = MakePanel(_canvas.transform, "Credits", new Color(0.04f, 0.05f, 0.08f, 0.98f));
        SetRect(_creditsRoot.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        var title = MakeLocText(_creditsRoot.transform, "CTitle", "credits.title", 60, TextAnchor.UpperCenter);
        SetRect(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -80), new Vector2(1400, 90));
        title.color = new Color(1f, 0.9f, 0.55f);

        var body = MakeLocText(_creditsRoot.transform, "CBody", "credits.body", 34, TextAnchor.UpperCenter);
        SetRect(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(1200, 500));
        body.color = new Color(0.88f, 0.92f, 1f);

        MakeButton(_creditsRoot.transform, "credits.back", 32, () => GameManager.Instance.CloseCredits(),
            new Vector2(0.5f, 0), new Vector2(0, 70), new Vector2(320, 84));

        _creditsRoot.SetActive(false);
    }

    public void ShowMainMenu()
    {
        if (_menuLangLabel != null) _menuLangLabel.text = Loc.Format("menu.language", Loc.LanguageName);
        _creditsRoot.SetActive(false);
        _menuRoot.SetActive(true);
        PlayShow(_menuRoot);
    }

    public void HideMainMenu()
    {
        if (_menuRoot != null) _menuRoot.SetActive(false);
        if (_creditsRoot != null) _creditsRoot.SetActive(false);
    }

    public void ShowCredits()
    {
        _creditsRoot.SetActive(true);
        PlayShow(_creditsRoot);
    }

    public void HideCredits()
    {
        if (_creditsRoot != null) _creditsRoot.SetActive(false);
    }

    void BuildFadeOverlay()
    {
        var go = MakePanel(_canvas.transform, "FadeOverlay", Color.black);
        SetRect(go.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        go.GetComponent<Image>().raycastTarget = false;
        _fadeGroup = go.AddComponent<CanvasGroup>();
        _fadeGroup.alpha = 0f;
        _fadeGroup.blocksRaycasts = false;
        _fadeGroup.interactable = false;
        go.transform.SetAsLastSibling();
    }

    void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }
    }

    void BuildHud()
    {
        // 所有 HUD 元素挂在一个透明容器下，进入相机/对话/相册/结算时整体隐藏。
        _hudRoot = new GameObject("HudRoot", typeof(RectTransform));
        _hudRoot.transform.SetParent(_canvas.transform, false);
        SetRect(_hudRoot.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        var hud = _hudRoot.transform;

        // 探索态暗角（相机/相册等模式随 HUD 一起隐藏，不会进照片）
        var vignette = new GameObject("Vignette", typeof(RectTransform));
        vignette.transform.SetParent(hud, false);
        var vimg = vignette.AddComponent<Image>();
        vimg.sprite = GeneratedArt.VignetteSprite;
        vimg.type = Image.Type.Simple;
        vimg.color = new Color(1f, 1f, 1f, 0.7f);
        vimg.raycastTarget = false;
        SetRect(vignette.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40, 40));
        vignette.transform.SetAsFirstSibling();

        MakeIcon(hud, "FilmIcon", GeneratedArt.GetIconSprite(0),
            new Vector2(0, 1), new Vector2(28, -28), new Vector2(46, 46));
        _filmText = MakeText(hud, "Film", "", 36, TextAnchor.UpperLeft);
        SetRect(_filmText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(86, -26), new Vector2(400, 60));

        MakeIcon(hud, "FoundIcon", GeneratedArt.GetIconSprite(6),
            new Vector2(1, 1), new Vector2(-250, -28), new Vector2(44, 44));
        _foundText = MakeText(hud, "Found", "", 36, TextAnchor.UpperRight);
        SetRect(_foundText.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
            new Vector2(-30, -26), new Vector2(400, 60));

        _promptText = MakeLocText(hud, "Prompt", "hud.prompt", 26, TextAnchor.LowerCenter);
        SetRect(_promptText.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
            new Vector2(0, 24), new Vector2(1300, 44));
        _promptText.color = new Color(1f, 1f, 1f, 0.6f);

        MakeButton(hud, "btn.camera", 30, () => GameManager.Instance.OpenCamera(),
            new Vector2(1, 0), new Vector2(-150, 210), new Vector2(240, 70));
        MakeButton(hud, "btn.album", 30, () => GameManager.Instance.OpenAlbum(),
            new Vector2(1, 0), new Vector2(-150, 130), new Vector2(240, 70));
        var markListBtn = MakeButton(hud, "btn.marklist", 28, () => GameManager.Instance.OpenMarkList(),
            new Vector2(1, 0), new Vector2(-150, 50), new Vector2(240, 70));
        SetButtonColor(markListBtn, new Color(0.7f, 0.4f, 0.3f));

        BuildInteractPanel(hud);
    }

    /// <summary>靠近某个 NPC 时出现的交互面板：交谈 / 看照片 / 指认。</summary>
    void BuildInteractPanel(Transform hud)
    {
        _interactRoot = MakePanel(hud, "Interact", new Color(0.06f, 0.07f, 0.1f, 0.9f));
        SetRect(_interactRoot.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
            new Vector2(0, 110), new Vector2(760, 130));

        _interactName = MakeText(_interactRoot.transform, "IName", "", 30, TextAnchor.UpperCenter);
        SetRect(_interactName.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(720, 40));
        _interactName.color = new Color(1f, 0.85f, 0.5f);

        MakeButton(_interactRoot.transform, "interact.talk", 26, () => GameManager.Instance.TalkNearest(),
            new Vector2(0.5f, 0), new Vector2(-250, 22), new Vector2(220, 64));
        MakeButton(_interactRoot.transform, "interact.viewphotos", 26, () => GameManager.Instance.ViewNearestPhotos(),
            new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(220, 64));
        _interactMarkBtn = MakeButton(_interactRoot.transform, "interact.mark", 24, () => GameManager.Instance.ToggleMarkNearest(),
            new Vector2(0.5f, 0), new Vector2(250, 22), new Vector2(220, 64));
        _interactMarkLabel = _interactMarkBtn.GetComponentInChildren<Text>();
        SetButtonColor(_interactMarkBtn, new Color(0.7f, 0.32f, 0.32f));

        _interactRoot.SetActive(false);
    }

    /// <summary>进入相机/对话/相册/结算等模式时隐藏主场景 HUD，回到探索时恢复。</summary>
    public void SetHudVisible(bool visible)
    {
        if (_hudRoot != null) _hudRoot.SetActive(visible);
    }

    /// <summary>刷新靠近角色的交互面板。npc 为空则隐藏。</summary>
    public void ShowInteract(Npc npc)
    {
        if (_interactRoot == null) return;
        if (npc == null)
        {
            _interactRoot.SetActive(false);
            return;
        }
        bool wasActive = _interactRoot.activeSelf;
        _interactRoot.SetActive(true);
        if (!wasActive) PlayShow(_interactRoot);
        _interactName.text = npc.marked ? npc.npcName + Loc.Get("interact.markedSuffix") : npc.npcName;
        if (_interactMarkLabel != null)
            _interactMarkLabel.text = Loc.Get(npc.marked ? "interact.unmark" : "interact.mark");
        if (_interactMarkBtn != null)
            SetButtonColor(_interactMarkBtn, npc.marked ? new Color(0.4f, 0.45f, 0.55f) : new Color(0.7f, 0.32f, 0.32f));
    }

    void BuildDialogue()
    {
        _dialogueRoot = MakePanel(_canvas.transform, "Dialogue", new Color(0.06f, 0.07f, 0.1f, 0.94f));
        SetRect(_dialogueRoot.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
            new Vector2(0, 200), new Vector2(1200, 300));

        _dialogueName = MakeText(_dialogueRoot.transform, "DName", "", 36, TextAnchor.UpperLeft);
        SetRect(_dialogueName.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -24), new Vector2(600, 50));
        _dialogueName.color = new Color(1f, 0.85f, 0.5f);

        _dialogueLine = MakeText(_dialogueRoot.transform, "DLine", "", 32, TextAnchor.UpperLeft);
        SetRect(_dialogueLine.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -90), new Vector2(-80, 120));

        MakeButton(_dialogueRoot.transform, "dlg.next", 30, () => GameManager.Instance.DialogueNext(),
            new Vector2(1, 0), new Vector2(-150, 50), new Vector2(220, 66));
        MakeButton(_dialogueRoot.transform, "dlg.end", 28, () => GameManager.Instance.EndDialogue(),
            new Vector2(0, 0), new Vector2(200, 50), new Vector2(240, 66));

        _dialogueRoot.SetActive(false);
    }

    void BuildCamera()
    {
        // 先从外壳贴图量出真实取景开口，让截图矩形与开口严格重合。
        ResolveViewfinderMetrics();

        // 完全不压暗，保证取景开口里看到的亮度/颜色和拍出的照片一致（所见即所拍）。
        _cameraRoot = MakePanel(_canvas.transform, "Camera", new Color(0f, 0f, 0f, 0f));
        SetRect(_cameraRoot.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        // 相机单元：外壳 + 取景开口 + 手，作为一个整体跟随鼠标移动。
        var unitGO = new GameObject("CameraUnit", typeof(RectTransform));
        unitGO.transform.SetParent(_cameraRoot.transform, false);
        _cameraUnit = unitGO.GetComponent<RectTransform>();
        SetRect(_cameraUnit, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        // 数码傻瓜相机外壳：中间取景开口已是透明，游戏场景从中透出。
        var cameraBodyGO = new GameObject("DigitalCameraBody", typeof(RectTransform));
        cameraBodyGO.transform.SetParent(_cameraUnit, false);
        _cameraBody = cameraBodyGO.AddComponent<Image>();
        _cameraBody.sprite = GeneratedArt.DigitalCameraOverlaySprite;
        // 精确铺满 BodyW×BodyH（外壳原生比例 1.5 与 913:609≈1.499 几乎一致，
        // 关掉 preserveAspect 避免亚像素黑边，使开口与量取的矩形严格对应）。
        _cameraBody.preserveAspect = false;
        _cameraBody.raycastTarget = false;
        SetRect(_cameraBody.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(BodyW, BodyH));

        // 取景框 = 外壳透明开口（截图区域与之完全一致，做到所见即所拍）。
        var vfGO = new GameObject("Viewfinder", typeof(RectTransform));
        vfGO.transform.SetParent(_cameraUnit, false);
        _viewfinder = vfGO.GetComponent<RectTransform>();
        SetRect(_viewfinder, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), _vfOffset, new Vector2(_vfW, _vfH));
        MakeCrosshair(_viewfinder, new Color(1f, 0.95f, 0.4f, 0.9f));

        // 手放在相机顶部快门上，点击/按空格时向下压。随相机整体移动。
        // 快门按钮在外壳约 (230, 258)（相对外壳中心），手指尖落在按钮上方。
        var hand = MakeIcon(_cameraUnit, "ShutterHand", GeneratedArt.CameraShutterHandSprite,
            new Vector2(0.5f, 0.5f), new Vector2(210f, 140f), new Vector2(300f, 300f));
        hand.raycastTarget = false;
        _shutterHand = hand.rectTransform;
        _shutterHandRestPosition = _shutterHand.anchoredPosition;

        // 顶部固定提示（不随相机移动）
        var tip = MakeLocText(_cameraRoot.transform, "Tip", "cam.tip", 28, TextAnchor.UpperCenter);
        SetRect(tip.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(1700, 46));

        _framedText = MakeLocText(_cameraRoot.transform, "Framed", "cam.framedNone", 30, TextAnchor.UpperCenter);
        SetRect(_framedText.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -72), new Vector2(1600, 46));
        _framedText.color = new Color(0.7f, 1f, 1f);

        // 取景状态 REC 指示（左上角，闪烁）
        var recDotGO = new GameObject("RecDot", typeof(RectTransform));
        recDotGO.transform.SetParent(_cameraRoot.transform, false);
        _recDot = recDotGO.AddComponent<Image>();
        _recDot.sprite = GeneratedArt.RecDotSprite;
        _recDot.color = new Color(1f, 0.25f, 0.25f, 1f);
        _recDot.raycastTarget = false;
        SetRect(_recDot.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, -60), new Vector2(28, 28));
        _recLabel = MakeText(_cameraRoot.transform, "RecLabel", "REC", 28, TextAnchor.MiddleLeft);
        _recLabel.color = new Color(1f, 0.35f, 0.35f);
        SetRect(_recLabel.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(88, -60), new Vector2(140, 32));

        // 底部操作栏（键位已标注；相机模式下鼠标用于瞄准，操作请用键盘）
        MakeButton(_cameraRoot.transform, "cam.peace", 28, () => GameManager.Instance.OnCameraPose(false),
            new Vector2(0.5f, 0), new Vector2(-500, 70), new Vector2(210, 74));
        MakeButton(_cameraRoot.transform, "cam.smile", 28, () => GameManager.Instance.OnCameraPose(true),
            new Vector2(0.5f, 0), new Vector2(-280, 70), new Vector2(210, 74));
        var shutter = MakeButton(_cameraRoot.transform, "cam.shutter", 30, () => GameManager.Instance.OnShutter(),
            new Vector2(0.5f, 0), new Vector2(0, 70), new Vector2(260, 74));
        SetButtonColor(shutter, new Color(0.8f, 0.3f, 0.3f));
        MakeButton(_cameraRoot.transform, "cam.exit", 28, () => GameManager.Instance.CloseCamera(),
            new Vector2(0.5f, 0), new Vector2(300, 70), new Vector2(220, 74));

        _cameraRoot.SetActive(false);
    }

    /// <summary>
    /// 从相机外壳贴图中央的透明窟窿量取真实取景开口，换算成画布单位写入
    /// _vfW / _vfH / _vfOffset。这样截图裁剪矩形与玩家透过的开口严格重合。
    /// 贴图不可读或量取异常时保留实测回退值。
    /// </summary>
    void ResolveViewfinderMetrics()
    {
        var sprite = GeneratedArt.DigitalCameraOverlaySprite;
        var tex = sprite != null ? sprite.texture : null;
        if (tex == null || !tex.isReadable) return;

        try
        {
            int tw = tex.width, th = tex.height;
            Color32[] px = tex.GetPixels32();      // 索引 0 = 左下角，行优先，y 向上
            const byte AThresh = 40;

            int cx = tw / 2, cy = th / 2;
            if (px[cy * tw + cx].a >= AThresh) return; // 中心不透明，说明假设不成立，回退

            var visited = new bool[tw * th];
            var stack = new Stack<int>();
            int start = cy * tw + cx;
            visited[start] = true;
            stack.Push(start);

            int minx = cx, maxx = cx, miny = cy, maxy = cy;
            while (stack.Count > 0)
            {
                int idx = stack.Pop();
                int x = idx % tw, y = idx / tw;
                if (x < minx) minx = x;
                if (x > maxx) maxx = x;
                if (y < miny) miny = y;
                if (y > maxy) maxy = y;

                if (x > 0)      TryPushTransparent(px, visited, stack, idx - 1, AThresh);
                if (x < tw - 1) TryPushTransparent(px, visited, stack, idx + 1, AThresh);
                if (y > 0)      TryPushTransparent(px, visited, stack, idx - tw, AThresh);
                if (y < th - 1) TryPushTransparent(px, visited, stack, idx + tw, AThresh);
            }

            float holeW = maxx - minx + 1;
            float holeH = maxy - miny + 1;
            float centerX = (minx + maxx + 1) * 0.5f;          // 像素，x 向右
            float centerYUp = (miny + maxy + 1) * 0.5f;        // 像素，y 向上

            _vfW = holeW / tw * BodyW;
            _vfH = holeH / th * BodyH;
            _vfOffset = new Vector2(
                (centerX / tw - 0.5f) * BodyW,
                (centerYUp / th - 0.5f) * BodyH);
        }
        catch
        {
            // 任意异常都保留回退常量
        }
    }

    static void TryPushTransparent(Color32[] px, bool[] visited, Stack<int> stack, int idx, byte aThresh)
    {
        if (!visited[idx] && px[idx].a < aThresh)
        {
            visited[idx] = true;
            stack.Push(idx);
        }
    }

    void BuildAlbum()
    {
        _albumRoot = MakePanel(_canvas.transform, "Album", new Color(0.05f, 0.06f, 0.09f, 0.97f));
        SetRect(_albumRoot.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        _albumTitle = MakeText(_albumRoot.transform, "ATitle", "", 34, TextAnchor.UpperCenter);
        SetRect(_albumTitle.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(1400, 50));

        var thumbGO = new GameObject("ThumbRow", typeof(RectTransform));
        thumbGO.transform.SetParent(_albumRoot.transform, false);
        _thumbRow = thumbGO.transform;
        SetRect(thumbGO.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -92), new Vector2(-80, 130));

        // 固定尺寸的照片框；照片用 AspectRatioFitter 按真实宽高比适配（所见即所拍，不拉伸）
        var frameGO = MakePanel(_albumRoot.transform, "PhotoFrame", new Color(0.02f, 0.02f, 0.03f, 1f));
        SetRect(frameGO.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(1180, 620));

        _albumBigImage = MakeRawImage(frameGO.transform, "BigPhoto",
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180, 620));
        _albumBigImage.color = Color.white;
        _albumFitter = _albumBigImage.gameObject.AddComponent<AspectRatioFitter>();
        _albumFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        _albumFitter.aspectRatio = _vfW / _vfH;

        _albumCaption = MakeText(_albumRoot.transform, "ACaption", "", 26, TextAnchor.UpperCenter);
        SetRect(_albumCaption.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 118), new Vector2(1180, 44));
        _albumCaption.color = new Color(0.8f, 0.9f, 1f);

        _albumHint = MakeText(_albumRoot.transform, "AHint", "", 30, TextAnchor.MiddleCenter);
        SetRect(_albumHint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 80));

        MakeButton(_albumRoot.transform, "album.close", 30, () => GameManager.Instance.CloseAlbum(),
            new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(280, 70));

        _albumRoot.SetActive(false);
    }

    void BuildMarkList()
    {
        _markListRoot = MakePanel(_canvas.transform, "MarkList", new Color(0.05f, 0.06f, 0.09f, 0.97f));
        SetRect(_markListRoot.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        var title = MakeLocText(_markListRoot.transform, "MTitle", "mark.title", 40, TextAnchor.UpperCenter);
        SetRect(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(1500, 60));
        title.color = new Color(1f, 0.85f, 0.5f);

        var sub = MakeLocText(_markListRoot.transform, "MSub", "mark.sub", 26, TextAnchor.UpperCenter);
        SetRect(sub.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(1500, 44));
        sub.color = new Color(0.8f, 0.85f, 0.95f);

        // 已标记名单容器（动态生成每一行）
        var listGO = new GameObject("MarkContainer", typeof(RectTransform));
        listGO.transform.SetParent(_markListRoot.transform, false);
        _markListContainer = listGO.transform;
        SetRect(listGO.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -180), new Vector2(900, 500));

        _markListHint = MakeText(_markListRoot.transform, "MHint", "", 30, TextAnchor.MiddleCenter);
        SetRect(_markListHint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1100, 120));
        _markListHint.color = new Color(0.75f, 0.8f, 0.9f);

        _markSubmitBtn = MakeButton(_markListRoot.transform, "mark.submit", 34, () => GameManager.Instance.RequestSubmit(),
            new Vector2(0.5f, 0), new Vector2(-180, 50), new Vector2(320, 82));
        SetButtonColor(_markSubmitBtn, new Color(0.75f, 0.3f, 0.3f));
        MakeButton(_markListRoot.transform, "mark.back", 30, () => GameManager.Instance.CloseMarkList(),
            new Vector2(0.5f, 0), new Vector2(180, 50), new Vector2(320, 82));

        // 提交确认弹窗（覆盖在列表之上）
        _submitConfirmRoot = MakePanel(_markListRoot.transform, "SubmitConfirm", new Color(0f, 0f, 0f, 0.75f));
        SetRect(_submitConfirmRoot.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        var box = MakePanel(_submitConfirmRoot.transform, "Box", new Color(0.1f, 0.12f, 0.16f, 1f));
        SetRect(box.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 560));

        var cTitle = MakeLocText(box.transform, "CTitle", "confirm.title", 40, TextAnchor.UpperCenter);
        SetRect(cTitle.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(940, 60));
        cTitle.color = new Color(1f, 0.85f, 0.5f);

        _submitConfirmText = MakeText(box.transform, "CText", "", 30, TextAnchor.UpperCenter);
        SetRect(_submitConfirmText.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(920, 340));

        var yes = MakeButton(box.transform, "confirm.yes", 32, () => GameManager.Instance.ConfirmSubmit(),
            new Vector2(0.5f, 0), new Vector2(-160, 40), new Vector2(280, 80));
        SetButtonColor(yes, new Color(0.75f, 0.3f, 0.3f));
        MakeButton(box.transform, "confirm.no", 32, () => GameManager.Instance.CancelSubmit(),
            new Vector2(0.5f, 0), new Vector2(160, 40), new Vector2(280, 80));

        _submitConfirmRoot.SetActive(false);
        _markListRoot.SetActive(false);
    }

    void BuildResult()
    {
        _resultRoot = MakePanel(_canvas.transform, "Result", new Color(0.04f, 0.05f, 0.08f, 0.97f));
        SetRect(_resultRoot.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        _resultTitle = MakeText(_resultRoot.transform, "RTitle", "", 70, TextAnchor.UpperCenter);
        SetRect(_resultTitle.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -80), new Vector2(1200, 110));

        _resultDetail = MakeText(_resultRoot.transform, "RDetail", "", 34, TextAnchor.UpperCenter);
        SetRect(_resultDetail.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -220), new Vector2(1300, 480));

        MakeButton(_resultRoot.transform, "result.replay", 34, () => GameManager.Instance.StartRound(),
            new Vector2(0.5f, 0), new Vector2(-180, 70), new Vector2(320, 80));
        MakeButton(_resultRoot.transform, "result.menu", 34, () => GameManager.Instance.EnterMainMenu(),
            new Vector2(0.5f, 0), new Vector2(180, 70), new Vector2(320, 80));

        _resultRoot.SetActive(false);
    }

    void BuildToast()
    {
        _toastText = MakeText(_canvas.transform, "Toast", "", 34, TextAnchor.MiddleCenter);
        SetRect(_toastText.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -180), new Vector2(1300, 80));
        _toastGroup = _toastText.gameObject.AddComponent<CanvasGroup>();
        _toastText.gameObject.SetActive(false);
    }

    // ---------------- HUD / 提示 ----------------

    public void SetHud(int film, int marked, int total)
    {
        _filmText.text = Loc.Format("hud.film", film);
        _filmText.color = film <= 2 ? new Color(1f, 0.4f, 0.4f) : Color.white;
        _foundText.text = Loc.Format("hud.marked", marked, total);

        if (_lastFilm != int.MinValue && film != _lastFilm) StartCoroutine(PunchText(_filmText.rectTransform));
        if (_lastMarked != int.MinValue && marked != _lastMarked) StartCoroutine(PunchText(_foundText.rectTransform));
        _lastFilm = film; _lastMarked = marked;
    }

    IEnumerator PunchText(RectTransform rt)
    {
        if (rt == null) yield break;
        float t = 0f, dur = 0.22f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            float s = 1f + 0.35f * Mathf.Sin(k * Mathf.PI);
            rt.localScale = Vector3.one * s;
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    public void SetInteractPrompt(Npc npc) => ShowInteract(npc);

    // ---------------- 对话 ----------------

    public void ShowDialogue(string name, string line)
    {
        bool wasActive = _dialogueRoot.activeSelf;
        _dialogueRoot.SetActive(true);
        _dialogueName.text = name;
        _dialogueLine.text = line;
        if (!wasActive) PlayShow(_dialogueRoot);
    }

    public void HideDialogue()
    {
        if (_dialogueRoot != null) _dialogueRoot.SetActive(false);
    }

    // ---------------- 相机 ----------------

    public void ShowCamera()
    {
        _cameraRoot.SetActive(true);
        PlayShow(_cameraRoot);
    }

    public void HideCamera()
    {
        if (_cameraRoot != null) _cameraRoot.SetActive(false);
    }

    /// <summary>拍照瞬间临时隐藏相机外壳/准星，只让场景入镜（抓屏后立即恢复）。</summary>
    public void SetCameraOverlayVisible(bool visible)
    {
        if (_cameraRoot != null) _cameraRoot.SetActive(visible);
    }

    /// <summary>数码相机的按快门手势：快速向下压，再回到待机位置。</summary>
    public void PlayShutterPress()
    {
        if (_shutterHand == null || !_cameraRoot.activeSelf) return;
        if (_shutterHandCo != null) StopCoroutine(_shutterHandCo);
        _shutterHandCo = StartCoroutine(ShutterHandPress());
    }

    IEnumerator ShutterHandPress()
    {
        _shutterHand.anchoredPosition = _shutterHandRestPosition + new Vector2(0f, -28f);
        yield return new WaitForSecondsRealtime(0.1f);
        _shutterHand.anchoredPosition = _shutterHandRestPosition;
        _shutterHandCo = null;
    }

    public void SetFramedChips(List<Npc> framed)
    {
        if (framed == null || framed.Count == 0)
        {
            _framedText.text = Loc.Get("cam.framedNone");
            return;
        }
        var names = new List<string>();
        foreach (var n in framed) names.Add(n.npcName);
        _framedText.text = Loc.Format("cam.framed", string.Join(Sep, names));
    }

    /// <summary>取景框在屏幕上的像素矩形（左下角为原点，与 ReadPixels 一致）。</summary>
    public Rect ViewfinderScreenRect
    {
        get
        {
            var corners = new Vector3[4];
            _viewfinder.GetWorldCorners(corners); // Overlay 画布下即屏幕像素
            float x = corners[0].x;
            float y = corners[0].y;
            float w = corners[2].x - corners[0].x;
            float h = corners[2].y - corners[0].y;
            return new Rect(x, y, w, h);
        }
    }

    void Update()
    {
        // 相机整体（外壳 + 开口 + 手）跟随鼠标，让取景开口中心对准鼠标。
        if (_cameraRoot != null && _cameraRoot.activeSelf && _cameraUnit != null)
        {
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRect, Input.mousePosition, null, out local))
            {
                // 夹取取景框中心，保证开口整体不出画布
                float maxX = _canvasRect.rect.width * 0.5f - _vfW * 0.5f;
                float maxY = _canvasRect.rect.height * 0.5f - _vfH * 0.5f;
                local.x = Mathf.Clamp(local.x, -maxX, maxX);
                local.y = Mathf.Clamp(local.y, -maxY, maxY);
                // 开口中心 = 单元位置 + _vfOffset，因此单元位置需减去偏移
                _cameraUnit.anchoredPosition = local - _vfOffset;
            }

            // REC 闪烁
            float blink = Mathf.PingPong(Time.unscaledTime * 1.6f, 1f);
            if (_recDot != null) _recDot.color = new Color(1f, 0.25f, 0.25f, 0.25f + 0.75f * blink);
            if (_recLabel != null) _recLabel.color = new Color(1f, 0.35f, 0.35f, 0.35f + 0.65f * blink);
        }
    }

    // ---------------- 相册 ----------------

    /// <summary>显示一组照片（title 用于区分“全部照片 / 某角色的照片”）。纯查看，不再在此指认。</summary>
    public void ShowAlbum(List<PhotoEntry> entries, string title)
    {
        _albumEntries = entries;
        _albumRoot.SetActive(true);
        PlayShow(_albumRoot);
        _albumTitle.text = title;
        ClearAlbumDynamic();

        if (entries == null || entries.Count == 0)
        {
            _selectedPhoto = -1;
            _albumBigImage.gameObject.SetActive(false);
            _albumCaption.text = "";
            _albumHint.text = Loc.Get("album.empty");
            return;
        }

        _albumHint.text = "";
        _albumBigImage.gameObject.SetActive(true);

        for (int i = 0; i < entries.Count; i++)
        {
            int index = i;
            var thumb = MakeRawImage(_thumbRow, "Thumb" + i,
                new Vector2(0, 0.5f), new Vector2(90 + i * 150, 0), new Vector2(140, 100));
            thumb.texture = entries[i].image;
            thumb.color = Color.white;
            var btn = thumb.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(() => SelectPhoto(index));
            _albumDynamic.Add(thumb.gameObject);
        }

        SelectPhoto(entries.Count - 1);
    }

    void SelectPhoto(int index)
    {
        if (_albumEntries == null || index < 0 || index >= _albumEntries.Count) return;
        _selectedPhoto = index;
        var entry = _albumEntries[index];
        _albumBigImage.texture = entry.image;
        _albumBigImage.color = Color.white;
        if (entry.image != null && entry.image.height > 0)
            _albumFitter.aspectRatio = entry.image.width / (float)entry.image.height;

        var names = new List<string>();
        foreach (var n in entry.framed) if (n != null) names.Add(n.npcName);
        _albumCaption.text = names.Count > 0 ? Loc.Format("album.inphoto", string.Join(Sep, names)) : "";
    }

    void ClearAlbumDynamic()
    {
        foreach (var go in _albumDynamic)
            if (go != null) Destroy(go);
        _albumDynamic.Clear();
    }

    public void HideAlbum()
    {
        if (_albumRoot != null) _albumRoot.SetActive(false);
        ClearAlbumDynamic();
    }

    // ---------------- 指认列表 / 提交确认 ----------------

    public void ShowMarkList(List<Npc> marked)
    {
        _markListRoot.SetActive(true);
        PlayShow(_markListRoot);
        _submitConfirmRoot.SetActive(false);
        ClearMarkDynamic();

        bool any = marked != null && marked.Count > 0;
        _markListHint.text = any ? "" : Loc.Get("mark.empty");
        _markSubmitBtn.interactable = any;

        if (!any) return;

        for (int i = 0; i < marked.Count; i++)
        {
            var npc = marked[i];
            var row = MakePanel(_markListContainer, "Row" + i, new Color(0.12f, 0.14f, 0.19f, 1f));
            SetRect(row.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -i * 72f), new Vector2(900, 62));

            var name = MakeText(row.transform, "N", $"{i + 1}.  {npc.npcName}", 30, TextAnchor.MiddleLeft);
            SetRect(name.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(560, 60));

            var npcRef = npc;
            var rm = MakeButton(row.transform, Loc.Get("mark.remove"), 24, () => GameManager.Instance.UnmarkFromList(npcRef),
                new Vector2(1, 0.5f), new Vector2(-130, 0), new Vector2(200, 50), register: false);
            SetButtonColor(rm, new Color(0.4f, 0.45f, 0.55f));

            _markDynamic.Add(row);
        }
    }

    public void HideMarkList()
    {
        if (_markListRoot != null) _markListRoot.SetActive(false);
        if (_submitConfirmRoot != null) _submitConfirmRoot.SetActive(false);
        ClearMarkDynamic();
    }

    public void ShowSubmitConfirm(List<Npc> marked)
    {
        var names = new List<string>();
        if (marked != null) foreach (var n in marked) if (n != null) names.Add(n.npcName);

        string body = names.Count > 0
            ? Loc.Format("confirm.body", names.Count, string.Join(Sep, names))
            : Loc.Get("confirm.bodyEmpty");
        _submitConfirmText.text = body;
        _submitConfirmRoot.SetActive(true);
        PlayShow(_submitConfirmRoot);
    }

    public void HideSubmitConfirm()
    {
        if (_submitConfirmRoot != null) _submitConfirmRoot.SetActive(false);
    }

    void ClearMarkDynamic()
    {
        foreach (var go in _markDynamic)
            if (go != null) Destroy(go);
        _markDynamic.Clear();
    }

    // ---------------- 结算 / Toast / 统一隐藏 ----------------

    public void ShowResult(bool win, int correct, int wrong, int total, List<string> imposters, int photos)
    {
        _resultRoot.SetActive(true);
        PlayShow(_resultRoot);
        _resultTitle.text = Loc.Get(win ? "result.win" : "result.lose");
        _resultTitle.color = win ? new Color(0.6f, 1f, 0.6f) : new Color(1f, 0.75f, 0.55f);

        string imposterList = (imposters != null && imposters.Count > 0)
            ? string.Join("\n", imposters) : Loc.Get("result.none");
        _resultDetail.text = Loc.Format("result.detail", correct, total, wrong, photos, imposterList);
    }

    public void HideAllPanels()
    {
        HideDialogue();
        HideCamera();
        HideAlbum();
        HideMarkList();
        if (_resultRoot != null) _resultRoot.SetActive(false);
        if (_interactRoot != null) _interactRoot.SetActive(false);
        if (_menuRoot != null) _menuRoot.SetActive(false);
        if (_creditsRoot != null) _creditsRoot.SetActive(false);
    }

    public void ShowToast(string msg, bool positive)
    {
        _toastText.text = msg;
        _toastText.color = positive ? new Color(0.75f, 1f, 0.75f) : new Color(1f, 0.6f, 0.6f);
        _toastText.gameObject.SetActive(true);
        if (_toastCo != null) StopCoroutine(_toastCo);
        _toastCo = StartCoroutine(ToastRoutine(2.2f));
    }

    IEnumerator ToastRoutine(float hold)
    {
        var rt = _toastText.rectTransform;
        Vector2 baseP = new Vector2(0, -180);
        // 滑入 + 淡入
        float t = 0f;
        while (t < 0.18f)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / 0.18f);
            float e = 1f - (1f - k) * (1f - k);
            _toastGroup.alpha = e;
            rt.anchoredPosition = baseP + new Vector2(0, 30f * (1f - e));
            yield return null;
        }
        _toastGroup.alpha = 1f;
        rt.anchoredPosition = baseP;

        yield return new WaitForSecondsRealtime(hold);

        // 淡出
        t = 0f;
        while (t < 0.25f)
        {
            t += Time.unscaledDeltaTime;
            _toastGroup.alpha = 1f - Mathf.Clamp01(t / 0.25f);
            yield return null;
        }
        _toastGroup.alpha = 0f;
        _toastText.gameObject.SetActive(false);
    }

    // ---------------- 通用动效工具 ----------------

    /// <summary>面板出现时淡入 + 轻微放大。</summary>
    void PlayShow(GameObject panel)
    {
        if (panel == null) return;
        var cg = panel.GetComponent<CanvasGroup>();
        if (cg == null) cg = panel.AddComponent<CanvasGroup>();
        StartCoroutine(ShowRoutine(cg, panel.transform as RectTransform));
    }

    IEnumerator ShowRoutine(CanvasGroup cg, RectTransform rt)
    {
        float t = 0f;
        Vector3 baseScale = Vector3.one;
        while (t < 0.16f)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / 0.16f);
            float e = 1f - (1f - k) * (1f - k);
            cg.alpha = e;
            if (rt != null) rt.localScale = baseScale * (0.96f + 0.04f * e);
            yield return null;
        }
        cg.alpha = 1f;
        if (rt != null) rt.localScale = baseScale;
    }

    /// <summary>回合开始的黑场淡入（从全黑到亮）。</summary>
    public void PlayFadeIn()
    {
        if (_fadeGroup == null) return;
        if (_fadeCo != null) StopCoroutine(_fadeCo);
        _fadeCo = StartCoroutine(FadeRoutine(1f, 0f, 0.45f));
    }

    IEnumerator FadeRoutine(float from, float to, float dur)
    {
        _fadeGroup.alpha = from;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            _fadeGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / dur));
            yield return null;
        }
        _fadeGroup.alpha = to;
    }

    /// <summary>拍照后把刚拍的照片从取景框飞入屏幕角落。</summary>
    public void PlayPhotoFly(Texture2D photo, Rect viewfinderScreenRect)
    {
        if (photo == null || _canvasRect == null) return;
        StartCoroutine(PhotoFlyRoutine(photo, viewfinderScreenRect));
    }

    IEnumerator PhotoFlyRoutine(Texture2D photo, Rect vf)
    {
        // 起点：取景框中心（屏幕像素 -> 画布本地坐标）
        Vector2 centerPx = new Vector2(vf.x + vf.width * 0.5f, vf.y + vf.height * 0.5f);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, centerPx, null, out var startLocal);

        float ratio = _canvasRect.rect.width / Mathf.Max(1, Screen.width);
        Vector2 startSize = new Vector2(vf.width, vf.height) * ratio;

        var go = new GameObject("PhotoFly", typeof(RectTransform));
        go.transform.SetParent(_canvas.transform, false);
        var raw = go.AddComponent<RawImage>();
        raw.texture = photo;
        raw.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = startSize;
        rt.anchoredPosition = startLocal;

        // 终点：右下角
        Vector2 endLocal = new Vector2(_canvasRect.rect.width * 0.5f - 130f, -_canvasRect.rect.height * 0.5f + 110f);
        Vector2 endSize = startSize * 0.22f;

        float t = 0f, dur = 0.5f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            float e = 1f - Mathf.Pow(1f - k, 3f); // easeOutCubic
            rt.anchoredPosition = Vector2.Lerp(startLocal, endLocal, e);
            rt.sizeDelta = Vector2.Lerp(startSize, endSize, e);
            rt.localRotation = Quaternion.Euler(0, 0, -12f * e);
            raw.color = new Color(1, 1, 1, 1f - 0.85f * Mathf.Clamp01((k - 0.6f) / 0.4f));
            yield return null;
        }
        Destroy(go);
    }

    // ---------------- UI 构建小工具 ----------------

    Text MakeText(Transform parent, string name, string content, int size, TextAnchor anchor)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = _font;
        t.text = content;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.supportRichText = true;
        return t;
    }

    GameObject MakePanel(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.AddComponent<Image>().color = color;
        return go;
    }

    Image MakeIcon(Transform parent, string name, Sprite sprite, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        SetRect(go.GetComponent<RectTransform>(), anchor, anchor, anchor, pos, size);
        return image;
    }

    RawImage MakeRawImage(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<RawImage>();
        SetRect(go.GetComponent<RectTransform>(), anchor, anchor, anchor, pos, size);
        return img;
    }

    void MakeCrosshair(Transform parent, Color color)
    {
        var h = new GameObject("CrossH", typeof(RectTransform));
        h.transform.SetParent(parent, false);
        var hi = h.AddComponent<Image>(); hi.color = color; hi.raycastTarget = false;
        SetRect(h.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26, 3));
        var v = new GameObject("CrossV", typeof(RectTransform));
        v.transform.SetParent(parent, false);
        var vi = v.AddComponent<Image>(); vi.color = color; vi.raycastTarget = false;
        SetRect(v.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3, 26));
    }

    /// <summary>
    /// 创建按钮。register=true 时把 key 当作本地化键（label 取 Loc.Get(key) 并登记，语言切换时自动重刷）；
    /// register=false 时把 key 直接当作静态/自管理文本（如“Language: 中文”这种自己刷新的）。
    /// </summary>
    Button MakeButton(Transform parent, string key, int size, UnityAction onClick,
                      Vector2 anchor, Vector2 anchoredPos, Vector2 sizeDelta, bool register = true)
    {
        var go = new GameObject("Btn_" + key, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.25f, 0.28f, 0.36f, 0.98f);
        var btn = go.AddComponent<Button>();
        btn.onClick.AddListener(onClick);
        go.AddComponent<UIButtonFeedback>();
        SetRect(go.GetComponent<RectTransform>(), anchor, anchor, anchor, anchoredPos, sizeDelta);

        var t = MakeText(go.transform, "Label", register ? Loc.Get(key) : key, size, TextAnchor.MiddleCenter);
        SetRect(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        if (register) _locStatic.Add((t, key));
        return btn;
    }

    /// <summary>创建带本地化键的静态文本（语言切换时自动重刷）。</summary>
    Text MakeLocText(Transform parent, string name, string key, int size, TextAnchor anchor)
    {
        var t = MakeText(parent, name, Loc.Get(key), size, anchor);
        _locStatic.Add((t, key));
        return t;
    }

    /// <summary>当前语言的名字连接符。</summary>
    string Sep => Loc.Current == Lang.ZH ? "、" : ", ";

    /// <summary>语言切换后重刷所有已登记的静态文案，并让 GameManager 刷新动态部分。</summary>
    public void Relocalize()
    {
        foreach (var e in _locStatic)
            if (e.text != null) e.text.text = Loc.Get(e.key);
        if (_menuLangLabel != null) _menuLangLabel.text = Loc.Format("menu.language", Loc.LanguageName);
        if (GameManager.Instance != null) GameManager.Instance.OnLanguageChanged();
    }

    void OnDestroy() { Loc.OnChanged -= Relocalize; }

    void SetButtonColor(Button btn, Color c) => btn.GetComponent<Image>().color = c;

    static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
                        Vector2 anchoredPos, Vector2 sizeDelta)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
    }
}
