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
    [SerializeField] private Color hoverColor = Color.blue;
    [SerializeField] private Color normalColor = Color.white;

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
    }

    private void OnValidate()
    {
        snapDuration = Mathf.Max(0.05f, snapDuration);
        overscrollPixelsPerStep = Mathf.Max(10f, overscrollPixelsPerStep);
        snapThreshold = Mathf.Clamp(snapThreshold, 0f, 0.5f);

        if (!Application.isPlaying)
        {
            ApplyAllNormalColors();
        }
    }

    private void Start()
    {
        Stage initialStage = snapToBottomOnStart ? Stage.Bottom : Stage.Top;
        _snapTargetStage = initialStage;
        AlignToStageImmediate(initialStage);
    }

    private void OnDisable()
    {
        SetScrollRectInteractable(true);
    }

    private void OnEnable()
    {
        SetScrollRectInteractable(_currentStage != Stage.Top);
    }

    private void LateUpdate()
    {
        if (scrollRect == null)
        {
            return;
        }

        HandleKeyboardInput();

        if (_isSnapping)
        {
            TickSnap(Time.unscaledDeltaTime);
        }
        else if (!_isDragging)
        {
            MaintainStageAlignment();
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _isDragging = true;
        _isSnapping = false;
        _overscrollAccumulator = 0f;
    }

    public void OnDrag(PointerEventData eventData)
    {
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

        ApplyAllNormalColors();

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
                if (_hoveredIndex > 0)
                {
                    _overscrollAccumulator -= step;
                    SetHoveredIndex(_hoveredIndex - 1);
                }
                else
                {
                    _overscrollAccumulator = step;
                    break;
                }
            }
            else
            {
                if (_hoveredIndex < topBlockItems.Count - 1)
                {
                    _overscrollAccumulator += step;
                    SetHoveredIndex(_hoveredIndex + 1);
                }
                else
                {
                    ClearHover();
                    _overscrollAccumulator = 0f;
                    _currentStage = Stage.Bottom;
                    SetScrollRectInteractable(true);
                    BeginSnap(Stage.Bottom);
                    return false;
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
        if (Input.GetKeyDown(KeyCode.D))
        {
            direction = -1;
        }
        else if (Input.GetKeyDown(KeyCode.A))
        {
            direction = 1;
        }

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

        int nextIndex = _hoveredIndex + direction;

        if (nextIndex < 0)
        {
            nextIndex = 0;
        }

        if (nextIndex > topBlockItems.Count - 1)
        {
            ClearHover();
            _currentStage = Stage.Bottom;
            SetScrollRectInteractable(true);
            BeginSnap(Stage.Bottom);
            _overscrollAccumulator = 0f;
            return;
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

    private void ApplyAllNormalColors()
    {
        for (int i = 0; i < topBlockItems.Count; i++)
        {
            SetItemColor(i, normalColor);
        }
    }

    private void SetItemColor(int index, Color color)
    {
        RectTransform rect = GetItem(index);
        if (rect == null)
        {
            return;
        }

        Image image = rect.GetComponent<Image>();
        if (image != null)
        {
            image.color = color;
        }
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

            SetItemColor(_hoveredIndex, normalColor);
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

        SetItemColor(_hoveredIndex, normalColor);

        _hoveredIndex = index;

        SetItemColor(_hoveredIndex, hoverColor);

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

    private static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        float inv = t - 1f;
        return 1f + inv * inv * inv;
    }
}
