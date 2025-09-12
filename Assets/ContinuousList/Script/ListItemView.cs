using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
#if TMP_PRESENT || true
using TMPro;
#endif

[DisallowMultipleComponent]
public class ListItemView : MonoBehaviour
{
    // Auto-referenced; not exposed in Inspector
    RectTransform rect;
    Transform depthTarget;
    CanvasGroup rootGroup;
    CanvasGroup outlineGroup;
    RectTransform outlineRect;
    Image outlineImage;
    CanvasGroup contentGroup;
    RectTransform contentRect;
    RectTransform bgRect;
    Image bgImage;

#if TMP_PRESENT || true
    TMP_Text subtitleTMP;
#endif
    Text subtitleUGUI;
    CanvasGroup subtitleGroup;

    [Header("Subtitle Texts")]
    [Tooltip("Short version shown in list view (optional; falls back to existing subtitle text).")]
    [TextArea(1, 4)] public string shortSubtitleText;
    [Tooltip("Long version shown when item opens.")]
    [TextArea(2, 6)] public string longSubtitleText;

    [HideInInspector] public int index; // assigned by controller

    const float MinOutlineAlpha = 0.1f; // clamp range is [0.2, 1]
    float _contentAlphaFromZ = 1f; // Stores alpha based on Z-position
    float _edgeContainerAlphaFactor = 1f; // Multiplies container visuals (outline/bg) during top squeeze
    float _edgeContainerAlphaVisual = 1f; // Smoothed visual alpha for container during squeeze
    [Header("Smoothing")]
    [Tooltip("Higher values make outline alpha ease more slowly to target (smoother). Units are 1/seconds in an exponential ease.")]
    [Range(1f, 20f)] public float outlineAlphaSmoothing = 8f;
    [Tooltip("Smoothing for the container (outline/bg) alpha during edge squeeze. Units are 1/seconds in an exponential ease.")]
    [Range(1f, 30f)] public float edgeContainerAlphaSmoothing = 16f;
    [Tooltip("Smoothing for the container height during edge squeeze/unsqueeze. Units are 1/seconds in an exponential ease.")]
    [Range(1f, 40f)] public float edgeContainerSizeSmoothing = 20f;
    [Header("Edge Unsqueeze Tween")]
    [Tooltip("Time to stretch back to full height when leaving the top squeeze (seconds).")]
    [Range(0.05f, 0.5f)] public float unsqueezeDuration = 0.15f;
    public Ease unsqueezeEase = Ease.OutCubic;
    float _outlineVisualAlpha = MinOutlineAlpha;
    float _containerHeightVisual = -1f; // lazy-initialized to current height
    float _lastSqueezeT = 0f;
    Tween _edgeHeightTween;
    float? _unsqueezeDurationOverride;
    Ease _unsqueezeEaseOverride = Ease.OutCubic;

    void KillEdgeHeightTween()
    {
        if (_edgeHeightTween != null && _edgeHeightTween.IsActive())
            _edgeHeightTween.Kill(false);
        _edgeHeightTween = null;
    }

    void StartUnsqueezeTween(float itemHeight)
    {
        KillEdgeHeightTween();
        float dur = _unsqueezeDurationOverride.HasValue ? Mathf.Max(0f, _unsqueezeDurationOverride.Value) : unsqueezeDuration;
        Ease ease = _unsqueezeDurationOverride.HasValue ? _unsqueezeEaseOverride : unsqueezeEase;
        Sequence seq = DOTween.Sequence().SetUpdate(true); // unscaled time
        if (outlineRect != null)
        {
            seq.Join(outlineRect.DOSizeDelta(new Vector2(outlineRect.sizeDelta.x, itemHeight), dur).SetEase(ease));
        }
        if (bgRect != null)
        {
            seq.Join(bgRect.DOSizeDelta(new Vector2(bgRect.sizeDelta.x, itemHeight), dur).SetEase(ease));
        }
        seq.OnUpdate(() =>
        {
            float h = itemHeight;
            if (outlineRect != null) h = outlineRect.sizeDelta.y;
            else if (bgRect != null) h = bgRect.sizeDelta.y;
            _containerHeightVisual = h;
        });
        seq.OnComplete(() =>
        {
            _containerHeightVisual = itemHeight;
            _edgeHeightTween = null;
        });
        _edgeHeightTween = seq;
    }

    // Allow external controller to sync unsqueeze tween duration/ease (e.g., match close duration)
    public void SetUnsqueezeTweenOverride(float duration, Ease ease)
    {
        _unsqueezeDurationOverride = Mathf.Max(0f, duration);
        _unsqueezeEaseOverride = ease;
    }

    public void ClearUnsqueezeTweenOverride()
    {
        _unsqueezeDurationOverride = null;
    }

    // Public accessors (not shown in Inspector)
    public RectTransform Rect
    {
        get
        {
            if (rect == null) rect = GetComponent<RectTransform>();
            return rect;
        }
    }

    public Transform DepthTarget
    {
        get
        {
            if (depthTarget == null) depthTarget = transform;
            return depthTarget;
        }
    }

    CanvasGroup OutlineGroup
    {
        get
        {
            if (outlineGroup == null)
            {
                // Prefer a child explicitly named "Outline"
                Transform t = null;
                // Try direct child first
                var direct = transform.Find("Outline");
                if (direct != null) t = direct;
                else
                {
                    // Fallback: search any depth for a Transform named "Outline"
                    var all = GetComponentsInChildren<Transform>(true);
                    foreach (var tr in all)
                    {
                        if (tr != null && tr.name == "Outline") { t = tr; break; }
                    }
                }

                if (t != null)
                {
                    outlineRect = t.GetComponent<RectTransform>();
                    outlineImage = t.GetComponent<Image>();
                    outlineGroup = t.GetComponent<CanvasGroup>();
                }
            }
            return outlineGroup;
        }
    }

    CanvasGroup ContentGroup
    {
        get
        {
            if (contentGroup == null)
            {
                Transform t = null;
                var direct = transform.Find("Content");
                if (direct != null) t = direct;
                else
                {
                    var all = GetComponentsInChildren<Transform>(true);
                    foreach (var tr in all)
                    {
                        if (tr != null && tr.name == "Content") { t = tr; break; }
                    }
                }

                if (t != null)
                {
                    contentGroup = t.GetComponent<CanvasGroup>();
                    contentRect = t.GetComponent<RectTransform>();
                }
            }
            return contentGroup;
        }
    }

    public RectTransform ContentRect
    {
        get
        {
            if (contentRect == null)
            {
                // Force ContentGroup resolution to also capture rect
                var _ = ContentGroup;
            }
            return contentRect;
        }
    }

    CanvasGroup RootGroup
    {
        get
        {
            if (rootGroup == null)
            {
                rootGroup = GetComponent<CanvasGroup>();
                if (rootGroup == null)
                {
                    rootGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }
            return rootGroup;
        }
    }

    RectTransform BGRect
    {
        get
        {
            if (bgRect == null)
            {
                Transform t = null;
                var direct = transform.Find("BG");
                if (direct != null) t = direct;
                else
                {
                    var all = GetComponentsInChildren<Transform>(true);
                    foreach (var tr in all)
                    {
                        if (tr != null && tr.name == "BG") { t = tr; break; }
                    }
                }

                if (t != null)
                {
                    bgRect = t.GetComponent<RectTransform>();
                    bgImage = t.GetComponent<Image>();
                    Debug.Log($"Successfully found BG object for {gameObject.name}", this);
                }
                else
                {
                    Debug.LogWarning($"Could not find BG object for {gameObject.name}", this);
                }
            }
            return bgRect;
        }
    }

    void Awake()
    {
        // Ensure auto references are set
        if (rect == null) rect = GetComponent<RectTransform>();
        if (depthTarget == null) depthTarget = transform;
        if (outlineGroup == null)
        {
            // resolve on awake
            var _ = OutlineGroup;
        }
        if (contentGroup == null)
        {
            var __ = ContentGroup;
        }
        // Resolve BG on awake to trigger debug log immediately
        var ___ = BGRect;
    }

    void Reset()
    {
        // Auto assign on add/reset in editor
        rect = GetComponent<RectTransform>();
        depthTarget = transform;
        outlineGroup = null; // will be resolved via property lookup
        outlineRect = null;
        outlineImage = null;
        contentGroup = null;
        contentRect = null;

        // Capture current subtitle as default short text for convenience
        var t = FindByNameDeep("subtitle");
        if (t != null)
        {
#if TMP_PRESENT || true
            var tmp = t.GetComponent<TMP_Text>();
            if (tmp != null) shortSubtitleText = tmp.text;
            else
#endif
            {
                var ui = t.GetComponent<Text>();
                if (ui != null) shortSubtitleText = ui.text;
            }
        }
    }

    public void SetYZ(float y, float z)
    {
        var r = Rect; // ensures cached
        if (r != null)
        {
            var lp = r.anchoredPosition;
            lp.y = y;
            r.anchoredPosition = lp;
        }

        var dt = DepthTarget;
        if (dt != null)
        {
            var pos = dt.localPosition;
            pos.z = z;
            dt.localPosition = pos;
        }
    }

    public void SetOutlineAlpha(float normalized)
    {
        var g = OutlineGroup;
        if (g == null) return;

        // Map [0,1] → [0.2,1]
        float t = Mathf.Clamp01(normalized);
        float target = Mathf.Lerp(MinOutlineAlpha, 1f, t);

        // Exponential smoothing toward target using unscaled deltaTime
        float dt = Mathf.Max(0f, Time.unscaledDeltaTime);
        float k = 1f - Mathf.Exp(-outlineAlphaSmoothing * dt);
        _outlineVisualAlpha = Mathf.Lerp(_outlineVisualAlpha, target, k);

        g.alpha = _outlineVisualAlpha * _edgeContainerAlphaFactor;
    }

    public void SetContentAlphaBasedOnZ(float currentZ, float zMid, float zFront)
    {
        // Calculate the interpolation factor 't' based on the current Z position
        float t = 0f;
        if (zFront != zMid) // Avoid division by zero
        {
            t = Mathf.Clamp01((currentZ - zMid) / (zFront - zMid));
            // Apply a curve so content fades out quicker when moving away from front
            t = t * t; // quadratic curve: reduces faster as z moves back
        }

        // Map [0,1] → [0.2,1] and store it
        _contentAlphaFromZ = Mathf.Lerp(0.1f, 1f, t);
    }

    public void SetEdgeSqueeze(float normalized, float itemHeight)
    {
        float t = Mathf.Clamp01(normalized);
        float newH = Mathf.Lerp(itemHeight, 0f, t);
        // With anchors min/max (0,1) and pivot (0,1), resizing keeps the top fixed.
        // No vertical offset is needed to maintain top anchoring.

        // Detect crossing out of squeeze and run an explicit unsqueeze tween
        if (t <= 0.001f && _lastSqueezeT > 0.01f)
        {
            StartUnsqueezeTween(itemHeight);
        }
        else if (t > 0.001f)
        {
            // While squeezed, cancel any running unsqueeze tween
            KillEdgeHeightTween();
        }

        // Initialize container visual height if needed
        if (_containerHeightVisual < 0f)
        {
            float current = itemHeight;
            if (outlineRect != null) current = outlineRect.sizeDelta.y;
            else if (bgRect != null) current = bgRect.sizeDelta.y;
            _containerHeightVisual = current;
        }
        // Smoothly approach target height
        float dtH = Mathf.Max(0f, Time.unscaledDeltaTime);
        float kH = 1f - Mathf.Exp(-edgeContainerSizeSmoothing * dtH);
        if (_edgeHeightTween == null || !_edgeHeightTween.IsActive())
            _containerHeightVisual = Mathf.Lerp(_containerHeightVisual, newH, kH);

        if (outlineRect != null && (_edgeHeightTween == null || !_edgeHeightTween.IsActive()))
        {
            var size = outlineRect.sizeDelta;
            size.y = _containerHeightVisual;
            outlineRect.sizeDelta = size;
        }

        if (outlineImage != null)
        {
            outlineImage.pixelsPerUnitMultiplier = Mathf.Lerp(1f, 2f, t);
        }

        if (bgRect != null && (_edgeHeightTween == null || !_edgeHeightTween.IsActive()))
        {
            var size = bgRect.sizeDelta;
            size.y = _containerHeightVisual;
            bgRect.sizeDelta = size;
        }

        if (bgImage != null)
        {
            bgImage.pixelsPerUnitMultiplier = Mathf.Lerp(5f, 10f, t);
        }

        var cg = ContentGroup;
        if (cg != null)
        {
            // Keep content fade behavior as before (independent of the container quick-fade)
            float edgeAlpha = 1f - Mathf.Clamp01(t * 5f); // content fades with squeeze, quicker but continuous
            cg.alpha = _contentAlphaFromZ * edgeAlpha;
        }

        // Smoothly adjust container fade across full squeeze range to avoid sudden pop-in
        // Target curve: fully visible at t=0, fades toward 0 as t→1
        float containerTarget = 1f - t; // linear falloff (simple and predictable)
        float dt = Mathf.Max(0f, Time.unscaledDeltaTime);
        float k = 1f - Mathf.Exp(-edgeContainerAlphaSmoothing * dt);
        _edgeContainerAlphaVisual = Mathf.Lerp(_edgeContainerAlphaVisual, containerTarget, k);
        _edgeContainerAlphaFactor = _edgeContainerAlphaVisual;

        // Optionally dim BG image directly
        if (bgImage != null)
        {
            var c = bgImage.color;
            c.a = _edgeContainerAlphaFactor;
            bgImage.color = c;
        }

        _lastSqueezeT = t;
    }

    // Variant used during subpage animations for items above: adjusts sizes only, leaves alphas untouched
    public void SetEdgeSqueeze_NoAlpha(float normalized, float itemHeight)
    {
        float t = Mathf.Clamp01(normalized);
        float newH = Mathf.Lerp(itemHeight, 0f, t);

        // Detect crossing out of squeeze and run an explicit unsqueeze tween
        if (t <= 0.001f && _lastSqueezeT > 0.01f)
        {
            StartUnsqueezeTween(itemHeight);
        }
        else if (t > 0.001f)
        {
            KillEdgeHeightTween();
        }

        // Initialize container visual height if needed
        if (_containerHeightVisual < 0f)
        {
            float current = itemHeight;
            if (outlineRect != null) current = outlineRect.sizeDelta.y;
            else if (bgRect != null) current = bgRect.sizeDelta.y;
            _containerHeightVisual = current;
        }
        // Smoothly approach target height
        float dtH = Mathf.Max(0f, Time.unscaledDeltaTime);
        float kH = 1f - Mathf.Exp(-edgeContainerSizeSmoothing * dtH);
        if (_edgeHeightTween == null || !_edgeHeightTween.IsActive())
            _containerHeightVisual = Mathf.Lerp(_containerHeightVisual, newH, kH);

        if (outlineRect != null && (_edgeHeightTween == null || !_edgeHeightTween.IsActive()))
        {
            var size = outlineRect.sizeDelta;
            size.y = _containerHeightVisual;
            outlineRect.sizeDelta = size;
        }

        if (outlineImage != null)
        {
            outlineImage.pixelsPerUnitMultiplier = Mathf.Lerp(1f, 2f, t);
        }

        if (bgRect != null && (_edgeHeightTween == null || !_edgeHeightTween.IsActive()))
        {
            var size = bgRect.sizeDelta;
            size.y = _containerHeightVisual;
            bgRect.sizeDelta = size;
        }

        if (bgImage != null)
        {
            bgImage.pixelsPerUnitMultiplier = Mathf.Lerp(5f, 10f, t);
        }
        // Intentionally do not touch ContentGroup.alpha, OutlineGroup.alpha, or bgImage.color.a here

        _lastSqueezeT = t;
    }

    // -------- Subpage helpers --------

    Transform FindByNameDeep(string target)
    {
        var all = GetComponentsInChildren<Transform>(true);
        foreach (var tr in all)
        {
            if (tr != null && tr.name == target) return tr;
        }
        return null;
    }

    public void SetSubtitleText(string text)
    {
#if TMP_PRESENT || true
        if (subtitleTMP == null)
        {
            var t = FindByNameDeep("subtitle");
            if (t != null)
            {
                subtitleTMP = t.GetComponent<TMP_Text>();
                if (subtitleTMP == null) subtitleUGUI = t.GetComponent<Text>();
            }
        }
        if (subtitleTMP != null)
        {
            subtitleTMP.text = text;
        }
        else
#endif
        {
            if (subtitleUGUI == null)
            {
                var t = FindByNameDeep("subtitle");
                if (t != null) subtitleUGUI = t.GetComponent<Text>();
            }
            if (subtitleUGUI != null) subtitleUGUI.text = text;
        }

        // Force layout to rebuild so Content height updates immediately
        var cr = ContentRect;
        if (cr != null)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(cr);
            Canvas.ForceUpdateCanvases();
        }
    }

    public string GetSubtitleText()
    {
#if TMP_PRESENT || true
        if (subtitleTMP == null && subtitleUGUI == null)
        {
            var t = FindByNameDeep("subtitle");
            if (t != null)
            {
                subtitleTMP = t.GetComponent<TMP_Text>();
                if (subtitleTMP == null) subtitleUGUI = t.GetComponent<Text>();
            }
        }
        if (subtitleTMP != null) return subtitleTMP.text;
#endif
        if (subtitleUGUI != null) return subtitleUGUI.text;
        return null;
    }

    CanvasGroup SubtitleGroup
    {
        get
        {
            if (subtitleGroup == null)
            {
                var t = FindByNameDeep("subtitle");
                if (t != null)
                {
                    subtitleGroup = t.GetComponent<CanvasGroup>();
                    if (subtitleGroup == null) subtitleGroup = t.gameObject.AddComponent<CanvasGroup>();
                }
            }
            return subtitleGroup;
        }
    }

    public void SetSubtitleAlpha(float a)
    {
        var g = SubtitleGroup;
        if (g != null) g.alpha = a;
    }

    public Tween FadeSubtitle(float targetAlpha, float duration)
    {
        var g = SubtitleGroup;
        return g != null ? g.DOFade(targetAlpha, duration) : null;
    }

    public Tween SetSubtitleAlpha(float targetAlpha, float duration, Ease ease = Ease.OutQuad)
    {
        var g = SubtitleGroup;
        if (g == null) return null;
        if (duration <= 0f)
        {
            g.alpha = targetAlpha;
            return null;
        }
        return g.DOFade(targetAlpha, duration).SetEase(ease);
    }

    public float GetContentHeight()
    {
        var cr = ContentRect;
        if (cr == null) return 0f;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(cr);
        return cr.rect.height;
    }

    public float MeasureHeightWithText(string text)
    {
        // Temporarily swap subtitle text to measure resulting Content height
        // Do it invisibly to avoid visual flicker
        string prev = GetSubtitleText();
        float prevAlpha = 1f;
        var g = SubtitleGroup;
        if (g != null) { prevAlpha = g.alpha; g.alpha = 0f; }

        SetSubtitleText(text);
        float h = GetContentHeight();

        if (!string.IsNullOrEmpty(prev)) SetSubtitleText(prev);
        if (g != null) g.alpha = prevAlpha;
        return h;
    }

    public Tween AnimateContainerHeightTo(float height, float duration)
    {
        Tween t = null;
        if (outlineRect != null)
        {
            t = outlineRect.DOSizeDelta(new Vector2(outlineRect.sizeDelta.x, height), duration);
        }
        if (bgRect != null)
        {
            var tb = bgRect.DOSizeDelta(new Vector2(bgRect.sizeDelta.x, height), duration);
            if (t == null) t = tb; else t = DOTween.Sequence().Join(t).Join(tb);
        }
        return t;
    }

    public Tween AnimateContainerHeightTo(float height, float duration, Ease ease)
    {
        Tween t = null;
        if (outlineRect != null)
        {
            t = outlineRect.DOSizeDelta(new Vector2(outlineRect.sizeDelta.x, height), duration).SetEase(ease);
        }
        if (bgRect != null)
        {
            var tb = bgRect.DOSizeDelta(new Vector2(bgRect.sizeDelta.x, height), duration).SetEase(ease);
            if (t == null) t = tb; else t = DOTween.Sequence().Join(t).Join(tb);
        }
        return t;
    }

    public Tween AnimateContainerHeightToContent(float duration)
    {
        float h = GetContentHeight();
        return AnimateContainerHeightTo(h, duration);
    }

    public Tween MoveByY(float delta, float duration, Ease ease)
    {
        var r = Rect;
        if (r == null) return null;
        return r.DOAnchorPosY(r.anchoredPosition.y + delta, duration).SetEase(ease);
    }

    public Sequence FadeAll(float targetAlpha, float duration)
    {
        var seq = DOTween.Sequence();
        if (OutlineGroup != null) seq.Join(OutlineGroup.DOFade(targetAlpha, duration));
        if (ContentGroup != null) seq.Join(ContentGroup.DOFade(targetAlpha, duration));
        if (bgImage != null) seq.Join(bgImage.DOFade(targetAlpha, duration));
        return seq;
    }

    public Tween FadeOutline(float targetAlpha, float duration)
    {
        var g = OutlineGroup;
        return g != null ? g.DOFade(targetAlpha, duration) : null;
    }

    public Tween FadeContent(float targetAlpha, float duration)
    {
        var g = ContentGroup;
        return g != null ? g.DOFade(targetAlpha, duration) : null;
    }

    public Tween FadeBG(float targetAlpha, float duration)
    {
        return bgImage != null ? bgImage.DOFade(targetAlpha, duration) : null;
    }

    // For subpage squeeze: quickly dim content as t grows (without touching outline/bg alphas)
    public void SetContentAlphaEdgeFade(float normalized)
    {
        var cg = ContentGroup;
        if (cg == null) return;
        float t = Mathf.Clamp01(normalized);
        float edgeAlpha = 1f - Mathf.Clamp01(t * 5f);
        cg.alpha = edgeAlpha;
    }

    public Tween FadeRoot(float targetAlpha, float duration, Ease ease)
    {
        var g = RootGroup;
        return g != null ? g.DOFade(Mathf.Clamp01(targetAlpha), duration).SetEase(ease) : null;
    }

    // Sets the root CanvasGroup alpha immediately (no tween)
    public void SetRootAlphaImmediate(float a)
    {
        var g = RootGroup;
        if (g != null) g.alpha = Mathf.Clamp01(a);
    }
    
        // Approximate current container (outline/bg) height used for squeeze decisions
    public float GetContainerHeight()
    {
        float h = -1f;
        if (outlineRect != null) h = outlineRect.sizeDelta.y;
        if (bgRect != null)
        {
            float hb = bgRect.sizeDelta.y;
            if (h < 0f || hb > h) h = hb; // prefer the larger if both exist
        }
        if (h < 0f && rect != null) h = rect.sizeDelta.y; // fallback
        return h;
    }
}
