// ScrollDragForwarder.cs
using System;
using UnityEngine;
using UnityEngine.UIElements;

public class ScrollDragForwarder : MonoBehaviour
{
    public UIDocument uiDocument;
    public string scrollViewName = "PlanetsScroll";
    public float dragThreshold = 6f;

    private ScrollView scrollView;
    private VisualElement content;
    private bool dragging = false;
    private int activePointerId = -9999;
    private Vector2 startPos;
    private Vector2 lastPos;

    void OnEnable()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null) { Debug.LogError("No UIDocument assigned."); enabled = false; return; }

        var root = uiDocument.rootVisualElement;
        scrollView = root.Q<ScrollView>(scrollViewName) ?? root.Q<ScrollView>();
        if (scrollView == null) { Debug.LogError("No ScrollView found."); enabled = false; return; }

        content = scrollView.contentContainer;
        content.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
        content.RegisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
        content.RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
        content.RegisterCallback<PointerCancelEvent>(OnPointerCancel, TrickleDown.TrickleDown);
    }

    void OnDisable()
    {
        if (content == null) return;
        content.UnregisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
        content.UnregisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
        content.UnregisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
        content.UnregisterCallback<PointerCancelEvent>(OnPointerCancel, TrickleDown.TrickleDown);
    }

    void OnPointerDown(PointerDownEvent evt)
    {
        activePointerId = evt.pointerId;
        startPos = lastPos = evt.position;
        dragging = false;

        try { content.CapturePointer(evt.pointerId); } catch { }
    }

    void OnPointerMove(PointerMoveEvent evt)
    {
        if (evt.pointerId != activePointerId) return;

        Vector2 pos = evt.position;
        Vector2 delta = pos - lastPos;

        if (!dragging)
        {
            if ((pos - startPos).magnitude > dragThreshold)
            {
                dragging = true;
                evt.StopPropagation();
            }
            else
            {
                lastPos = pos;
                return;
            }
        }

        if (dragging)
        {
            scrollView.scrollOffset -= new Vector2(delta.x, 0f); // horizontal scroll
            evt.StopPropagation();
            lastPos = pos;
        }
    }

    void OnPointerUp(PointerUpEvent evt)
    {
        if (evt.pointerId != activePointerId) return;
        if (dragging) evt.StopPropagation();
        CancelDrag();
    }

    void OnPointerCancel(PointerCancelEvent evt)
    {
        CancelDrag();
    }

    private void CancelDrag()
    {
        if (content != null && activePointerId != -9999)
        {
            try { content.ReleasePointer(activePointerId); } catch { }
        }
        dragging = false;
        activePointerId = -9999;
    }
}
