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

    // 相册
    GameObject _albumRoot;
    RawImage _albumBigImage;
    Text _albumHint;
    Transform _thumbRow;
    Transform _chipContainer;
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

        _promptText = MakeText(hud, "Prompt", "", 34, TextAnchor.LowerCenter);
        SetRect(_promptText.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
            new Vector2(0, 150), new Vector2(900, 60));

        MakeButton(hud, "相机 (空格)", 30, () => GameManager.Instance.OpenCamera(),
            new Vector2(1, 0), new Vector2(-150, 130), new Vector2(240, 70));
        MakeButton(hud, "相册 (Tab)", 30, () => GameManager.Instance.OpenAlbum(),
            new Vector2(1, 0), new Vector2(-150, 50), new Vector2(240, 70));
    }

    /// <summary>进入相机/对话/相册/结算等模式时隐藏主场景 HUD，回到探索时恢复。</summary>
    public void SetHudVisible(bool visible)
    {
        if (_hudRoot != null) _hudRoot.SetActive(visible);
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
        // 轻微压暗四周，突出相机；开口区域的暗化很轻，基本保持“所见即所拍”。
        _cameraRoot = MakePanel(_canvas.transform, "Camera", new Color(0f, 0f, 0f, 0.15f));
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
        var tip = MakeText(_cameraRoot.transform, "Tip", "移动鼠标瞄准；[1] 比耶　[2] 笑　[空格] 拍照　[Tab] 相册　[Esc] 退出", 28, TextAnchor.UpperCenter);
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
        MakeButton(_cameraRoot.transform, "相册 [Tab]", 28, () => GameManager.Instance.OpenAlbum(),
            new Vector2(0.5f, 0), new Vector2(300, 70), new Vector2(220, 74));
        MakeButton(_cameraRoot.transform, "退出 [Esc]", 28, () => GameManager.Instance.CloseCamera(),
            new Vector2(0.5f, 0), new Vector2(520, 70), new Vector2(220, 74));

        _cameraRoot.SetActive(false);
    }

    void BuildAlbum()
    {
        _albumRoot = MakePanel(_canvas.transform, "Album", new Color(0.05f, 0.06f, 0.09f, 0.97f));
        SetRect(_albumRoot.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        var title = MakeText(_albumRoot.transform, "ATitle", "相册 —— 点缩略图查看，点名字指认伪人", 34, TextAnchor.UpperCenter);
        SetRect(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(1400, 50));

        var thumbGO = new GameObject("ThumbRow", typeof(RectTransform));
        thumbGO.transform.SetParent(_albumRoot.transform, false);
        _thumbRow = thumbGO.transform;
        SetRect(thumbGO.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -90), new Vector2(-80, 130));

        _albumBigImage = MakeRawImage(_albumRoot.transform, "BigPhoto",
            new Vector2(0.5f, 0.5f), new Vector2(-220, -20), new Vector2(700, 470));
        _albumBigImage.color = new Color(0.15f, 0.15f, 0.15f);

        var chipGO = new GameObject("Chips", typeof(RectTransform));
        chipGO.transform.SetParent(_albumRoot.transform, false);
        _chipContainer = chipGO.transform;
        SetRect(chipGO.GetComponent<RectTransform>(), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-260, -20), new Vector2(460, 560));

        _albumHint = MakeText(_albumRoot.transform, "AHint", "", 30, TextAnchor.MiddleCenter);
        SetRect(_albumHint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 80));

        MakeButton(_albumRoot.transform, "关闭 (Esc)", 30, () => GameManager.Instance.CloseAlbum(),
            new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(240, 70));

        _albumRoot.SetActive(false);
    }

    void BuildResult()
    {
        _resultRoot = MakePanel(_canvas.transform, "Result", new Color(0.04f, 0.05f, 0.08f, 0.97f));
        SetRect(_resultRoot.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        _resultTitle = MakeText(_resultRoot.transform, "RTitle", "", 70, TextAnchor.MiddleCenter);
        SetRect(_resultTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(1200, 120));

        _resultDetail = MakeText(_resultRoot.transform, "RDetail", "", 36, TextAnchor.MiddleCenter);
        SetRect(_resultDetail.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200, 200));

        MakeButton(_resultRoot.transform, "再玩一次", 34, () => GameManager.Instance.StartRound(),
            new Vector2(0.5f, 0.5f), new Vector2(0, -170), new Vector2(320, 80));

        _resultRoot.SetActive(false);
    }

    void BuildToast()
    {
        _toastText = MakeText(_canvas.transform, "Toast", "", 34, TextAnchor.MiddleCenter);
        SetRect(_toastText.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -180), new Vector2(1300, 80));
        _toastText.gameObject.SetActive(false);
    }

    // ---------------- HUD / 提示 ----------------

    public void SetHud(int film, int found, int total)
    {
        _filmText.text = "胶卷 " + film;
        _filmText.color = film <= 2 ? new Color(1f, 0.4f, 0.4f) : Color.white;
        _foundText.text = $"伪人 {found}/{total}";
    }

    public void SetInteractPrompt(Npc npc)
    {
        _promptText.text = npc == null ? "" : $"按 [E] 与 {npc.npcName} 交谈";
    }

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

    public void ShowAlbum(List<PhotoEntry> entries)
    {
        _albumEntries = entries;
        _albumRoot.SetActive(true);
        ClearAlbumDynamic();

        if (entries == null || entries.Count == 0)
        {
            _selectedPhoto = -1;
            _albumBigImage.gameObject.SetActive(false);
            _albumHint.text = "还没有照片，先去拍几张吧。";
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
        _albumBigImage.texture = _albumEntries[index].image;
        _albumBigImage.color = Color.white;
        RefreshAlbumChips();
    }

    public void RefreshAlbumChips()
    {
        // 移除旧的名字按钮（保留缩略图）
        for (int i = _albumDynamic.Count - 1; i >= 0; i--)
        {
            var go = _albumDynamic[i];
            if (go != null && go.transform.parent == _chipContainer)
            {
                Destroy(go);
                _albumDynamic.RemoveAt(i);
            }
        }

        if (_albumEntries == null || _selectedPhoto < 0 || _selectedPhoto >= _albumEntries.Count) return;
        var framed = _albumEntries[_selectedPhoto].framed;

        var titleChip = MakeText(_chipContainer, "ChipTitle", "这张照片里有：", 28, TextAnchor.UpperLeft);
        SetRect(titleChip.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(0, 44));
        _albumDynamic.Add(titleChip.gameObject);

        for (int i = 0; i < framed.Count; i++)
        {
            var npc = framed[i];
            string label = npc.npcName;
            if (npc.caught) label += "（已指认·伪人）";
            else if (npc.accusedWrong) label += "（误判·普通人）";

            var btn = MakeButton(_chipContainer, label, 28, () => GameManager.Instance.AccuseFromAlbum(npc),
                new Vector2(0.5f, 1), new Vector2(0, -70 - i * 74), new Vector2(420, 64));

            if (npc.caught) SetButtonColor(btn, new Color(0.3f, 0.5f, 0.3f));
            else if (npc.accusedWrong) SetButtonColor(btn, new Color(0.45f, 0.3f, 0.3f));
            _albumDynamic.Add(btn.gameObject);
        }
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

    // ---------------- 结算 / Toast / 统一隐藏 ----------------

    public void ShowResult(bool win, int found, int total, int photos)
    {
        _resultRoot.SetActive(true);
        _resultTitle.text = win ? "任务完成！" : "胶卷耗尽…";
        _resultTitle.color = win ? new Color(0.6f, 1f, 0.6f) : new Color(1f, 0.6f, 0.6f);
        _resultDetail.text = $"抓获伪人 {found}/{total}\n本次共拍了 {photos} 张照片";
    }

    public void HideAllPanels()
    {
        HideDialogue();
        HideCamera();
        HideAlbum();
        if (_resultRoot != null) _resultRoot.SetActive(false);
        _promptText.text = "";
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
