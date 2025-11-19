using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
[RequireComponent(typeof(ScrollRect))]
public class TwoStageScrollController : MonoBehaviour, IBeginDragHandler, IEndDragHandler, IDragHandler, IScrollHandler, IPointerClickHandler
{
    private enum Stage
    {
        Bottom,
        Top
    }
    [Header("Scroll References")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform viewport;
    [Header("Snapping")]
    [SerializeField] private bool snapToBottomOnStart = true;
    [SerializeField] [Min(0.05f)] private float snapDuration = 0.35f;
    [SerializeField] [Range(0f, 0.5f)] private float snapThreshold = 0.2f;
    [Header("Top Block Selection")]
    [SerializeField] [Min(10f)] private float overscrollPixelsPerStep = 80f;
    [SerializeField] private List<RectTransform> topBlockItems = new();
    [SerializeField] private bool sendPointerEvents = false;
    [SerializeField] private bool sendClickEvents = false;
    [SerializeField] private UnityEvent<int> onHoverIndexChanged;
    [SerializeField] private UnityEvent<int> onItemClicked;
    [Header("Hover Effect")]
    [SerializeField] [Min(0.1f)] private float hoverFadeDuration = 0.15f;
    [Header("Continuous Mode")]
    [SerializeField] private bool continuous = false;
    [SerializeField] [Min(0.1f)] private float mouseSensitivity = 2.0f;
    [SerializeField] [Min(0.1f)] private float mouseStopDelay = 0.2f;
    [Header("Content Animation")]
    [SerializeField] private RectTransform animatedRoot;
    [SerializeField] private bool animateContentItems = true;
    [SerializeField] [Min(0f)] private float animationEdgeRange = 40f;
    [SerializeField] [Range(0f, 1f)] private float animationMinScale = 0.5f;
    [SerializeField] private bool autoAddCanvasGroup = true;

    private const float SnapEpsilon = 0.0005f;
    private Stage _currentStage = Stage.Bottom;
    private Stage _snapTargetStage = Stage.Bottom;
    private bool _isDragging;
    private bool _isSnapping;
    private float _snapStart;
    private float _snapTarget;
    private float _snapTime;
    private float _overscrollAccumulator;
    private int _hoveredIndex = -1;
    private bool _scrollRectInitiallyEnabled = true;
    // Continuous mode variables
    private Vector3 _lastMousePosition;
    private float _lastMouseMoveTime;
    private bool _isMouseMoving;
    private float _continuousScrollPosition;
    private readonly List<ContentItemState> _contentItems = new();
    private readonly Dictionary<RectTransform, Vector3> _contentBaseScales = new();
    private readonly List<RectTransform> _pruneBuffer = new();
    private readonly Vector3[] _corners = new Vector3[4];


    private void Awake()
    {
        if (scrollRect == null)
        {
            scrollRect = GetComponent<ScrollRect>();
        }
        if (scrollRect != null && viewport == null)
        {
            viewport = scrollRect.viewport;
        }
        if (scrollRect != null)
        {
            _scrollRectInitiallyEnabled = scrollRect.enabled;
        }
        CollectContentChildren();
    }
    private void OnValidate()
    {
        snapDuration = Mathf.Max(0.05f, snapDuration);
        overscrollPixelsPerStep = Mathf.Max(10f, overscrollPixelsPerStep);
        snapThreshold = Mathf.Clamp(snapThreshold, 0f, 0.5f);
        animationEdgeRange = Mathf.Max(0f, animationEdgeRange);
        animationMinScale = Mathf.Clamp01(animationMinScale);
        if (!Application.isPlaying)
        {
            ApplyAllNormalStates();
        }
    }
    private void Start()
    {
        Stage initialStage = snapToBottomOnStart ? Stage.Bottom : Stage.Top;
        _snapTargetStage = initialStage;
        AlignToStageImmediate(initialStage);
        
        // Initialize mouse tracking for continuous mode
        _lastMousePosition = Input.mousePosition;
        _continuousScrollPosition = 0f;
    }
    private void OnDisable()
    {
        SetScrollRectInteractable(true);
        VInput.onVInputEvent -= OnVInputEvent;
    }
    private void OnEnable()
    {
        SetScrollRectInteractable(_currentStage != Stage.Top);
        VInput.onVInputEvent += OnVInputEvent;
        CollectContentChildren();
        UpdateContentItemVisuals();
    }
    private void LateUpdate() {
        if (scrollRect == null) {
            return;
        }
        // Update VInput system to process Vuzix touchpad input
        VInput.Update(Time.unscaledDeltaTime);
        if (continuous)
        {
            HandleContinuousMouseInput();
        }
        else
        {
            HandleKeyboardInput();
        }
        if (_isSnapping) {
            TickSnap(Time.unscaledDeltaTime);
        } else if (!_isDragging) {
            MaintainStageAlignment();
        }

        if (animateContentItems) {
            RectTransform root = GetAnimationRoot();
            if (root != null && _contentItems.Count != root.childCount) {
                CollectContentChildren();
            }
        }
        UpdateContentItemVisuals();
    }
    
    private void OnVInputEvent(VINPUT_EVENT vEvent)
    {
        if (_isSnapping)
            return;
        switch (vEvent)
        {
            case VINPUT_EVENT.SWIPE_BACKWARD_1FINGER:
                // 1-finger backward swipe: maps to 'A' key behavior
                HandleVInputNavigation(-1); // A key direction
                break;
            case VINPUT_EVENT.SWIPE_FORWARD_1FINGER:
                // 1-finger forward swipe: maps to 'D' key behavior
                HandleVInputNavigation(1); // D key direction
                break;
        }
    }
    private void HandleVInputNavigation(int direction)
    {
        if (_isDragging)
        {
            return;
        }
        if (_isSnapping)
        {
            return;
        }
        if (_currentStage != Stage.Top)
        {
            if (direction < 0)
            {
                BeginSnap(Stage.Top);
            }
            return;
        }
        if (topBlockItems.Count == 0)
        {
            return;
        }
        if (_hoveredIndex < 0 || _hoveredIndex >= topBlockItems.Count)
        {
            InitializeHover();
            return;
        }
        // Use same logic as keyboard input - reverse direction for top block item switching
        int topBlockDirection = -direction;
        int nextIndex = _hoveredIndex + topBlockDirection;
        if (nextIndex < 0)
        {
            if (_hoveredIndex == 0)
            {
                // At first item, going further back exits to bottom block
                ClearHover();
                _currentStage = Stage.Bottom;
                SetScrollRectInteractable(true);
                BeginSnap(Stage.Bottom);
                _overscrollAccumulator = 0f;
                return;
            }
            nextIndex = 0;
        }
        if (nextIndex > topBlockItems.Count - 1)
        {
            nextIndex = topBlockItems.Count - 1;
        }
        SetHoveredIndex(nextIndex, true);
        _overscrollAccumulator = 0f;
    }
    private void HandleContinuousMouseInput()
    {
        if (_isSnapping)
            return;
        Vector3 currentMousePosition = Input.mousePosition;
        
        // Check if mouse is moving
        if (Vector3.Distance(currentMousePosition, _lastMousePosition) > 0.1f)
        {
            // Mouse is moving
            _isMouseMoving = true;
            _lastMouseMoveTime = Time.time;
            // Calculate horizontal movement delta
            float mouseDelta = (currentMousePosition.x - _lastMousePosition.x) * mouseSensitivity;
            
            // Left to right = scroll down (positive), right to left = scroll up (negative)
            _continuousScrollPosition += mouseDelta * 0.01f;
            // Update hover based on continuous scroll position
            if (_currentStage == Stage.Top && topBlockItems.Count > 0)
            {
                // Check if we should exit to bottom block (moving left past first item)
                if (_continuousScrollPosition < -0.5f && _hoveredIndex == 0)
                {
                    ClearHover();
                    _currentStage = Stage.Bottom;
                    SetScrollRectInteractable(true);
                    BeginSnap(Stage.Bottom);
                    _continuousScrollPosition = 0f;
                    return;
                }
                
                // Clamp position after checking for exit condition
                _continuousScrollPosition = Mathf.Clamp(_continuousScrollPosition, 0f, topBlockItems.Count - 1f);
                
                int newHoverIndex = Mathf.RoundToInt(_continuousScrollPosition);
                newHoverIndex = Mathf.Clamp(newHoverIndex, 0, topBlockItems.Count - 1);
                
                if (newHoverIndex != _hoveredIndex)
                {
                    SetHoveredIndex(newHoverIndex, true);
                }
            }
            else if (_currentStage == Stage.Bottom)
            {
                // If in bottom stage and moving right, go to top stage
                if (mouseDelta > 0 && _continuousScrollPosition > 0.5f)
                {
                    _currentStage = Stage.Top;
                    BeginSnap(Stage.Top);
                    _continuousScrollPosition = 0f;
                    return;
                }
            }
        }
        else if (_isMouseMoving && Time.time - _lastMouseMoveTime > mouseStopDelay)
        {
            // Mouse has stopped moving, trigger snap
            _isMouseMoving = false;
            
            if (_currentStage == Stage.Top)
            {
                // Snap to nearest item
                int snapIndex = Mathf.RoundToInt(_continuousScrollPosition);
                snapIndex = Mathf.Clamp(snapIndex, 0, topBlockItems.Count - 1);
                _continuousScrollPosition = snapIndex;
                
                if (snapIndex != _hoveredIndex)
                {
                    SetHoveredIndex(snapIndex, true);
                }
            }
        }
        _lastMousePosition = currentMousePosition;
    }
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (continuous)
        {
            // In continuous mode, disable traditional dragging
            return;
        }
        
        _isDragging = true;
        _isSnapping = false;
        _overscrollAccumulator = 0f;
    }
    public void OnDrag(PointerEventData eventData)
    {
        if (continuous)
        {
            // In continuous mode, disable traditional dragging
            return;
        }
        
        if (scrollRect == null)
        {
            return;
        }
        if (_currentStage == Stage.Top)
        {
            bool keepAtTop = HandleTopOverscroll(eventData.delta.y);
            if (keepAtTop)
            {
                ForceNormalized(1f);
                scrollRect.StopMovement();
                SetScrollRectInteractable(false);
                eventData.Use();
                return;
            }
        }
    }
    public void OnEndDrag(PointerEventData eventData)
    {
        if (continuous)
        {
            // In continuous mode, disable traditional dragging
            return;
        }
        
        _isDragging = false;
        if (scrollRect == null || _isSnapping)
        {
            return;
        }
        float position = Mathf.Clamp01(scrollRect.verticalNormalizedPosition);
        Stage target;
        float upperSnapThreshold = 1f - snapThreshold;
        if (position >= upperSnapThreshold)
        {
            target = Stage.Top;
        }
        else if (position <= snapThreshold)
        {
            target = Stage.Bottom;
        }
        else
        {
            target = position >= 0.5f ? Stage.Top : Stage.Bottom;
        }
        BeginSnap(target);
    }
    public void OnScroll(PointerEventData eventData)
    {
        if (scrollRect == null || Mathf.Approximately(eventData.scrollDelta.y, 0f))
        {
            return;
        }
        if (_currentStage != Stage.Top || _isSnapping)
        {
            return;
        }
        float scaledDelta = eventData.scrollDelta.y * overscrollPixelsPerStep;
        bool keepAtTop = HandleTopOverscroll(scaledDelta);
        if (keepAtTop)
        {
            ForceNormalized(1f);
            scrollRect.StopMovement();
            SetScrollRectInteractable(false);
            eventData.Use();
        }
    }
    private void BeginSnap(Stage targetStage)
    {
        if (scrollRect == null)
        {
            return;
        }
        float targetNormalized = targetStage == Stage.Top ? 1f : 0f;
        float current = Mathf.Clamp01(scrollRect.verticalNormalizedPosition);
        _snapTargetStage = targetStage;
        if (Mathf.Abs(current - targetNormalized) <= SnapEpsilon)
        {
            AlignToStageImmediate(targetStage);
            return;
        }
        _isSnapping = true;
        _snapTime = 0f;
        _snapStart = current;
        _snapTarget = targetNormalized;
    }
    private void TickSnap(float deltaTime)
    {
        float duration = Mathf.Max(0.0001f, snapDuration);
        _snapTime += deltaTime;
        float t = Mathf.Clamp01(_snapTime / duration);
        float eased = EaseOutCubic(t);
        float next = Mathf.Lerp(_snapStart, _snapTarget, eased);
        scrollRect.verticalNormalizedPosition = next;
        if (Mathf.Abs(next - _snapTarget) <= SnapEpsilon)
        {
            AlignToStageImmediate(_snapTargetStage);
            _isSnapping = false;
        }
    }
    private void AlignToStageImmediate(Stage stage)
    {
        _currentStage = stage;
        _overscrollAccumulator = 0f;
        if (scrollRect != null)
        {
            float normalized = stage == Stage.Top ? 1f : 0f;
            scrollRect.verticalNormalizedPosition = normalized;
            scrollRect.velocity = Vector2.zero;
        }
        SetScrollRectInteractable(stage != Stage.Top);
        ApplyAllNormalStates();
        if (stage == Stage.Top)
        {
            if (_hoveredIndex < 0 || _hoveredIndex >= topBlockItems.Count)
            {
                InitializeHover();
            }
            else
            {
                SetHoveredIndex(_hoveredIndex, true);
            }
        }
        else
        {
            ClearHover();
        }
    }
    private void MaintainStageAlignment()
    {
        float desired = _currentStage == Stage.Top ? 1f : 0f;
        float current = Mathf.Clamp01(scrollRect.verticalNormalizedPosition);
        if (Mathf.Abs(current - desired) > SnapEpsilon)
        {
            scrollRect.verticalNormalizedPosition = desired;
            scrollRect.velocity = Vector2.zero;
        }
    }
    private bool HandleTopOverscroll(float rawDelta)
    {
        if (topBlockItems.Count == 0)
        {
            SetScrollRectInteractable(true);
            return false;
        }
        if (Mathf.Approximately(rawDelta, 0f))
        {
            return true;
        }
        rawDelta = -rawDelta;
        float step = Mathf.Max(1f, overscrollPixelsPerStep);
        if (Mathf.Sign(_overscrollAccumulator) != Mathf.Sign(rawDelta))
        {
            _overscrollAccumulator = 0f;
        }
        _overscrollAccumulator += rawDelta;
        // Consume repeated drags while the view is locked at the top. Each overscroll "step"
        // advances the hover index, and pushing past the final item releases the lock so the
        // user can continue scrolling back to the bottom block.
        while (Mathf.Abs(_overscrollAccumulator) >= step)
        {
            if (_overscrollAccumulator > 0f)
            {
                if (_hoveredIndex < topBlockItems.Count - 1)
                {
                    _overscrollAccumulator -= step;
                    SetHoveredIndex(_hoveredIndex + 1);
                }
                else
                {
                    _overscrollAccumulator = step;
                    break;
                }
            }
            else
            {
                if (_hoveredIndex > 0)
                {
                    _overscrollAccumulator += step;
                    SetHoveredIndex(_hoveredIndex - 1);
                }
                else if (_hoveredIndex == 0)
                {
                    ClearHover();
                    _overscrollAccumulator = 0f;
                    _currentStage = Stage.Bottom;
                    SetScrollRectInteractable(true);
                    BeginSnap(Stage.Bottom);
                    return false;
                }
                else
                {
                    _overscrollAccumulator = -step;
                    break;
                }
            }
        }
        _overscrollAccumulator = Mathf.Clamp(_overscrollAccumulator, -step, step);
        return true;
    }
    private void HandleKeyboardInput()
    {
        if (_isDragging)
        {
            return;
        }
        int direction = 0;
        // if (Input.GetKeyDown(KeyCode.D))
        // {
        //     direction = -1;
        // }
        // else if (Input.GetKeyDown(KeyCode.A))
        // {
        //     direction = 1;
        // }
        if (direction == 0)
        {
            return;
        }
        if (_isSnapping)
        {
            return;
        }
        if (_currentStage != Stage.Top)
        {
            if (direction < 0)
            {
                BeginSnap(Stage.Top);
            }
            return;
        }
        if (topBlockItems.Count == 0)
        {
            return;
        }
        if (_hoveredIndex < 0 || _hoveredIndex >= topBlockItems.Count)
        {
            InitializeHover();
            return;
        }
        // Reverse direction for top block item switching
        int topBlockDirection = -direction;
        int nextIndex = _hoveredIndex + topBlockDirection;
        if (nextIndex < 0)
        {
            if (_hoveredIndex == 0)
            {
                // At first item, going further back exits to bottom block
                ClearHover();
                _currentStage = Stage.Bottom;
                SetScrollRectInteractable(true);
                BeginSnap(Stage.Bottom);
                _overscrollAccumulator = 0f;
                return;
            }
            nextIndex = 0;
        }
        if (nextIndex > topBlockItems.Count - 1)
        {
            nextIndex = topBlockItems.Count - 1;
        }
        SetHoveredIndex(nextIndex, true);
        _overscrollAccumulator = 0f;
    }
    private void ForceNormalized(float value)
    {
        if (scrollRect == null)
        {
            return;
        }
        scrollRect.verticalNormalizedPosition = Mathf.Clamp01(value);
        scrollRect.velocity = Vector2.zero;
    }
    private void SetScrollRectInteractable(bool enable)
    {
        if (scrollRect == null)
        {
            return;
        }
        bool desired = enable ? _scrollRectInitiallyEnabled : false;
        if (scrollRect.enabled != desired)
        {
            scrollRect.enabled = desired;
        }
    }
    private void ApplyAllNormalStates()
    {
        for (int i = 0; i < topBlockItems.Count; i++)
        {
            SetItemHoverState(i, false);
        }

        if (!animateContentItems)
        {
            return;
        }

        if (!Application.isPlaying)
        {
            CollectContentChildren();
        }

        for (int i = 0; i < _contentItems.Count; i++)
        {
            var item = _contentItems[i];
            if (item.Rect == null)
            {
                continue;
            }

            item.Rect.localScale = item.BaseScale;
            if (item.CanvasGroup != null)
            {
                item.CanvasGroup.alpha = 1f;
            }
        }
    }
    private void SetItemHoverState(int index, bool isHovered)
    {
        RectTransform rect = GetItem(index);
        if (rect == null)
        {
            return;
        }
        // Look for a child with CanvasGroup component
        CanvasGroup hoverChild = GetHoverChild(rect);
        if (hoverChild != null)
        {
            float targetAlpha = isHovered ? 1f : 0f;
            if (Application.isPlaying)
            {
                StartCoroutine(FadeHoverChild(hoverChild, targetAlpha));
            }
            else
            {
                hoverChild.alpha = targetAlpha;
            }
        }
    }
    private CanvasGroup GetHoverChild(RectTransform parent)
    {
        // Check all children for CanvasGroup component
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            CanvasGroup canvasGroup = child.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                return canvasGroup;
            }
        }
        return null;
    }
    private System.Collections.IEnumerator FadeHoverChild(CanvasGroup canvasGroup, float targetAlpha)
    {
        float startAlpha = canvasGroup.alpha;
        float elapsedTime = 0f;
        while (elapsedTime < hoverFadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / hoverFadeDuration;
            
            // Use smooth easing
            float smoothProgress = progress * progress * (3f - 2f * progress);
            
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, smoothProgress);
            yield return null;
        }
        // Ensure final alpha
        canvasGroup.alpha = targetAlpha;
    }
    private void InitializeHover()
    {
        if (topBlockItems.Count == 0)
        {
            ClearHover();
            return;
        }
        SetHoveredIndex(0, true);
    }
    private void ClearHover()
    {
        RectTransform previousRect = GetItem(_hoveredIndex);
        if (previousRect != null)
        {
            GameObject previous = previousRect.gameObject;
            if (sendPointerEvents)
            {
                ExecutePointerExit(previous);
            }
            SetItemHoverState(_hoveredIndex, false);
        }
        _hoveredIndex = -1;
        onHoverIndexChanged?.Invoke(-1);
    }
    private void SetHoveredIndex(int index, bool force = false)
    {
        if (topBlockItems.Count == 0)
        {
            ClearHover();
            return;
        }
        index = Mathf.Clamp(index, 0, topBlockItems.Count - 1);
        if (!force && index == _hoveredIndex)
        {
            return;
        }
        GameObject previous = null;
        RectTransform previousRect = GetItem(_hoveredIndex);
        if (previousRect != null)
        {
            previous = previousRect.gameObject;
        }
        RectTransform currentRect = GetItem(index);
        GameObject current = currentRect != null ? currentRect.gameObject : null;
        if (sendPointerEvents && previous != null)
        {
            ExecutePointerExit(previous);
        }
        SetItemHoverState(_hoveredIndex, false);
        _hoveredIndex = index;
        SetItemHoverState(_hoveredIndex, true);
        if (current != null && sendPointerEvents)
        {
            ExecutePointerEnter(current);
        }
        onHoverIndexChanged?.Invoke(_hoveredIndex);
    }
    public void OnPointerClick(PointerEventData eventData)
    {
        if (_currentStage != Stage.Top || _hoveredIndex < 0 || _hoveredIndex >= topBlockItems.Count)
        {
            return;
        }
        onItemClicked?.Invoke(_hoveredIndex);
        if (sendClickEvents && EventSystem.current != null)
        {
            RectTransform rect = GetItem(_hoveredIndex);
            GameObject current = rect != null ? rect.gameObject : null;
            if (current != null)
            {
                ExecutePointerClick(current, eventData);
            }
        }
    }
    private void ExecutePointerEnter(GameObject target)
    {
        if (EventSystem.current == null)
        {
            return;
        }
        var data = new PointerEventData(EventSystem.current);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerEnterHandler);
    }
    private void ExecutePointerExit(GameObject target)
    {
        if (EventSystem.current == null)
        {
            return;
        }
        var data = new PointerEventData(EventSystem.current);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerExitHandler);
    }
    private void ExecutePointerClick(GameObject target, PointerEventData original)
    {
        if (EventSystem.current == null)
        {
            return;
        }
        var data = new PointerEventData(EventSystem.current)
        {
            button = original != null ? original.button : PointerEventData.InputButton.Left,
            clickCount = original != null ? original.clickCount : 1
        };
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerClickHandler);
    }

    private void CollectContentChildren()
    {
        _contentItems.Clear();

        RectTransform root = GetAnimationRoot();
        if (root == null)
        {
            _contentBaseScales.Clear();
            return;
        }

        int childCount = root.childCount;
        for (int i = 0; i < childCount; i++)
        {
            RectTransform child = root.GetChild(i) as RectTransform;
            if (child == null)
            {
                continue;
            }

            if (!_contentBaseScales.TryGetValue(child, out var baseScale))
            {
                baseScale = child.localScale;
                _contentBaseScales[child] = baseScale;
            }

            CanvasGroup canvas = child.GetComponent<CanvasGroup>();
            if (canvas == null && autoAddCanvasGroup)
            {
                canvas = child.gameObject.AddComponent<CanvasGroup>();
            }

            _contentItems.Add(new ContentItemState
            {
                Rect = child,
                BaseScale = baseScale,
                CanvasGroup = canvas
            });
        }

        _pruneBuffer.Clear();
        foreach (var kvp in _contentBaseScales)
        {
            bool stillPresent = false;
            for (int i = 0; i < _contentItems.Count; i++)
            {
                if (_contentItems[i].Rect == kvp.Key)
                {
                    stillPresent = true;
                    break;
                }
            }

            if (!stillPresent)
            {
                _pruneBuffer.Add(kvp.Key);
            }
        }

        for (int i = 0; i < _pruneBuffer.Count; i++)
        {
            _contentBaseScales.Remove(_pruneBuffer[i]);
        }

        _pruneBuffer.Clear();
    }

    private void UpdateContentItemVisuals()
    {
        if (!animateContentItems || viewport == null)
        {
            return;
        }

        RectTransform root = GetAnimationRoot();
        if (root == null || _contentItems.Count == 0)
        {
            return;
        }

        Rect viewportRect = viewport.rect;
        float clampedRange = Mathf.Max(animationEdgeRange, 0.0001f);

        for (int i = 0; i < _contentItems.Count; i++)
        {
            var item = _contentItems[i];
            if (item.Rect == null)
            {
                continue;
            }

            item.Rect.GetWorldCorners(_corners);
            Vector3 topLeftLocal = viewport.InverseTransformPoint(_corners[1]);
            Vector3 bottomLeftLocal = viewport.InverseTransformPoint(_corners[0]);
            float centerLocalY = (topLeftLocal.y + bottomLeftLocal.y) * 0.5f;
            float distanceFromTop = viewportRect.yMax - centerLocalY;
            float distanceFromBottom = centerLocalY - viewportRect.yMin;

            float scaleFactor = 1f;
            float alpha = 1f;

            if (distanceFromTop <= 0f)
            {
                scaleFactor = Mathf.Min(scaleFactor, animationMinScale);
                alpha = 0f;
            }
            else if (distanceFromTop < animationEdgeRange)
            {
                float t = Mathf.Clamp01(distanceFromTop / clampedRange);
                scaleFactor = Mathf.Min(scaleFactor, Mathf.Lerp(animationMinScale, 1f, t));
                alpha = Mathf.Min(alpha, t);
            }

            if (distanceFromBottom <= 0f)
            {
                scaleFactor = Mathf.Min(scaleFactor, animationMinScale);
                alpha = 0f;
            }
            else if (distanceFromBottom < animationEdgeRange)
            {
                float t = Mathf.Clamp01(distanceFromBottom / clampedRange);
                scaleFactor = Mathf.Min(scaleFactor, Mathf.Lerp(animationMinScale, 1f, t));
                alpha = Mathf.Min(alpha, t);
            }

            Vector3 targetScale = item.BaseScale * scaleFactor;
            if (item.Rect.localScale != targetScale)
            {
                item.Rect.localScale = targetScale;
            }

            if (item.CanvasGroup != null && !Mathf.Approximately(item.CanvasGroup.alpha, alpha))
            {
                item.CanvasGroup.alpha = alpha;
            }
        }
    }

    private RectTransform GetAnimationRoot()
    {
        if (animatedRoot != null)
        {
            return animatedRoot;
        }

        return scrollRect != null ? scrollRect.content : null;
    }
    private RectTransform GetItem(int logicalIndex)
    {
        if (logicalIndex < 0 || logicalIndex >= topBlockItems.Count)
        {
            return null;
        }
        int reversedIndex = topBlockItems.Count - 1 - logicalIndex;
        if (reversedIndex < 0 || reversedIndex >= topBlockItems.Count)
        {
            return null;
        }
        return topBlockItems[reversedIndex];
    }

    private struct ContentItemState
    {
        public RectTransform Rect;
        public Vector3 BaseScale;
        public CanvasGroup CanvasGroup;
    }












    private static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        float inv = t - 1f;
        return 1f + inv * inv * inv;
    }
}
