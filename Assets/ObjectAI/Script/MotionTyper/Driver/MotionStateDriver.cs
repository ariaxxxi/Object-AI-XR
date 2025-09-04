using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

[DisallowMultipleComponent]
public class MotionStateDriver : MonoBehaviour
{
    [Header("Controller")]
    public StageController controller;

    [Header("Targets (optional)")]
    [HideInInspector] public RectTransform rt;
    [HideInInspector] public CanvasGroup cg;

    [Header("Global Timing Overrides")]
    public bool overrideDuration;
    public float duration = 1f;
    public bool overrideEase;
    public Ease ease = Ease.InOutExpo;
    public float startDelay = 0f;

    [System.Serializable]
    public class EdgeTimingOverride
    {
        public bool enabled = true;
        [Tooltip("From stage index")]
        public int from;
        [Tooltip("To stage index")]
        public int to;

        [Header("Overrides")]
        public bool useDuration;
        public float duration = 1f;

        public bool useEase;
        public Ease ease = Ease.InOutExpo;

        public bool useDelay;
        public float delay = 0f;
    }

    [Header("Per-Edge Timing Overrides")]
    public List<EdgeTimingOverride> edgeTimingOverrides = new();

    [Header("Track Toggles (Essentials)")]
    public bool useAnchoredPosition;
    public bool useLocalPosition;
    public bool useEuler;
    public bool useUniformScale;
    public bool useSizeDelta;
    public bool useAlpha;

    [Header("Per-Stage Values")]
    public List<Vector2> anchoredPosPerStage = new();
    public List<Vector3> localPosPerStage = new();
    public List<Vector3> eulerPerStage = new();
    public List<float>   uniformScalePerStage = new();
    public List<Vector2> sizePerStage = new();
    public List<float>   alphaPerStage = new();

    [SerializeField] string tweenId;

    void Reset()
    {
        rt = GetComponent<RectTransform>();
        cg = GetComponent<CanvasGroup>();
    }

    void Awake()
    {
        if (string.IsNullOrEmpty(tweenId)) tweenId = "DRV_CORE_" + GetInstanceID();
        rt = GetComponent<RectTransform>();
        cg = GetComponent<CanvasGroup>();
    }

    void OnEnable()
    {
        if (controller != null)
        {
            controller.OnStageChanged += ApplyStageLegacy;
            controller.OnStageChangedDetailed += ApplyStageDetailed;
        }
        EnsureListSizes();
        if (controller && controller.CurrentIndex >= 0) ApplyInstant(controller.CurrentIndex);
    }

    void OnDisable()
    {
        if (controller != null)
        {
            controller.OnStageChanged -= ApplyStageLegacy;
            controller.OnStageChangedDetailed -= ApplyStageDetailed;
        }
        DOTween.Kill(tweenId, false);
    }

    void OnValidate()
    {
        if (string.IsNullOrEmpty(tweenId)) tweenId = "DRV_CORE_" + GetInstanceID();
        rt = GetComponent<RectTransform>();
        cg = GetComponent<CanvasGroup>();
        EnsureListSizes();
    }

    public void EnsureListSizes()
    {
        int count = controller && controller.stages != null ? controller.stages.Count : 0;
        if (count <= 0) return;

        void Fit<T>(List<T> list, T def)
        {
            if (list == null) return;
            while (list.Count < count) list.Add(def);
            if (list.Count > count) list.RemoveRange(count, list.Count - count);
        }

        Vector2 curAnch = rt ? rt.anchoredPosition : Vector2.zero;
        Vector3 curLoc  = rt ? rt.localPosition : Vector3.zero;
        Vector3 curRot  = rt ? rt.localEulerAngles : Vector3.zero;
        float curScale  = rt ? rt.localScale.x : 1f;
        Vector2 curSize = rt ? rt.sizeDelta : new Vector2(100, 100);
        float curA      = cg ? cg.alpha : 1f;

        if (useAnchoredPosition) Fit(anchoredPosPerStage, curAnch);
        if (useLocalPosition)    Fit(localPosPerStage,   curLoc);
        if (useEuler)            Fit(eulerPerStage,      curRot);
        if (useUniformScale)     Fit(uniformScalePerStage, curScale);
        if (useSizeDelta)        Fit(sizePerStage,       curSize);
        if (useAlpha)            Fit(alphaPerStage,      curA);
    }

    void ApplyStageLegacy(int toIndex, StageDef def, float globalDuration, Ease globalEase)
        => ApplyStageInternal(-1, toIndex, globalDuration, globalEase);

    void ApplyStageDetailed(int fromIndex, int toIndex, StageDef def, float globalDuration, Ease globalEase)
        => ApplyStageInternal(fromIndex, toIndex, globalDuration, globalEase);

    void ResolveTiming(int fromIndex, int toIndex, float globalDuration, Ease globalEase,
                       out float effDuration, out Ease effEase, out float effDelay)
    {
        effDuration = overrideDuration ? duration : globalDuration;
        effEase     = overrideEase     ? ease     : globalEase;
        effDelay    = startDelay;

        // Apply per-edge override if present
        var o = FindEdgeOverride(fromIndex, toIndex);
        if (o != null && o.enabled)
        {
            if (o.useDuration) effDuration = o.duration;
            if (o.useEase)     effEase     = o.ease;
            if (o.useDelay)    effDelay    = o.delay;
        }
    }

    EdgeTimingOverride FindEdgeOverride(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || edgeTimingOverrides == null) return null;
        for (int i = 0; i < edgeTimingOverrides.Count; i++)
        {
            var o = edgeTimingOverrides[i];
            if (o != null && o.enabled && o.from == fromIndex && o.to == toIndex)
                return o;
        }
        return null;
    }

    void ApplyStageInternal(int fromIndex, int toIndex, float globalDuration, Ease globalEase)
    {
        if (toIndex < 0) return;

        ResolveTiming(fromIndex, toIndex, globalDuration, globalEase,
                      out float effDuration, out Ease effEase, out float effDelay);

        DOTween.Kill(tweenId, false);

        Tween Wrap(Tween t)
        {
            if (t == null) return null;
            t.SetId(tweenId);
            if (effDelay > 0f) t.SetDelay(effDelay);
            return t;
        }

        if (useAnchoredPosition && rt && anchoredPosPerStage.Count > toIndex)
            Wrap(rt.DOAnchorPos(anchoredPosPerStage[toIndex], effDuration).SetEase(effEase));

        if (useLocalPosition && rt && localPosPerStage.Count > toIndex)
            Wrap(rt.DOLocalMove(localPosPerStage[toIndex], effDuration).SetEase(effEase));

        if (useEuler && rt && eulerPerStage.Count > toIndex)
            Wrap(rt.DOLocalRotate(eulerPerStage[toIndex], effDuration).SetEase(effEase));

        if (useSizeDelta && rt && sizePerStage.Count > toIndex)
            Wrap(rt.DOSizeDelta(sizePerStage[toIndex], effDuration).SetEase(effEase));

        if (useUniformScale && rt && uniformScalePerStage.Count > toIndex)
            Wrap(rt.DOScale(uniformScalePerStage[toIndex], effDuration).SetEase(effEase));

        if (useAlpha && cg && alphaPerStage.Count > toIndex)
            Wrap(cg.DOFade(alphaPerStage[toIndex], effDuration).SetEase(effEase));
    }

    public void ApplyInstant(int index)
    {
        if (index < 0) return;

        if (useAnchoredPosition && rt && anchoredPosPerStage.Count > index)
            rt.anchoredPosition = anchoredPosPerStage[index];
        if (useLocalPosition && rt && localPosPerStage.Count > index)
            rt.localPosition = localPosPerStage[index];

        if (useEuler && rt && eulerPerStage.Count > index)
            rt.localEulerAngles = eulerPerStage[index];

        if (useSizeDelta && rt && sizePerStage.Count > index)
            rt.sizeDelta = sizePerStage[index];

        if (useUniformScale && rt && uniformScalePerStage.Count > index)
            rt.localScale = Vector3.one * uniformScalePerStage[index];

        if (useAlpha && cg && alphaPerStage.Count > index)
            cg.alpha = alphaPerStage[index];
    }
}
