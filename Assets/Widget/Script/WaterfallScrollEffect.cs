using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
[RequireComponent(typeof(ScrollRect))]
public class WaterfallScrollEffect : MonoBehaviour, IBeginDragHandler, IEndDragHandler
{
    [Header("Scroll Setup")]
    [SerializeField] private RectTransform viewport;
    [SerializeField] private bool snapToBottomOnStart = true;

    [Header("Animation")]
    [SerializeField] [Min(0f)] private float enterRange = 80f;
    [SerializeField] [Min(0f)] private float exitFadeStart = 400f;
    [SerializeField] [Min(0f)] private float exitFadeEnd = 480f;
    [SerializeField] [Range(0.5f, 1f)] private float minScale = 0.8f;
    [SerializeField] private bool autoAddCanvasGroup = true;

    [Header("Snapping")]
    [SerializeField] private bool enableSnapping = true;
    [SerializeField] [Min(0f)] private float snapHeight = 480f;
    [SerializeField] [Range(0f, 0.5f)] private float snapThresholdNormalized = 0.15f;
    [SerializeField] [Min(0.1f)] private float snapDuration = 0.45f;
    [SerializeField] [Min(0f)] private float snapVelocityThreshold = 50f;

    private readonly List<ItemState> _items = new();
    private readonly Dictionary<RectTransform, Vector3> _baseScales = new();
    private readonly List<RectTransform> _pruneBuffer = new();
    private readonly Vector3[] _corners = new Vector3[4];
    private ScrollRect _scrollRect;
    private bool _isDragging;
    private bool _isSnapping;
    private float _snapTargetNormalized;
    private float _snapStartNormalized;
    private float _snapTime;
    private const float SnapEpsilon = 0.0001f;

    private void Awake()
    {
        _scrollRect = GetComponent<ScrollRect>();
        if (viewport == null)
        {
            viewport = _scrollRect.viewport;
        }

        CollectChildren();
    }

    private void OnEnable()
    {
        CollectChildren();
        if (snapToBottomOnStart)
        {
            SnapToBottom();
        }
    }

    private void Start()
    {
        // Force an initial tick so the visuals match the starting scroll position.
        UpdateItemStates();
    }

    private void LateUpdate()
    {
        if (_scrollRect != null && _scrollRect.content != null && _scrollRect.content.childCount != _items.Count)
        {
            CollectChildren();
        }

        UpdateItemStates();
        HandleSnap(Time.unscaledDeltaTime);
    }

    private void OnValidate()
    {
        if (exitFadeEnd < exitFadeStart)
        {
            exitFadeEnd = exitFadeStart;
        }

        if (minScale > 1f)
        {
            minScale = 1f;
        }

        if (snapThresholdNormalized > 0.5f)
        {
            snapThresholdNormalized = 0.5f;
        }

        if (snapDuration < 0.1f)
        {
            snapDuration = 0.1f;
        }
    }

    private void CollectChildren()
    {
        _items.Clear();

        if (_scrollRect == null || _scrollRect.content == null)
        {
            _baseScales.Clear();
            return;
        }

        foreach (RectTransform child in _scrollRect.content)
        {
            if (child == null)
            {
                continue;
            }

            if (!_baseScales.TryGetValue(child, out var baseScale))
            {
                baseScale = child.localScale;
                _baseScales[child] = baseScale;
            }

            var state = new ItemState();
            state.Rect = child;
            state.BaseScale = baseScale;
            state.CanvasGroup = child.GetComponent<CanvasGroup>();

            if (state.CanvasGroup == null && autoAddCanvasGroup)
            {
                state.CanvasGroup = child.gameObject.AddComponent<CanvasGroup>();
            }

            _items.Add(state);
        }

        _pruneBuffer.Clear();
        foreach (var entry in _baseScales)
        {
            bool stillPresent = false;
            foreach (var item in _items)
            {
                if (item.Rect == entry.Key)
                {
                    stillPresent = true;
                    break;
                }
            }

            if (!stillPresent)
            {
                _pruneBuffer.Add(entry.Key);
            }
        }

        for (int i = 0; i < _pruneBuffer.Count; i++)
        {
            _baseScales.Remove(_pruneBuffer[i]);
        }
        _pruneBuffer.Clear();
    }

    private void SnapToBottom()
    {
        if (_scrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            _scrollRect.verticalNormalizedPosition = 0f;
            _scrollRect.velocity = Vector2.zero;
        }
    }

    private void UpdateItemStates()
    {
        if (viewport == null)
        {
            return;
        }

        Rect viewportRect = viewport.rect;

        foreach (var item in _items)
        {
            if (item.Rect == null)
            {
                continue;
            }

            item.Rect.GetWorldCorners(_corners);
            Vector3 topLeftLocal = viewport.InverseTransformPoint(_corners[1]);
            Vector3 bottomLeftLocal = viewport.InverseTransformPoint(_corners[0]);
            float centerLocalY = (topLeftLocal.y + bottomLeftLocal.y) * 0.5f;
            float centerDistanceFromTop = viewportRect.yMax - centerLocalY;

            float scaleFactor = 1f;
            float alpha = 1f;

            if (centerDistanceFromTop <= 0f)
            {
                scaleFactor = minScale;
                alpha = 0f;
            }
            else if (centerDistanceFromTop < enterRange)
            {
                float t = Mathf.Clamp01(centerDistanceFromTop / Mathf.Max(enterRange, 0.0001f));
                scaleFactor = Mathf.Lerp(minScale, 1f, t);
                alpha = t;
            }

            if (centerDistanceFromTop > exitFadeStart)
            {
                float exitRange = Mathf.Max(exitFadeEnd - exitFadeStart, 0.0001f);
                float t = Mathf.Clamp01((centerDistanceFromTop - exitFadeStart) / exitRange);
                scaleFactor = Mathf.Min(scaleFactor, Mathf.Lerp(1f, minScale, t));
                alpha = Mathf.Min(alpha, Mathf.Lerp(1f, 0f, t));
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

    public void OnBeginDrag(PointerEventData eventData)
    {
        _isDragging = true;
        _isSnapping = false;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _isDragging = false;
        TryStartSnap();
    }

    private void TryStartSnap()
    {
        if (!enableSnapping || _scrollRect == null || viewport == null || _scrollRect.content == null)
        {
            return;
        }

        float maxOffset = Mathf.Max(0f, _scrollRect.content.rect.height - viewport.rect.height);
        if (maxOffset <= 0f)
        {
            return;
        }

        float secondSnapNormalized = Mathf.Clamp01(snapHeight / maxOffset);
        if (secondSnapNormalized <= SnapEpsilon)
        {
            return;
        }

        float current = _scrollRect.verticalNormalizedPosition;

        float lowerBound = -snapThresholdNormalized;
        float upperBound = secondSnapNormalized + snapThresholdNormalized;
        if (current < lowerBound || current > upperBound)
        {
            return;
        }

        float distanceToBottom = Mathf.Abs(current);
        float distanceToSecond = Mathf.Abs(current - secondSnapNormalized);

        _snapTargetNormalized = distanceToBottom <= distanceToSecond ? 0f : secondSnapNormalized;
        _snapStartNormalized = current;
        _snapTime = 0f;
        _isSnapping = true;
        _scrollRect.StopMovement();
    }

    private void HandleSnap(float deltaTime)
    {
        if (!enableSnapping || _scrollRect == null || viewport == null || _scrollRect.content == null)
        {
            return;
        }

        if (_isDragging)
        {
            return;
        }

        if (!_isSnapping)
        {
            if (Mathf.Abs(_scrollRect.velocity.y) <= snapVelocityThreshold)
            {
                TryStartSnap();
            }

            return;
        }

        _snapTime += deltaTime;
        float duration = Mathf.Max(0.0001f, snapDuration);
        float t = Mathf.Clamp01(_snapTime / duration);
        float eased = EaseOutCubic(t);
        float next = Mathf.Lerp(_snapStartNormalized, _snapTargetNormalized, eased);
        _scrollRect.verticalNormalizedPosition = next;

        if (Mathf.Abs(next - _snapTargetNormalized) <= SnapEpsilon)
        {
            _scrollRect.verticalNormalizedPosition = _snapTargetNormalized;
            _scrollRect.velocity = Vector2.zero;
            _isSnapping = false;
        }
    }

    private struct ItemState
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
