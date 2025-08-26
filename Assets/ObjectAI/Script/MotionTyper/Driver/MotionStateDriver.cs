using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

[System.Serializable]
public class TransitionOverride
{
    [Header("Match")]
    [Tooltip("From stage index (0..N-1). Use -1 to match any source.")]
    public int fromIndex = -1;
    [Tooltip("To stage index (0..N-1). Use -1 to match any destination.")]
    public int toIndex = -1;

    [Header("Overrides")]
    public bool overrideDuration = false;
    public float duration = 1f;

    public bool overrideEase = false;
    public Ease ease = Ease.InOutExpo;

    public bool overrideDelay = false;
    public float delay = 0f;
}

[DisallowMultipleComponent]
public class MotionStateDriver : MonoBehaviour
{
    [Header("Refs")]
    public StageController controller;
    public RectTransform rt;
    public CanvasGroup cg;
    public Graphic graphic;
    public Renderer targetRenderer;

    [Header("Position Mode")]
    public bool useAnchoredPosition = false;

    [Header("Track Toggles")]
    public bool useLocalPos = true;
    public bool useAnchoredPos = false;
    public bool useEuler = false;
    public bool useUniformScale = false;
    public bool useSizeDelta = false;
    public bool useAlpha = false;
    public bool useColor = false;

    [Header("Per-Stage Tracks (auto-sized)")]
    public List<Vector3> localPosPerStage = new();
    public List<Vector2> anchoredPosPerStage = new();
    public List<Vector3> eulerPerStage = new();
    public List<float>   uniformScalePerStage = new();
    public List<Vector2> sizePerStage = new();
    public List<float>   alphaPerStage = new();
    public List<Color>   colorPerStage = new();

    [Header("Per-Driver Timing Overrides (fallback)")]
    public bool overrideDuration = false;
    public float duration = 1f;
    public bool overrideEase = false;
    public Ease ease = Ease.InOutExpo;

    [Header("Stagger / Delay (fallback)")]
    public float startDelay = 0f;

    [Header("Looping")]
    public bool enableLoop = false;
    public int loopCount = -1;
    public LoopType loopType = LoopType.Yoyo;

    [Header("Per-Transition Overrides (highest priority)")]
    public List<TransitionOverride> transitionOverrides = new();

    void Reset()
    {
        rt = GetComponent<RectTransform>();
        cg = GetComponent<CanvasGroup>();
        if (!cg && useAlpha) cg = gameObject.AddComponent<CanvasGroup>();
        graphic = GetComponent<Graphic>();
        targetRenderer = GetComponent<Renderer>();
    }

    void Awake()
    {
        if (!rt) rt = GetComponent<RectTransform>();
        if (!controller) controller = FindObjectOfType<StageController>();
        if (useAlpha && !cg) cg = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        if (!graphic) graphic = GetComponent<Graphic>();
        if (!targetRenderer) targetRenderer = GetComponent<Renderer>();
    }

    void OnEnable()
    {
        if (!controller)
        {
            Debug.LogWarning($"[MotionStateDriver] No StageController for {name}");
            return;
        }

        // Prefer detailed event if available
        controller.OnStageChangedDetailed += ApplyStageDetailed;
        controller.OnStageChanged += ApplyStageLegacy;

        EnsureListSizes();

        if (controller.CurrentIndex >= 0)
        {
            // Apply instantly to current stage on enable
            ApplyInstant(controller.CurrentIndex);
        }
    }

    void OnDisable()
    {
        if (controller != null)
        {
            controller.OnStageChangedDetailed -= ApplyStageDetailed;
            controller.OnStageChanged -= ApplyStageLegacy;
        }
    }

    void OnValidate()
    {
        if (!rt) rt = GetComponent<RectTransform>();
        if (!controller) controller = FindObjectOfType<StageController>();
        if (useAlpha && !cg) cg = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        if (!graphic) graphic = GetComponent<Graphic>();
        if (!targetRenderer) targetRenderer = GetComponent<Renderer>();
        EnsureListSizes();
    }

    // ---------------- Sizes / defaults ----------------
    void EnsureListSizes()
    {
        int count = controller && controller.stages != null ? controller.stages.Count : 0;
        if (count <= 0) return;

        void Fit<T>(List<T> list, T def)
        {
            if (list == null) return;
            while (list.Count < count) list.Add(def);
            if (list.Count > count) list.RemoveRange(count, list.Count - count);
        }

        Vector3 curLocal = rt ? rt.localPosition : Vector3.zero;
        Vector2 curAnch  = rt ? rt.anchoredPosition : Vector2.zero;
        Vector3 curEuler = rt ? rt.localEulerAngles : Vector3.zero;
        float curScale   = rt ? rt.localScale.x : 1f;
        Vector2 curSize  = rt ? rt.sizeDelta : new Vector2(100, 100);
        float curAlpha   = cg ? cg.alpha : 1f;
        Color curColor   = graphic ? graphic.color : (targetRenderer ? GetRendererColor(targetRenderer) : Color.white);

        if (useAnchoredPosition) Fit(anchoredPosPerStage, curAnch); else Fit(localPosPerStage, curLocal);
        if (useEuler)            Fit(eulerPerStage, curEuler);
        if (useUniformScale)     Fit(uniformScalePerStage, curScale);
        if (useSizeDelta)        Fit(sizePerStage, curSize);
        if (useAlpha)            Fit(alphaPerStage, curAlpha);
        if (useColor)            Fit(colorPerStage, curColor);
    }

    // ---------------- Event adapters ----------------
    // Old event (no 'from' info) – we treat it as "from = current (unknown)" so only wildcard overrides would match.
    void ApplyStageLegacy(int toIndex, StageDef toDef, float globalDuration, Ease globalEase)
    {
        ApplyStageInternal(fromIndex: -999, toIndex, toDef, globalDuration, globalEase);
    }

    // New detailed event
    void ApplyStageDetailed(int fromIndex, int toIndex, StageDef toDef, float globalDuration, Ease globalEase)
    {
        ApplyStageInternal(fromIndex, toIndex, toDef, globalDuration, globalEase);
    }

    // ---------------- Core apply with per-transition overrides ----------------
    void ApplyStageInternal(int fromIndex, int toIndex, StageDef toDef, float globalDuration, Ease globalEase)
    {
        if (!rt) return;

        // 1) Compute effective timing from highest to lowest priority:
        //    (A) First matching TransitionOverride  → (B) per-driver override → (C) controller default
        float effDuration = overrideDuration ? duration : globalDuration;
        Ease  effEase     = overrideEase     ? ease     : globalEase;
        float effDelay    = startDelay;

        var ov = FindFirstMatchingOverride(fromIndex, toIndex);
        if (ov != null)
        {
            if (ov.overrideDuration) effDuration = ov.duration;
            if (ov.overrideEase)     effEase     = ov.ease;
            if (ov.overrideDelay)    effDelay    = ov.delay;
        }

        // 2) Kill existing tweens on my targets
        DOTween.Kill(rt);
        if (cg) DOTween.Kill(cg);
        if (graphic) DOTween.Kill(graphic);

        // Helper applies delay/looping consistently
        Tween WithTiming(Tween t)
        {
            if (t == null) return null;
            if (effDelay > 0f) t.SetDelay(effDelay);
            if (enableLoop) t.SetLoops(loopCount, loopType);
            return t;
        }

        // 3) Start tweens to the "toIndex" values
        if (useAnchoredPosition && useAnchoredPos && anchoredPosPerStage.Count > toIndex)
            WithTiming(rt.DOAnchorPos(anchoredPosPerStage[toIndex], effDuration).SetEase(effEase));
        else if (!useAnchoredPosition && useLocalPos && localPosPerStage.Count > toIndex)
            WithTiming(rt.DOLocalMove(localPosPerStage[toIndex], effDuration).SetEase(effEase));

        if (useEuler && eulerPerStage.Count > toIndex)
            WithTiming(rt.DOLocalRotate(eulerPerStage[toIndex], effDuration).SetEase(effEase));

        if (useSizeDelta && sizePerStage.Count > toIndex)
            WithTiming(rt.DOSizeDelta(sizePerStage[toIndex], effDuration).SetEase(effEase));

        if (useUniformScale && uniformScalePerStage.Count > toIndex)
            WithTiming(rt.DOScale(uniformScalePerStage[toIndex], effDuration).SetEase(effEase));

        if (useAlpha && cg && alphaPerStage.Count > toIndex)
            WithTiming(cg.DOFade(alphaPerStage[toIndex], effDuration).SetEase(effEase));

        if (useColor && colorPerStage.Count > toIndex)
        {
            var target = colorPerStage[toIndex];
            if (graphic)
                WithTiming(graphic.DOColor(target, effDuration).SetEase(effEase));
            else if (targetRenderer)
            {
                var mat = targetRenderer.material;
                if (mat.HasProperty("_BaseColor"))
                    WithTiming(DOTween.To(() => mat.GetColor("_BaseColor"), c => mat.SetColor("_BaseColor", c), target, effDuration).SetEase(effEase));
                else if (mat.HasProperty("_Color"))
                    WithTiming(DOTween.To(() => mat.color, c => mat.color = c, target, effDuration).SetEase(effEase));
            }
        }
    }

    TransitionOverride FindFirstMatchingOverride(int fromIndex, int toIndex)
    {
        if (transitionOverrides == null || transitionOverrides.Count == 0) return null;

        // Priority order:
        // 1) Exact from & exact to
        // 2) Exact from & any to (-1)
        // 3) Any from (-1) & exact to
        // 4) Any from (-1) & any to (-1)
        TransitionOverride best = null; int bestScore = -1;

        foreach (var ov in transitionOverrides)
        {
            bool fromMatch = (ov.fromIndex == -1) || (ov.fromIndex == fromIndex);
            bool toMatch   = (ov.toIndex   == -1) || (ov.toIndex   == toIndex);
            if (!fromMatch || !toMatch) continue;

            int score = 0;
            if (ov.fromIndex != -1) score += 2;
            if (ov.toIndex   != -1) score += 1;

            if (score > bestScore)
            {
                best = ov;
                bestScore = score;
                if (bestScore == 3) break; // exact/ exact found
            }
        }
        return best;
    }

    public void ApplyInstant(int index)
    {
        if (!rt) return;

        if (useAnchoredPosition && useAnchoredPos && anchoredPosPerStage.Count > index)
            rt.anchoredPosition = anchoredPosPerStage[index];
        else if (!useAnchoredPosition && useLocalPos && localPosPerStage.Count > index)
            rt.localPosition = localPosPerStage[index];

        if (useEuler && eulerPerStage.Count > index)
            rt.localEulerAngles = eulerPerStage[index];

        if (useSizeDelta && sizePerStage.Count > index)
            rt.sizeDelta = sizePerStage[index];

        if (useUniformScale && uniformScalePerStage.Count > index)
            rt.localScale = Vector3.one * uniformScalePerStage[index];

        if (useAlpha && cg && alphaPerStage.Count > index)
            cg.alpha = alphaPerStage[index];

        if (useColor && colorPerStage.Count > index)
        {
            var c = colorPerStage[index];
            if (graphic) graphic.color = c;
            else if (targetRenderer)
            {
                var mat = targetRenderer.material;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                else if (mat.HasProperty("_Color")) mat.color = c;
            }
        }
    }

    static Color GetRendererColor(Renderer r)
    {
        if (!r) return Color.white;
        var m = r.sharedMaterial;
        if (!m) return Color.white;
        if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor");
        if (m.HasProperty("_Color"))     return m.color;
        return Color.white;
    }
}
