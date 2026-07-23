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

    // 相机外壳开口测量值（digital_camera_overlay.png：开口约占外壳 89.8% 宽、79.8% 高，几乎居中、略偏上）
    const float VfW = 820f;                 // 取景框/开口宽（画布参考单位）
    const float VfH = 486f;                 // 取景框/开口高（≈ VfW / 1.688 保持开口比例）
    const float BodyW = 913f;               // 外壳宽 = VfW / 0.898
    const float BodyH = 609f;               // 外壳高 = VfH / 0.798
    static readonly Vector2 VfOffset = new Vector2(0f, 8f); // 开口中心相对外壳中心的偏移

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
    Coroutine _toastCo;

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

        MakeIcon(hud, "FilmIcon", GeneratedArt.GetIconSprite(0),
            new Vector2(0, 1), new Vector2(28, -28), new Vector2(46, 46));
        _filmText = MakeText(hud, "Film", "胶卷 10", 36, TextAnchor.UpperLeft);
        SetRect(_filmText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(86, -26), new Vector2(400, 60));

        MakeIcon(hud, "FoundIcon", GeneratedArt.GetIconSprite(6),
            new Vector2(1, 1), new Vector2(-250, -28), new Vector2(44, 44));
        _foundText = MakeText(hud, "Found", "伪人 0/3", 36, TextAnchor.UpperRight);
        SetRect(_foundText.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
            new Vector2(-30, -26), new Vector2(400, 60));

        _promptText = MakeText(hud, "Prompt", "移动 [WASD]　拍照 [空格]　相册 [Tab]　指认列表 [M]", 26, TextAnchor.LowerCenter);
        SetRect(_promptText.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
            new Vector2(0, 24), new Vector2(1100, 44));
        _promptText.color = new Color(1f, 1f, 1f, 0.6f);

        MakeButton(hud, "相机 (空格)", 30, () => GameManager.Instance.OpenCamera(),
            new Vector2(1, 0), new Vector2(-150, 210), new Vector2(240, 70));
        MakeButton(hud, "相册 (Tab)", 30, () => GameManager.Instance.OpenAlbum(),
            new Vector2(1, 0), new Vector2(-150, 130), new Vector2(240, 70));
        var markListBtn = MakeButton(hud, "指认列表 (M)", 28, () => GameManager.Instance.OpenMarkList(),
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

        MakeButton(_interactRoot.transform, "交谈 [E]", 26, () => GameManager.Instance.TalkNearest(),
            new Vector2(0.5f, 0), new Vector2(-250, 22), new Vector2(220, 64));
        MakeButton(_interactRoot.transform, "查看照片 [Q]", 26, () => GameManager.Instance.ViewNearestPhotos(),
            new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(220, 64));
        _interactMarkBtn = MakeButton(_interactRoot.transform, "标记嫌疑人 [F]", 24, () => GameManager.Instance.ToggleMarkNearest(),
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
        _interactRoot.SetActive(true);
        _interactName.text = npc.marked ? npc.npcName + "（已标记）" : npc.npcName;
        if (_interactMarkLabel != null)
            _interactMarkLabel.text = npc.marked ? "取消标记 [F]" : "标记嫌疑人 [F]";
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

        MakeButton(_dialogueRoot.transform, "继续 ▶", 30, () => GameManager.Instance.DialogueNext(),
            new Vector2(1, 0), new Vector2(-150, 50), new Vector2(220, 66));
        MakeButton(_dialogueRoot.transform, "结束对话", 28, () => GameManager.Instance.EndDialogue(),
            new Vector2(0, 0), new Vector2(150, 50), new Vector2(200, 66));

        _dialogueRoot.SetActive(false);
    }

    void BuildCamera()
    {
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
        _cameraBody.preserveAspect = true;
        _cameraBody.raycastTarget = false;
        SetRect(_cameraBody.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(BodyW, BodyH));

        // 取景框 = 外壳透明开口（截图区域与之完全一致，做到所见即所拍）。
        var vfGO = new GameObject("Viewfinder", typeof(RectTransform));
        vfGO.transform.SetParent(_cameraUnit, false);
        _viewfinder = vfGO.GetComponent<RectTransform>();
        SetRect(_viewfinder, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), VfOffset, new Vector2(VfW, VfH));
        MakeCrosshair(_viewfinder, new Color(1f, 0.95f, 0.4f, 0.9f));

        // 手放在相机顶部快门上，点击/按空格时向下压。随相机整体移动。
        // 快门按钮在外壳约 (230, 258)（相对外壳中心），手指尖落在按钮上方。
        var hand = MakeIcon(_cameraUnit, "ShutterHand", GeneratedArt.CameraShutterHandSprite,
            new Vector2(0.5f, 0.5f), new Vector2(210f, 140f), new Vector2(300f, 300f));
        hand.raycastTarget = false;
        _shutterHand = hand.rectTransform;
        _shutterHandRestPosition = _shutterHand.anchoredPosition;

        // 顶部固定提示（不随相机移动）
        var tip = MakeText(_cameraRoot.transform, "Tip", "移动鼠标瞄准；[1] 比耶　[2] 笑　[空格] 拍照　[Esc] 退出", 28, TextAnchor.UpperCenter);
        SetRect(tip.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(1600, 46));

        _framedText = MakeText(_cameraRoot.transform, "Framed", "在镜头中：（无）", 30, TextAnchor.UpperCenter);
        SetRect(_framedText.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -72), new Vector2(1600, 46));
        _framedText.color = new Color(0.7f, 1f, 1f);

        // 底部操作栏（键位已标注；相机模式下鼠标用于瞄准，操作请用键盘）
        MakeButton(_cameraRoot.transform, "比个耶 [1]", 28, () => GameManager.Instance.OnCameraPose(false),
            new Vector2(0.5f, 0), new Vector2(-500, 70), new Vector2(210, 74));
        MakeButton(_cameraRoot.transform, "笑一下 [2]", 28, () => GameManager.Instance.OnCameraPose(true),
            new Vector2(0.5f, 0), new Vector2(-280, 70), new Vector2(210, 74));
        var shutter = MakeButton(_cameraRoot.transform, "快门 [空格]", 30, () => GameManager.Instance.OnShutter(),
            new Vector2(0.5f, 0), new Vector2(0, 70), new Vector2(260, 74));
        SetButtonColor(shutter, new Color(0.8f, 0.3f, 0.3f));
        MakeButton(_cameraRoot.transform, "退出 [Esc]", 28, () => GameManager.Instance.CloseCamera(),
            new Vector2(0.5f, 0), new Vector2(300, 70), new Vector2(220, 74));

        _cameraRoot.SetActive(false);
    }

    void BuildAlbum()
    {
        _albumRoot = MakePanel(_canvas.transform, "Album", new Color(0.05f, 0.06f, 0.09f, 0.97f));
        SetRect(_albumRoot.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        _albumTitle = MakeText(_albumRoot.transform, "ATitle", "相册", 34, TextAnchor.UpperCenter);
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
        _albumFitter.aspectRatio = 1.687f;

        _albumCaption = MakeText(_albumRoot.transform, "ACaption", "", 26, TextAnchor.UpperCenter);
        SetRect(_albumCaption.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 118), new Vector2(1180, 44));
        _albumCaption.color = new Color(0.8f, 0.9f, 1f);

        _albumHint = MakeText(_albumRoot.transform, "AHint", "", 30, TextAnchor.MiddleCenter);
        SetRect(_albumHint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 80));

        MakeButton(_albumRoot.transform, "关闭 (Esc)", 30, () => GameManager.Instance.CloseAlbum(),
            new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(240, 70));

        _albumRoot.SetActive(false);
    }

    void BuildMarkList()
    {
        _markListRoot = MakePanel(_canvas.transform, "MarkList", new Color(0.05f, 0.06f, 0.09f, 0.97f));
        SetRect(_markListRoot.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        var title = MakeText(_markListRoot.transform, "MTitle", "指认列表 —— 你标记的嫌疑人", 40, TextAnchor.UpperCenter);
        SetRect(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(1400, 60));
        title.color = new Color(1f, 0.85f, 0.5f);

        var sub = MakeText(_markListRoot.transform, "MSub", "确认无误后点击“提交指认”。注意：提交后本局立即结束。", 26, TextAnchor.UpperCenter);
        SetRect(sub.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(1400, 44));
        sub.color = new Color(0.8f, 0.85f, 0.95f);

        // 已标记名单容器（动态生成每一行）
        var listGO = new GameObject("MarkContainer", typeof(RectTransform));
        listGO.transform.SetParent(_markListRoot.transform, false);
        _markListContainer = listGO.transform;
        SetRect(listGO.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -180), new Vector2(900, 500));

        _markListHint = MakeText(_markListRoot.transform, "MHint", "", 30, TextAnchor.MiddleCenter);
        SetRect(_markListHint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1100, 120));
        _markListHint.color = new Color(0.75f, 0.8f, 0.9f);

        _markSubmitBtn = MakeButton(_markListRoot.transform, "提交指认", 34, () => GameManager.Instance.RequestSubmit(),
            new Vector2(0.5f, 0), new Vector2(-170, 50), new Vector2(300, 82));
        SetButtonColor(_markSubmitBtn, new Color(0.75f, 0.3f, 0.3f));
        MakeButton(_markListRoot.transform, "返回场景 [Esc]", 30, () => GameManager.Instance.CloseMarkList(),
            new Vector2(0.5f, 0), new Vector2(170, 50), new Vector2(300, 82));

        // 提交确认弹窗（覆盖在列表之上）
        _submitConfirmRoot = MakePanel(_markListRoot.transform, "SubmitConfirm", new Color(0f, 0f, 0f, 0.75f));
        SetRect(_submitConfirmRoot.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        var box = MakePanel(_submitConfirmRoot.transform, "Box", new Color(0.1f, 0.12f, 0.16f, 1f));
        SetRect(box.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 560));

        var cTitle = MakeText(box.transform, "CTitle", "确认提交指认", 40, TextAnchor.UpperCenter);
        SetRect(cTitle.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(940, 60));
        cTitle.color = new Color(1f, 0.85f, 0.5f);

        _submitConfirmText = MakeText(box.transform, "CText", "", 30, TextAnchor.UpperCenter);
        SetRect(_submitConfirmText.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(920, 340));

        var yes = MakeButton(box.transform, "确认提交", 32, () => GameManager.Instance.ConfirmSubmit(),
            new Vector2(0.5f, 0), new Vector2(-160, 40), new Vector2(280, 80));
        SetButtonColor(yes, new Color(0.75f, 0.3f, 0.3f));
        MakeButton(box.transform, "再想想", 32, () => GameManager.Instance.CancelSubmit(),
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

        MakeButton(_resultRoot.transform, "再玩一次", 34, () => GameManager.Instance.StartRound(),
            new Vector2(0.5f, 0), new Vector2(0, 70), new Vector2(320, 80));

        _resultRoot.SetActive(false);
    }

    void BuildToast()
    {
        _toastText = MakeText(_canvas.transform, "Toast", "", 34, TextAnchor.MiddleCenter);
        SetRect(_toastText.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -180), new Vector2(1300, 80));
        _toastText.gameObject.SetActive(false);
    }

    // ---------------- HUD / 提示 ----------------

    public void SetHud(int film, int marked, int total)
    {
        _filmText.text = "胶卷 " + film;
        _filmText.color = film <= 2 ? new Color(1f, 0.4f, 0.4f) : Color.white;
        _foundText.text = $"已标记 {marked}（伪人共 {total}）";
    }

    public void SetInteractPrompt(Npc npc) => ShowInteract(npc);

    // ---------------- 对话 ----------------

    public void ShowDialogue(string name, string line)
    {
        _dialogueRoot.SetActive(true);
        _dialogueName.text = name;
        _dialogueLine.text = line;
    }

    public void HideDialogue()
    {
        if (_dialogueRoot != null) _dialogueRoot.SetActive(false);
    }

    // ---------------- 相机 ----------------

    public void ShowCamera()
    {
        _cameraRoot.SetActive(true);
    }

    public void HideCamera()
    {
        if (_cameraRoot != null) _cameraRoot.SetActive(false);
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
            _framedText.text = "在镜头中：（无）";
            return;
        }
        var names = new List<string>();
        foreach (var n in framed) names.Add(n.npcName);
        _framedText.text = "在镜头中：" + string.Join("、", names);
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
                float maxX = _canvasRect.rect.width * 0.5f - VfW * 0.5f;
                float maxY = _canvasRect.rect.height * 0.5f - VfH * 0.5f;
                local.x = Mathf.Clamp(local.x, -maxX, maxX);
                local.y = Mathf.Clamp(local.y, -maxY, maxY);
                // 开口中心 = 单元位置 + VfOffset，因此单元位置需减去偏移
                _cameraUnit.anchoredPosition = local - VfOffset;
            }
        }
    }

    // ---------------- 相册 ----------------

    /// <summary>显示一组照片（title 用于区分“全部照片 / 某角色的照片”）。纯查看，不再在此指认。</summary>
    public void ShowAlbum(List<PhotoEntry> entries, string title)
    {
        _albumEntries = entries;
        _albumRoot.SetActive(true);
        _albumTitle.text = title;
        ClearAlbumDynamic();

        if (entries == null || entries.Count == 0)
        {
            _selectedPhoto = -1;
            _albumBigImage.gameObject.SetActive(false);
            _albumCaption.text = "";
            _albumHint.text = "还没有照片，先用相机拍几张吧。";
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
        _albumCaption.text = names.Count > 0 ? "照片里：" + string.Join("、", names) : "";
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
        _submitConfirmRoot.SetActive(false);
        ClearMarkDynamic();

        bool any = marked != null && marked.Count > 0;
        _markListHint.text = any ? "" : "你还没有标记任何人。\n回到场景靠近 NPC 按 [F] 标记你怀疑的对象。";
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
            var rm = MakeButton(row.transform, "移除标记", 24, () => GameManager.Instance.UnmarkFromList(npcRef),
                new Vector2(1, 0.5f), new Vector2(-130, 0), new Vector2(200, 50));
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
            ? $"你将指认以下 {names.Count} 人为伪人：\n\n{string.Join("、", names)}\n\n提交后本局立即结束，且无法修改。确定吗？"
            : "你还没有标记任何人。\n若直接提交，将视为“没有找出任何伪人”。\n\n提交后本局立即结束，确定吗？";
        _submitConfirmText.text = body;
        _submitConfirmRoot.SetActive(true);
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
        _resultTitle.text = win ? "全部识破！" : "调查结束";
        _resultTitle.color = win ? new Color(0.6f, 1f, 0.6f) : new Color(1f, 0.75f, 0.55f);

        string imposterList = (imposters != null && imposters.Count > 0)
            ? string.Join("\n", imposters) : "（无）";
        _resultDetail.text =
            $"指认正确 {correct}/{total}　误指 {wrong} 人\n共拍摄 {photos} 张照片\n\n真正的伪人是：\n{imposterList}";
    }

    public void HideAllPanels()
    {
        HideDialogue();
        HideCamera();
        HideAlbum();
        HideMarkList();
        if (_resultRoot != null) _resultRoot.SetActive(false);
        if (_interactRoot != null) _interactRoot.SetActive(false);
    }

    public void ShowToast(string msg, bool positive)
    {
        _toastText.text = msg;
        _toastText.color = positive ? new Color(0.75f, 1f, 0.75f) : new Color(1f, 0.6f, 0.6f);
        _toastText.gameObject.SetActive(true);
        if (_toastCo != null) StopCoroutine(_toastCo);
        _toastCo = StartCoroutine(HideToastAfter(2.2f));
    }

    IEnumerator HideToastAfter(float t)
    {
        yield return new WaitForSeconds(t);
        _toastText.gameObject.SetActive(false);
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

    Button MakeButton(Transform parent, string label, int size, UnityAction onClick,
                      Vector2 anchor, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var go = new GameObject("Btn_" + label, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.25f, 0.28f, 0.36f, 0.98f);
        var btn = go.AddComponent<Button>();
        btn.onClick.AddListener(onClick);
        SetRect(go.GetComponent<RectTransform>(), anchor, anchor, anchor, anchoredPos, sizeDelta);

        var t = MakeText(go.transform, "Label", label, size, TextAnchor.MiddleCenter);
        SetRect(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        return btn;
    }

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
