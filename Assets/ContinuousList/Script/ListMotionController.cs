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

    [Header("Title Item")]
    public GameObject titleItem; // Assign the title item GameObject in the Inspector
    public float titleScrollThreshold = 1.0f; // How much to scroll before title is fully gone
    public float titleScrolledZOffset = 20f; // Target Z position when scrolled

    [Header("Events")]
    public UnityEvent<int> onSnappedToIndex; // fired when a snap completes with highlighted index

    [Header("Scroll Sounds")]
    public bool clickSoundEnabled = true;
    public AudioClip clickClip;
    [Range(0f,1f)] public float clickVolume = 0.5f;
    [Tooltip("Random pitch variation (+/-) for each tick.")]
    [Range(0f,0.5f)] public float clickPitchJitter = 0.05f;
    [Tooltip("Minimum time between ticks (seconds)")]
    [Range(0f,0.2f)] public float minTickInterval = 0.03f;

    // Input area and camera removed for now; input always allowed

    // Internal state
    float _offset;            // continuous scroll offset (0..(count-1)*step)
    float _step;              // itemHeight + gap
    float _itemHeight;        // cached item height
    float _edgeY;             // top edge for squeeze behavior
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
    CanvasGroup _titleCanvasGroup;
    TitleBlurEffect _titleBlurEffect; // We will create this script next

    // Last snapped (highlighted) index
    int _lastSnappedIndex = -1;
    public int LastSnappedIndex => _lastSnappedIndex;

    // Sound state
    AudioSource _audio;
    int _lastTickIndex = -1;
    float _lastTickTime = -999f;

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

        // Cache title item components
        if (titleItem != null)
        {
            _titleCanvasGroup = titleItem.GetComponent<CanvasGroup>();
            if (_titleCanvasGroup == null)
            {
                _titleCanvasGroup = titleItem.AddComponent<CanvasGroup>();
            }
            _titleBlurEffect = titleItem.GetComponent<TitleBlurEffect>();
            if (_titleBlurEffect == null)
            {
                // We will create this script later.
                // For now, we'll just log a warning if it's not attached.
                Debug.LogWarning("TitleBlurEffect component not found on titleItem. Please attach it for blur effect.", titleItem);
            }
        }

        RecomputeStep();
        ApplyLayoutImmediate();

        // Prepare audio (optional)
        _audio = GetComponent<AudioSource>();
        if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.loop = false;
        _audio.spatialBlend = 0f;
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
        _itemHeight = itemH;
        _step = _itemHeight + gap;
        _edgeY = _itemHeight * 3f + gap * 2f;
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

        // Play tick when the nearest index changes
        PlayScrollTickIfNeeded();
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

    public void UpdateRawInput(float signedInt, bool isTouch)
    {
        // New scroll input cancels any existing snap tween
        KillSnap();
        
        // Apply the raw input value, scaled by sensitivity and step
        float delta = signedInt * scrollSensitivity * _step * 0.1f; // Adjusted scaling for raw input
        _offset -= delta; // reversed direction to match typical scroll feel
        ClampOffset();
        
        _scrollSnapPending = true;
        _lastScrollTime = Time.unscaledTime;
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

    void PlayScrollTickIfNeeded()
    {
        if (!clickSoundEnabled || clickClip == null || _step <= Mathf.Epsilon) return;
        int selectedIndex = Mathf.Clamp(Mathf.RoundToInt(_offset / _step), 0, Mathf.Max(0, items.Count - 1));
        if (selectedIndex != _lastTickIndex)
        {
            // Rate-limit to avoid double-fire in the same frame
            if (Time.unscaledTime - _lastTickTime >= minTickInterval)
            {
                _lastTickTime = Time.unscaledTime;
                _lastTickIndex = selectedIndex;
                if (_audio != null)
                {
                    float basePitch = 1f;
                    float jitter = (clickPitchJitter > 0f) ? UnityEngine.Random.Range(-clickPitchJitter, clickPitchJitter) : 0f;
                    _audio.pitch = basePitch + jitter;
                    _audio.PlayOneShot(clickClip, clickVolume);
                }
            }
        }
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
        // Determine the currently "selected" item as the one closest to the A position
        int selectedIndex = Mathf.Clamp(Mathf.RoundToInt(_offset / _step), 0, Mathf.Max(0, items.Count - 1));
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            if (it == null) continue;

            Pose p0 = PoseAtStage(k, i);
            Pose p1 = PoseAtStage(k + 1, i);

            float y = Mathf.Lerp(p0.y, p1.y, t);
            float z = Mathf.Lerp(p0.z, p1.z, t);

            float topY = y + _itemHeight;
            float squeezeT = 0f;
            if (topY > _edgeY)
            {
                float delta = topY - _edgeY;
                // Move the item down so its top is pinned to the edge
                y -= delta;
                // Drive squeeze based on how far beyond the edge the top would have gone
                squeezeT = Mathf.Clamp01(delta / _itemHeight);
            }

            it.SetYZ(y, z);
            it.SetEdgeSqueeze(squeezeT, _itemHeight);
            it.SetContentAlphaBasedOnZ(z, zMid, zFront);

            // Outline alpha: hard-select the closest item; others at min alpha
            float alpha = (i == selectedIndex) ? 1f : 0f;

            it.SetOutlineAlpha(alpha);
        }

        // Apply title item effects
        if (titleItem != null)
        {
            float titleProgress = 0f;
            if (titleScrollThreshold > 0)
            {
                titleProgress = Mathf.Clamp01(_offset / titleScrollThreshold);
            }

            // Z-axis movement
            Vector3 titlePos = titleItem.transform.localPosition;
            titlePos.z = Mathf.Lerp(0, titleScrolledZOffset, titleProgress);
            titleItem.transform.localPosition = titlePos;

            // Fade out
            if (_titleCanvasGroup != null)
            {
                _titleCanvasGroup.alpha = Mathf.Lerp(1f, 0f, titleProgress);
            }

            // Blur effect
            if (_titleBlurEffect != null)
            {
                _titleBlurEffect.BlurAmount = titleProgress; // Assuming BlurAmount is a property from 0 to 1
            }
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
