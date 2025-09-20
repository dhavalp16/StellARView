using UnityEngine;
using UnityEngine.UIElements;

public class ScrollDragForwarder : MonoBehaviour
{
    [Header("UI References")]
    public UIDocument uiDocument;
    public string scrollViewName = "ScrollView";

    [Header("Scroll Settings")]
    [SerializeField, Range(1f, 20f)] private float dragThreshold = 8f;
    [SerializeField, Range(0.1f, 5f)] private float scrollSensitivity = 1.5f;
    [SerializeField, Range(0.8f, 0.99f)] private float dampening = 0.92f;
    [SerializeField] private bool enableInertia = true;
    [SerializeField] private float inertiaThreshold = 30f;

    private ScrollView scrollView;
    private VisualElement content;
    private bool isDragging = false;
    private int activePointerId = -1;
    private Vector2 startPosition, currentPosition, previousPosition;
    private Vector2 velocity;
    private float lastMoveTime;
    private bool hasInertia = false;
    private float maxScrollOffset;

    void OnEnable()
    {
        InitializeScrollView();
    }

    void OnDisable()
    {
        UnregisterEvents();
        if (content != null)
            content.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
    }

    #region Unchanged Methods (with slight wiring)
    private void InitializeScrollView()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null) { enabled = false; return; }
        var root = uiDocument.rootVisualElement;
        if (root == null) { enabled = false; return; }
        scrollView = !string.IsNullOrEmpty(scrollViewName) ? root.Q<ScrollView>(scrollViewName) : root.Q<ScrollView>();
        if (scrollView == null) { enabled = false; return; }
        scrollView.mode = ScrollViewMode.Horizontal;
        scrollView.horizontalScrollerVisibility = ScrollerVisibility.AlwaysVisible;
        // we manage inertia ourselves:
        scrollView.scrollDecelerationRate = 0f;
        scrollView.elasticity = 0f;
        content = scrollView.contentContainer;
        if (content == null) { enabled = false; return; }
        // listen for geometry changes so we can compute correct max offset
        content.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        RegisterEvents();
        // compute initial value
        UpdateMaxScrollOffset();
    }

    private void RegisterEvents()
    {
        if (content == null) return;
        UnregisterEvents();
        content.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
        content.RegisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
        content.RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
        content.RegisterCallback<PointerCancelEvent>(OnPointerCancel, TrickleDown.TrickleDown);
    }

    private void UnregisterEvents()
    {
        if (content == null) return;
        content.UnregisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
        content.UnregisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
        content.UnregisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
        content.UnregisterCallback<PointerCancelEvent>(OnPointerCancel, TrickleDown.TrickleDown);
    }

    private void OnPointerDown(PointerDownEvent evt)
    {
        if (activePointerId != -1) return;
        activePointerId = evt.pointerId;
        startPosition = currentPosition = previousPosition = evt.position;
        isDragging = false;
        velocity = Vector2.zero;
        hasInertia = false;
        lastMoveTime = Time.time;
        content.CapturePointer(evt.pointerId);
    }

    private void OnPointerMove(PointerMoveEvent evt)
    {
        if (evt.pointerId != activePointerId) return;
        previousPosition = currentPosition;
        currentPosition = evt.position;
        Vector2 frameDelta = currentPosition - previousPosition;
        float deltaTime = Time.time - lastMoveTime;
        if (deltaTime > 0.001f) { velocity = frameDelta / deltaTime; }
        lastMoveTime = Time.time;
        if (!isDragging && Mathf.Abs(currentPosition.x - startPosition.x) > dragThreshold) { isDragging = true; }
        if (isDragging)
        {
            // move and don't let UIElements apply its own decel/elasticity
            scrollView.scrollOffset -= new Vector2(frameDelta.x * scrollSensitivity, 0);
            evt.StopPropagation();
        }
    }

    private void OnPointerUp(PointerUpEvent evt)
    {
        if (evt.pointerId != activePointerId) return;
        if (isDragging)
        {
            evt.StopPropagation();
            if (enableInertia && Mathf.Abs(velocity.x) > inertiaThreshold)
            {
                hasInertia = true;
            }
        }
        // Debug log to help see values at release time
        Debug.Log($"OnPointerUp: offset={scrollView.scrollOffset.x:F2}, max={maxScrollOffset:F2}, vel={velocity.x:F2}, hasInertia={hasInertia}");
        EndDrag();
    }

    private void OnPointerCancel(PointerCancelEvent evt)
    {
        if (evt.pointerId != activePointerId) return;
        EndDrag();
    }

    private void EndDrag()
    {
        if (content != null && content.HasPointerCapture(activePointerId))
        {
            content.ReleasePointer(activePointerId);
        }
        isDragging = false;
        activePointerId = -1;
    }
    #endregion

    // --- Improved sizing logic ---

    private void OnGeometryChanged(GeometryChangedEvent evt)
    {
        // geometry changed — recompute max offset
        UpdateMaxScrollOffset();
    }

    private void UpdateMaxScrollOffset()
    {
        if (scrollView == null || content == null) { maxScrollOffset = 0f; return; }

        // Use layout widths (these are available after layout)
        float contentWidth = content.layout.width;
        // Use the scrollView's viewport (contentViewport) for accurate viewport width
        float viewportWidth = (scrollView.contentViewport != null) ? scrollView.contentViewport.layout.width : scrollView.layout.width;

        // Fallback: if layout.width gives 0 (not yet measured), try resolvedStyle (less reliable)
        if (contentWidth <= 0f) contentWidth = content.resolvedStyle.width;
        if (viewportWidth <= 0f) viewportWidth = scrollView.resolvedStyle.width;

        maxScrollOffset = Mathf.Max(0f, contentWidth - viewportWidth);
        // Debug for visibility:
        // Remove or comment these logs when stable
        Debug.Log($"UpdateMaxScrollOffset: content={contentWidth:F2}, viewport={viewportWidth:F2}, max={maxScrollOffset:F2}");
    }

    void Update()
    {
        // Keep this as a fallback if geometry events miss something
        if (scrollView == null || content == null) return;
        if (maxScrollOffset <= 0f)
            UpdateMaxScrollOffset();
    }

    void LateUpdate()
    {
        if (scrollView == null) return;

        if (hasInertia && enableInertia)
        {
            ApplyInertia();
        }
        // If not dragging and not coasting, ensure offset is inside valid range,
        // but only clamp using a valid maxScrollOffset
        else if (!isDragging)
        {
            Vector2 currentOffset = scrollView.scrollOffset;
            // Only clamp when we have a sensible max
            if (maxScrollOffset > 0f)
            {
                float clampedX = Mathf.Clamp(currentOffset.x, 0, maxScrollOffset);
                if (!Mathf.Approximately(clampedX, currentOffset.x))
                {
                    currentOffset.x = clampedX;
                    scrollView.scrollOffset = currentOffset;
                    Debug.Log($"LateUpdate: clamped -> {clampedX:F2}");
                }
            }
        }
    }

    private void ApplyInertia()
    {
        // Apply velocity
        Vector2 newOffset = scrollView.scrollOffset - new Vector2(velocity.x * scrollSensitivity * Time.deltaTime, 0);

        // If we have a valid max, clamp; otherwise don't clobber to 0
        if (maxScrollOffset > 0f)
        {
            newOffset.x = Mathf.Clamp(newOffset.x, 0f, maxScrollOffset);
        }
        scrollView.scrollOffset = newOffset;

        // Dampen velocity
        velocity *= dampening;

        // Stop inertia if velocity is low
        if (Mathf.Abs(velocity.x) < 10f)
        {
            hasInertia = false;
            Debug.Log($"ApplyInertia: stopping (slow). finalOffset={scrollView.scrollOffset.x:F2}");
        }
        else
        {
            // If we hit a boundary, keep damping but don't immediately zero-out the offset
            if (maxScrollOffset > 0f && (Mathf.Approximately(newOffset.x, 0f) || Mathf.Approximately(newOffset.x, maxScrollOffset)))
            {
                // allow inertia to slowly die while clamped at boundary
                // if you prefer an immediate stop at the edge, uncomment next line:
                // hasInertia = false;
                Debug.Log($"ApplyInertia: at boundary newOffset={newOffset.x:F2} vel={velocity.x:F2}");
            }
        }
    }
}
