using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using UnityEngine;
using UnityEngine.Events;
using DG.Tweening;
[DisallowMultipleComponent]
public class ListMotionController : MonoBehaviour {
    [Header("Item Layout")]
    public float gap = 20f;
    public float overlapYOffset = -30f;
    public float zFront = 0f;
    public float zMid = -2f;
    public float zBack = -2.1f;
    [Header("Interaction")]
    public float scrollSensitivity = 1.0f;
    [Header("Inertia + Snap")]
    public float inertiaDamping = 8f;
    public float maxVelocity = 5000f;
    public bool enableSnap = true;
    public float snapVelocityThreshold = 50f;
    public float snapSpeed = 15f;
    [Header("Selection Gate")]
    public float selectionZThreshold = 10f;
    [Header("Items")]
    public List<ListItemView> items = new();
    public bool autoCollectChildren = false;
    [Header("Title Item")]
    public GameObject titleItem;
    public float titleScrollThreshold = 1.0f;
    public float titleScrolledZOffset = 20f;
    [Header("Audio")]
    [Tooltip("Audio clip to play when a new element is highlighted/selected")]
    public AudioClip selectionAudioClip;
    [Header("Events")]
    public UnityEvent<int> onSnappedToIndex;
    public float dragTimeout = 0.2f;
    public float velocity;
    public int SelectedIndex => _selectedIndex; 
    float _offset;
    float _step;
    float _itemHeight;
    float _edgeY;
    int _selectedIndex = 0;
    bool _isDragging = false;
    float _dragTimer = 0f;
    float _max = 0f;
    private Coroutine _timedSnapCoroutine;
    RectTransform _rect;
    CanvasGroup _titleCanvasGroup;
    AudioSource _audioSource;
    Tween _titleOverrideTween;
    float _titleOverrideProgress = 0f;
    struct Pose { public float y; public float z; public Pose(float yy, float zz) { y = yy; z = zz; } }
    void Awake() {
        _rect = GetComponent<RectTransform>();
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null) {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
        }
        if (autoCollectChildren) AutoCollectItems();
        for (int i = 0; i < items.Count; i++) if (items[i] != null) items[i].index = i;
        if (titleItem != null) {
            _titleCanvasGroup = titleItem.GetComponent<CanvasGroup>() ?? titleItem.AddComponent<CanvasGroup>();
            // Reset title position before animation
            Vector3 initialTitlePos = titleItem.transform.localPosition;
            initialTitlePos.z = 0f;
            titleItem.transform.localPosition = initialTitlePos;
        }
        RecomputeStep();
        ApplyLayoutImmediate();
    }
    void OnDestroy() {
        _titleOverrideTween?.Kill();
        _titleOverrideTween = null;
    }
    void AutoCollectItems() {
        if (items == null) items = new List<ListItemView>();
        items.Clear();
        int childCount = transform.childCount;
        for (int i = childCount - 1; i >= 0; i--) {
            var view = transform.GetChild(i).GetComponent<ListItemView>();
            if (view != null) items.Add(view);
        }
        for (int i = 0; i < items.Count; i++) if (items[i] != null) items[i].index = i;
    }
    void RecomputeStep() {
        float itemHeight = 120f;
        if (items.Count > 0 && items[0] != null && items[0].Rect != null) {
            itemHeight = Mathf.Abs(items[0].Rect.sizeDelta.y);
        }
        _itemHeight = itemHeight;
        _step = itemHeight + gap;
        _edgeY = itemHeight * 3f + 30f;

        _max = Mathf.Max(0, (items.Count - 1) * _step);
    }
    void Update() {
        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f) return;
        // Drag Scrolling Logic
        if (_isDragging) {
            _offset -= velocity * dt;
            _dragTimer -= dt;
            if (_dragTimer <= 0f) {
                _isDragging = false;
            }
        }
        // Momentum Scrolling Logic
        else if (velocity != 0f) {
            _offset -= velocity * dt;
            velocity *= Mathf.Exp(-inertiaDamping * dt);
            velocity = Mathf.Clamp(velocity, -maxVelocity, maxVelocity);
        }
        // Scrolled to edges
        if (_offset < 0f) { _offset = 0f; velocity = 0f; }
        if (_offset > _max) { _offset = _max; velocity = 0f; }
        // Snapping Logic
        if (!_isDragging && enableSnap && Mathf.Abs(velocity) < snapVelocityThreshold) {
            float targetIndex = Mathf.Round(_offset / _step);
            float targetOffset = Mathf.Clamp(targetIndex * _step, 0f, _max);
            float distanceToTarget = targetOffset - _offset;
            if (Mathf.Abs(distanceToTarget) > 0.1f) {
                float snapDirection = Mathf.Sign(distanceToTarget);
                _offset += snapDirection * snapSpeed * dt;
                velocity = 0f;
                if (snapDirection > 0 && _offset > targetOffset)
                    _offset = targetOffset;
                if (snapDirection < 0 && _offset < targetOffset)
                    _offset = targetOffset;
            } else {
                _offset = targetOffset;
                velocity = 0f;
            }
        }
        ApplyLayoutImmediate();
    }
    
    public void UpdateRawInput(float signedInt, bool isTouch) {
        float newVelocity = signedInt * scrollSensitivity;
        newVelocity = Mathf.Clamp(newVelocity, -maxVelocity, maxVelocity);
        velocity = newVelocity;
        _isDragging = true;
        _dragTimer = dragTimeout;
    }
    
    public void snapInTime(float t, System.Action onComplete) {
        if (!enableSnap || t <= 0f)
            return;
        if (_timedSnapCoroutine != null) {
            StopCoroutine(_timedSnapCoroutine);
        }
        _timedSnapCoroutine = StartCoroutine(TimedSnapCoroutine(t, onComplete));
    }
    public void AnimateTitleToScrolledOffset(float duration = 0.5f) {
        if (titleItem == null)
            return;
        _titleOverrideTween?.Kill();
        if (duration <= 0f) {
            _titleOverrideProgress = 1f;
            ApplyLayoutImmediate();
            return;
        }
        _titleOverrideTween = DOTween.To(() => _titleOverrideProgress,
            value => {
                _titleOverrideProgress = value;
                ApplyLayoutImmediate();
            },
            1f, duration)
            .SetEase(Ease.InOutSine)
            .OnKill(() => _titleOverrideTween = null);
    }
    public void ResetTitleOverride(float duration = 0.5f) {
        if (titleItem == null)
            return;
        _titleOverrideTween?.Kill();
        if (duration <= 0f) {
            _titleOverrideProgress = 0f;
            ApplyLayoutImmediate();
            return;
        }
        _titleOverrideTween = DOTween.To(() => _titleOverrideProgress,
            value => {
                _titleOverrideProgress = value;
                ApplyLayoutImmediate();
            },
            0f, duration)
            .SetEase(Ease.InOutSine)
            .OnKill(() => _titleOverrideTween = null);
    }
    
    private IEnumerator TimedSnapCoroutine(float duration, System.Action onComplete = null) {
        float max = (items.Count - 1) * _step;
        float targetIndex = Mathf.Round(_offset / _step);
        float targetOffset = Mathf.Clamp(targetIndex * _step, 0f, max);
        
        float startOffset = _offset;
        float elapsed = 0f;
        
        velocity = 0f;
        _isDragging = false;
        
        while (elapsed < duration) {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            
            // Use smooth step for easing
            float smoothProgress = progress * progress * (3f - 2f * progress);
            _offset = Mathf.Lerp(startOffset, targetOffset, smoothProgress);
            
            ApplyLayoutImmediate();
            yield return null;
        }
        
        // Ensure we end exactly at target
        _offset = targetOffset;
        ApplyLayoutImmediate();
        _timedSnapCoroutine = null;
        
        // Call completion callback if provided
        onComplete?.Invoke();
    }
    void ApplyLayoutImmediate() {
        if (items == null || items.Count == 0) return;
        int k = Mathf.FloorToInt(_offset / _step);
        float baseK = k * _step;
        float t = (_step > Mathf.Epsilon) ? Mathf.Clamp01((_offset - baseK) / _step) : 0f;
        int candidate = -1;
        float bestYDist = float.PositiveInfinity;
        for (int i = 0; i < items.Count; i++) {
            var it = items[i]; if (it == null) continue;
            Pose p0 = PoseAtStage(k, i);
            Pose p1 = PoseAtStage(k + 1, i);
            float y = Mathf.Lerp(p0.y, p1.y, t);
            float z = Mathf.Lerp(p0.z, p1.z, t);
            float zDist = Mathf.Abs(z - zFront);
            if (zDist <= selectionZThreshold) {
                float yDist = Mathf.Abs(y);
                if (yDist < bestYDist) { bestYDist = yDist; candidate = i; }
            }
        }
        if (candidate >= 0 && candidate != _selectedIndex) {
            PlaySelectionAudio();
            _selectedIndex = candidate;
        }
        for (int i = 0; i < items.Count; i++) {
            var it = items[i]; if (it == null) continue;
            Pose p0 = PoseAtStage(k, i);
            Pose p1 = PoseAtStage(k + 1, i);
            float y = Mathf.Lerp(p0.y, p1.y, t);
            float z = Mathf.Lerp(p0.z, p1.z, t);
            float topY = y + _itemHeight;
            float squeezeT = 0f;
            if (topY > _edgeY) {
                float delta = topY - _edgeY;
                y -= delta * 0.9f;
                squeezeT = Mathf.Clamp01(delta / _itemHeight);
            }
            it.SetYZ(y, z);
            it.SetEdgeSqueeze(squeezeT, z);
            it.SetContentAlphaBasedOnZ(z, zMid, zFront);
            it.SetRootAlphaBasedOnZ(z, zBack);
            it.SetOutlineAlpha(i == _selectedIndex ? 1f : 0f);
        }
        if (titleItem != null) {
            float titleProgress = (titleScrollThreshold > 0) ? Mathf.Clamp01(_offset / titleScrollThreshold) : 0f;
            if (_titleOverrideProgress > titleProgress)
                titleProgress = _titleOverrideProgress;
            Vector3 titlePos = titleItem.transform.localPosition;
            titlePos.z = Mathf.Lerp(0, titleScrolledZOffset, titleProgress);
            titleItem.transform.localPosition = titlePos;
            if (_titleCanvasGroup != null) _titleCanvasGroup.alpha = Mathf.Lerp(1f, 0f, titleProgress);
        }
    }
    void PlaySelectionAudio() {
        if (_audioSource != null && selectionAudioClip != null) {
            _audioSource.PlayOneShot(selectionAudioClip);
        }
    }
    Pose PoseAtStage(int stage, int itemIndex) {
        if (itemIndex == stage) return new Pose(0f, zFront);
        if (itemIndex == stage + 1) return new Pose(overlapYOffset, zMid);
        if (itemIndex > stage + 1) return new Pose(overlapYOffset, zBack);
        int stepsAbove = stage - itemIndex;
        return new Pose(stepsAbove * _step, zFront);
    }
}
