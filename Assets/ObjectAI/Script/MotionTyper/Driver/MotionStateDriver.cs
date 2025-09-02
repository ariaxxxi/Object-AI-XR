using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

[DisallowMultipleComponent]
public class MotionStateDriver : MonoBehaviour
{
    [Header("Controller")]
    public StageController controller;

    [Header("Targets (optional)")]
    public RectTransform rt;
    public CanvasGroup cg;

    [Header("Global Timing (fallback)")]
    public bool overrideDuration;
    public float duration = 1f;
    public bool overrideEase;
    public Ease ease = Ease.InOutExpo;
    public float startDelay = 0f;

    [Header("Track Toggles (ESSENTIALS)")]
    public bool useAnchoredPosition;
    public bool useLocalPosition;
    public bool useEuler;
    public bool useUniformScale;
    public bool useSizeDelta;
    public bool useAlpha;

    [Header("Per-Stage Values (auto-sized)")]
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
        if (!rt) rt = GetComponent<RectTransform>();
        if (!cg && useAlpha) cg = GetComponent<CanvasGroup>();
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
        if (!rt) rt = GetComponent<RectTransform>();
        if (!cg && useAlpha) cg = GetComponent<CanvasGroup>();
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
        => ApplyStageInternal(toIndex, globalDuration, globalEase);

    void ApplyStageDetailed(int fromIndex, int toIndex, StageDef def, float globalDuration, Ease globalEase)
        => ApplyStageInternal(toIndex, globalDuration, globalEase);

    void ApplyStageInternal(int toIndex, float globalDuration, Ease globalEase)
    {
        if (toIndex < 0) return;

        float effDuration = overrideDuration ? duration : globalDuration;
        Ease  effEase     = overrideEase     ? ease     : globalEase;

        DOTween.Kill(tweenId, false);

        Tween Wrap(Tween t)
        {
            if (t == null) return null;
            t.SetId(tweenId);
            if (startDelay > 0f) t.SetDelay(startDelay);
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
