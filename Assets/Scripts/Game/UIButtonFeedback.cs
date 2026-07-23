using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>按钮悬停/按下的缩放反馈。挂在按钮 GameObject 上。</summary>
public class UIButtonFeedback : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    Vector3 _base = Vector3.one;
    RectTransform _rt;

    void Awake()
    {
        _rt = GetComponent<RectTransform>();
        _base = _rt.localScale;
    }

    void OnDisable()
    {
        if (_rt != null) _rt.localScale = _base;
    }

    public void OnPointerEnter(PointerEventData e) { if (_rt) _rt.localScale = _base * 1.05f; }
    public void OnPointerExit(PointerEventData e) { if (_rt) _rt.localScale = _base; }
    public void OnPointerDown(PointerEventData e) { if (_rt) _rt.localScale = _base * 0.93f; }
    public void OnPointerUp(PointerEventData e) { if (_rt) _rt.localScale = _base * 1.05f; }
}
