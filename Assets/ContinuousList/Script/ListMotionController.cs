using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems; // pointer over UI checks
using UnityEngine.UI;
using DG.Tweening; // Install DOTween (Demigiant) and set up
using UnityEngine.Events;

[DisallowMultipleComponent]
public class ListMotionController : MonoBehaviour
{

    [Header("Item Layout")]
    public float gap = 20f;
    public float overlapYOffset = -30f;
    public float zFront = 0f;
    public float zMid = -2f;
    public float zBack = -2.1f;

    [Header("Interaction")]
    public float scrollSensitivity = 1.0f;
    public float snapDuration = 0.3f;
    public Ease snapEaseType = Ease.OutBack; // exposed in Inspector
    public float snapOvershoot = 2.5f; // OutBack overshoot (higher = bouncier)

    [Header("Behavior")]
    public bool clampToBounds = true;
    public float dragPixelsPerUnit = 4f; // applied to touchpad scroll

    [Header("Items")]
    public List<ListItemView> items = new();

    [Header("Events")]
    public UnityEvent<int> onSnappedToIndex; // fired when a snap completes with highlighted index

    // Input area and camera removed for now; input always allowed

    // Internal state
    float _offset;            // continuous scroll offset (0..(count-1)*step)
    float _step;              // itemHeight + gap
    Tweener _snapTween;       // DOTween tween for snapping
    // Scroll snapping state
    bool _scrollSnapPending;
    float _lastScrollTime;
    const float ScrollSnapDelay = 0.15f; // seconds of no scroll before snapping
    // Pointer stillness gate (avoid snapping while fingers remain on touchpad)
    Vector2 _lastMousePos;
    float _lastMouseMoveTime;
    const float MouseStillDelay = 0.05f; // require pointer to be still briefly

    // Cached
    RectTransform _rect;

    // Last snapped (highlighted) index
    int _lastSnappedIndex = -1;
    public int LastSnappedIndex => _lastSnappedIndex;

    void Awake()
    {
        _rect = GetComponent<RectTransform>();

        // Auto-collect removed; assign items manually in Inspector

        // Index assignment
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] == null) continue;
            items[i].index = i;
        }

        RecomputeStep();
        ApplyLayoutImmediate();
    }

    void RecomputeStep()
    {
        float itemH = 110f; // fallback height
        // Try derive from first item rect height
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null && items[i].Rect != null)
            {
                itemH = Mathf.Abs(items[i].Rect.sizeDelta.y);
                break;
            }
        }

        _step = itemH + gap;
    }

    void Update()
    {
        // Always active; config is now local fields
        // Track pointer movement globally (used to infer touchpad contact)
        var mp = (Vector2)Input.mousePosition;
        if (mp != _lastMousePos)
        {
            _lastMousePos = mp;
            _lastMouseMoveTime = Time.unscaledTime;
        }
        HandleInput();
        // Snap after touchpad/mouse wheel scroll settles
        if (_scrollSnapPending
            && (Time.unscaledTime - _lastScrollTime) > ScrollSnapDelay
            && (Time.unscaledTime - _lastMouseMoveTime) > MouseStillDelay
            && !Input.GetMouseButton(0) && !Input.GetMouseButton(1) && !Input.GetMouseButton(2))
        {
            _scrollSnapPending = false;
            SnapToNearestStage();
        }
        ApplyLayoutImmediate(); // pure function of _offset
    }

    void HandleInput()
    {
        // Mouse wheel / touchpad scroll (most trackpads map two-finger to scrollDelta)
        float scrollY = Input.mouseScrollDelta.y; // +up / -down (reversed below)
        if (Mathf.Abs(scrollY) > Mathf.Epsilon)
        {
            // New scroll input cancels any existing snap tween
            KillSnap();
            float delta = scrollY * scrollSensitivity * _step * 0.2f;
            // Apply dragPixelsPerUnit to touchpad scroll as requested (higher = slower)
            delta /= Mathf.Max(1f, dragPixelsPerUnit);
            _offset -= delta; // reversed direction
            ClampOffset();
            _scrollSnapPending = true;
            _lastScrollTime = Time.unscaledTime;
        }
    }



    void ClampOffset()
    {
        if (!clampToBounds) return;
        float max = Mathf.Max(0, (items.Count - 1) * _step);
        _offset = Mathf.Clamp(_offset, 0f, max);
    }

    void KillSnap()
    {
        if (_snapTween != null && _snapTween.IsActive()) _snapTween.Kill(false);
        _snapTween = null;
    }

    void SnapToNearestStage()
    {
        float target = Mathf.Round(_offset / _step) * _step;
        int targetIndex = Mathf.RoundToInt(target / _step);

        // If already aligned, nothing to do
        if (Mathf.Abs(target - _offset) < 1e-4f)
            return;

        KillSnap();
        // DOTween smooth snap using configurable curve (softer ease)
        float duration = Mathf.Max(0.01f, snapDuration);
        _snapTween = DOVirtual.Float(_offset, target, duration, v => { _offset = v; })
            .OnComplete(() =>
            {
                _lastSnappedIndex = targetIndex;
                Debug.Log($"Snapped to index {_lastSnappedIndex}");
                if (onSnappedToIndex != null)
                    onSnappedToIndex.Invoke(_lastSnappedIndex);
            });
        if (snapEaseType == Ease.OutBack)
            _snapTween.SetEase(Ease.OutBack, snapOvershoot);
        else
            _snapTween.SetEase(snapEaseType);

    }

    void ApplyLayoutImmediate()
    {
        if (items == null || items.Count == 0) return;

        // Stage decomposition
        int k = Mathf.FloorToInt(_offset / _step);
        float baseK = k * _step;
        float t = 0f;
        if (_step > Mathf.Epsilon) t = Mathf.Clamp01((_offset - baseK) / _step);

        // For each item, compute pose at stage k (t=0) and k+1 (t=1), then lerp
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            if (it == null) continue;

            Pose p0 = PoseAtStage(k, i);
            Pose p1 = PoseAtStage(k + 1, i);

            float y = Mathf.Lerp(p0.y, p1.y, t);
            float z = Mathf.Lerp(p0.z, p1.z, t);
            it.SetYZ(y, z);

            // Outline alpha: item moving into A gains alpha with t; one leaving loses with t
            float alpha = 0f;
            if (i == k) alpha = 1f - t;        // currently at A, fading out
            else if (i == k + 1) alpha = t;    // moving into A, fading in
            else alpha = 0f;

            it.SetOutlineAlpha(alpha);
        }
    }

    struct Pose { public float y; public float z; public Pose(float yy, float zz) { y = yy; z = zz; } }

    Pose PoseAtStage(int stage, int itemIndex)
    {
        float yA = 0f;
        float zA = zFront;

        float yB = overlapYOffset; // now positive
        float zB = zMid;

        float yHidden = overlapYOffset;
        float zHidden = zBack;

        if (itemIndex == stage)
        {
            // posA
            return new Pose(yA, zA);
        }
        else if (itemIndex == stage + 1)
        {
            // posB
            return new Pose(yB, zB);
        }
        else if (itemIndex > stage + 1)
        {
            // Hidden
            return new Pose(yHidden, zHidden);
        }
        else // itemIndex < stage → already scrolled past A (above stack)
        {
            int stepsAbove = stage - itemIndex;
            float y = stepsAbove * _step; // now positive
            float z = zA;
            return new Pose(y, z);
        }
    }



}
