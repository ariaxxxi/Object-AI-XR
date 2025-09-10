using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

[DisallowMultipleComponent]
public class MotionStateDriver : MonoBehaviour
{
    [Header("Controller")]
    public StageController controller;

    [Header("Targets")]
    [HideInInspector] public RectTransform rt;
    [HideInInspector] public CanvasGroup cg;
    [HideInInspector] public Transform tf;

    [Header("Motion Track Toggles")]
    public bool useLocalPosition;
    public bool useEuler;
    public bool useUniformScale;
    public bool useSizeDelta;
    public bool useAlpha;
    public bool useTransform3DPosition;
    public bool useTransform3DRotation;
    public bool useTransform3DScale;

    [Header("Per-Stage Values")]
    public List<Vector3> localPosPerStage = new();
    public List<Vector3> eulerPerStage = new();
    public List<Vector3> scalePerStage = new();
    public List<Vector2> sizePerStage = new();
    public List<float>   alphaPerStage = new();
    public List<Vector3> transform3DPositionPerStage = new();
    public List<Vector3> transform3DRotationPerStage = new();
    public List<Vector3> transform3DScalePerStage = new();


    [Header("Global Timing Overrides")]
    public bool showGlobalTimingOverrides;
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



    [SerializeField] string tweenId;

    [Header("Dynamic Tracks (optional)")]
    [Tooltip("Additional user-defined tracks driven by stages. Uses the same timing and easing as above.")]
    [SerializeReference] public List<TrackBase> dynamicTracks = new();

    void Reset()
    {
        rt = GetComponent<RectTransform>();
        cg = GetComponent<CanvasGroup>();
        tf = GetComponent<Transform>();
    }

    void Awake()
    {
        if (string.IsNullOrEmpty(tweenId)) tweenId = "DRV_CORE_" + GetInstanceID();
        rt = GetComponent<RectTransform>();
        cg = GetComponent<CanvasGroup>();
        tf = GetComponent<Transform>();
        // Prepare dynamic track accessors at runtime
        if (dynamicTracks != null)
        {
            foreach (var t in dynamicTracks) t?.BuildAccessors();
        }
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
        tf = GetComponent<Transform>();
        // Keep dynamic track accessors up to date in editor
        if (dynamicTracks != null)
        {
            foreach (var t in dynamicTracks) t?.BuildAccessors();
        }
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

        Vector3 curLoc  = rt ? rt.localPosition : Vector3.zero;
        Vector3 curRot  = rt ? rt.localEulerAngles : Vector3.zero;
        Vector3 curScale = rt ? rt.localScale : Vector3.one;
        Vector2 curSize = rt ? rt.sizeDelta : new Vector2(100, 100);
        float curA      = cg ? cg.alpha : 1f;
        Vector3 cur3DPos = tf ? tf.localPosition : Vector3.zero;
        Vector3 cur3DRot = tf ? tf.localEulerAngles : Vector3.zero;
        Vector3 cur3DScale = tf ? tf.localScale : Vector3.one;

        if (useLocalPosition)    Fit(localPosPerStage,   curLoc);
        if (useEuler)            Fit(eulerPerStage,      curRot);
        if (useUniformScale)     Fit(scalePerStage,      curScale);
        if (useSizeDelta)        Fit(sizePerStage,       curSize);
        if (useAlpha)            Fit(alphaPerStage,      curA);
        if (useTransform3DPosition) Fit(transform3DPositionPerStage, cur3DPos);
        if (useTransform3DRotation) Fit(transform3DRotationPerStage, cur3DRot);
        if (useTransform3DScale)    Fit(transform3DScalePerStage,    cur3DScale);

        // Ensure dynamic tracks have values per stage
        if (dynamicTracks != null)
        {
            foreach (var t in dynamicTracks) t?.EnsureSize(count);
        }
    }

    void ApplyStageLegacy(int toIndex, StageDef def, float globalDuration, Ease globalEase)
        => ApplyStageInternal(-1, toIndex, def, globalDuration, globalEase);

    void ApplyStageDetailed(int fromIndex, int toIndex, StageDef def, float globalDuration, Ease globalEase)
        => ApplyStageInternal(fromIndex, toIndex, def, globalDuration, globalEase);

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

    void ApplyStageInternal(int fromIndex, int toIndex, StageDef def, float globalDuration, Ease globalEase)
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

        // Optional AE-style custom curve
        AnimationCurve customCurve = BuildAEEaseCurve(def);

        // Helper to apply either custom curve or Ease
        Tween ApplyEase(Tween tw, Ease e, AnimationCurve curve)
            => (curve != null) ? tw.SetEase(curve) : tw.SetEase(e);

        // Check for InOutBack ease to apply overshoot (ignored when custom curve is used)
        if (customCurve == null && effEase == Ease.InOutBack && controller != null)
        {
            float overshoot = controller.overshoot;
            if (useLocalPosition && rt && localPosPerStage.Count > toIndex)
                Wrap(rt.DOLocalMove(localPosPerStage[toIndex], effDuration).SetEase(effEase, overshoot));

            if (useEuler && rt && eulerPerStage.Count > toIndex)
                Wrap(rt.DOLocalRotate(eulerPerStage[toIndex], effDuration).SetEase(effEase, overshoot));

            if (useSizeDelta && rt && sizePerStage.Count > toIndex)
                Wrap(rt.DOSizeDelta(sizePerStage[toIndex], effDuration).SetEase(effEase, overshoot));

            if (useUniformScale && rt && scalePerStage.Count > toIndex)
                Wrap(rt.DOScale(scalePerStage[toIndex], effDuration).SetEase(effEase, overshoot));

            if (useAlpha && cg && alphaPerStage.Count > toIndex)
                Wrap(cg.DOFade(alphaPerStage[toIndex], effDuration).SetEase(effEase, overshoot));

            if (useTransform3DPosition && tf && transform3DPositionPerStage.Count > toIndex)
                Wrap(tf.DOLocalMove(transform3DPositionPerStage[toIndex], effDuration).SetEase(effEase, overshoot));
            if (useTransform3DRotation && tf && transform3DRotationPerStage.Count > toIndex)
                Wrap(tf.DOLocalRotate(transform3DRotationPerStage[toIndex], effDuration).SetEase(effEase, overshoot));
            if (useTransform3DScale && tf && transform3DScalePerStage.Count > toIndex)
                Wrap(tf.DOScale(transform3DScalePerStage[toIndex], effDuration).SetEase(effEase, overshoot));

            // Apply dynamic tracks with overshoot
            if (dynamicTracks != null)
            {
                foreach (var t in dynamicTracks)
                {
                    if (t == null || !t.enabled) continue;
                    t.ApplyTween(toIndex, effDuration, effEase, tweenId, effDelay, true, overshoot, customCurve);
                }
            }
        }
        else
        {
            if (useLocalPosition && rt && localPosPerStage.Count > toIndex)
                Wrap(ApplyEase(rt.DOLocalMove(localPosPerStage[toIndex], effDuration), effEase, customCurve));

            if (useEuler && rt && eulerPerStage.Count > toIndex)
                Wrap(ApplyEase(rt.DOLocalRotate(eulerPerStage[toIndex], effDuration), effEase, customCurve));

            if (useSizeDelta && rt && sizePerStage.Count > toIndex)
                Wrap(ApplyEase(rt.DOSizeDelta(sizePerStage[toIndex], effDuration), effEase, customCurve));

            if (useUniformScale && rt && scalePerStage.Count > toIndex)
                Wrap(ApplyEase(rt.DOScale(scalePerStage[toIndex], effDuration), effEase, customCurve));

            if (useAlpha && cg && alphaPerStage.Count > toIndex)
                Wrap(ApplyEase(cg.DOFade(alphaPerStage[toIndex], effDuration), effEase, customCurve));

            if (useTransform3DPosition && tf && transform3DPositionPerStage.Count > toIndex)
                Wrap(ApplyEase(tf.DOLocalMove(transform3DPositionPerStage[toIndex], effDuration), effEase, customCurve));
            if (useTransform3DRotation && tf && transform3DRotationPerStage.Count > toIndex)
                Wrap(ApplyEase(tf.DOLocalRotate(transform3DRotationPerStage[toIndex], effDuration), effEase, customCurve));
            if (useTransform3DScale && tf && transform3DScalePerStage.Count > toIndex)
                Wrap(ApplyEase(tf.DOScale(transform3DScalePerStage[toIndex], effDuration), effEase, customCurve));

            // Apply dynamic tracks without overshoot
            if (dynamicTracks != null)
            {
                foreach (var t in dynamicTracks)
                {
                    if (t == null || !t.enabled) continue;
                    t.ApplyTween(toIndex, effDuration, effEase, tweenId, effDelay, false, 0f, customCurve);
                }
            }
        }
    }

    public void ApplyInstant(int index)
    {
        if (index < 0) return;

        if (useLocalPosition && rt && localPosPerStage.Count > index)
            rt.localPosition = localPosPerStage[index];

        if (useEuler && rt && eulerPerStage.Count > index)
            rt.localEulerAngles = eulerPerStage[index];

        if (useSizeDelta && rt && sizePerStage.Count > index)
            rt.sizeDelta = sizePerStage[index];

        if (useUniformScale && rt && scalePerStage.Count > index)
            rt.localScale = scalePerStage[index];

        if (useAlpha && cg && alphaPerStage.Count > index)
            cg.alpha = alphaPerStage[index];

        if (useTransform3DPosition && tf && transform3DPositionPerStage.Count > index)
            tf.localPosition = transform3DPositionPerStage[index];
        if (useTransform3DRotation && tf && transform3DRotationPerStage.Count > index)
            tf.localEulerAngles = transform3DRotationPerStage[index];
        if (useTransform3DScale && tf && transform3DScalePerStage.Count > index)
            tf.localScale = transform3DScalePerStage[index];

        // Apply dynamic tracks instantly
        if (dynamicTracks != null)
        {
            foreach (var t in dynamicTracks)
            {
                if (t == null || !t.enabled) continue;
                t.ApplyInstant(index);
            }
        }
    }

    static AnimationCurve BuildAEEaseCurve(StageDef def)
    {
        if (def == null || !def.useAEEase) return null;
        float tScale = Mathf.Max(0.01f, def.aeTangentScale);
        float outTan0 = def.aeStartSpeed * tScale;
        float inTan1  = def.aeEndSpeed   * tScale;

        var k0 = new Keyframe(0f, 0f, 0f, outTan0);
        var k1 = new Keyframe(1f, 1f, inTan1, 0f);
#if UNITY_2018_1_OR_NEWER
        k0.weightedMode = WeightedMode.Both;
        k1.weightedMode = WeightedMode.Both;
        k0.outWeight = Mathf.Clamp01(def.aeStartInfluence / 100f);
        k1.inWeight  = Mathf.Clamp01(def.aeEndInfluence   / 100f);
#endif
        return new AnimationCurve(k0, k1);
    }
}
