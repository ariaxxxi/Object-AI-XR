using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

[DisallowMultipleComponent]
public class MotionStateDriver : MonoBehaviour
{
    [Header("References")]
    public StageController controller;            // Auto-found if null
    public RectTransform rt;                      // Auto-filled from this.transform if null
    public CanvasGroup cg;                        // Required only if useAlpha; auto-added if missing

    [Header("Position Mode")]
    [Tooltip("If ON, use RectTransform.anchoredPosition; if OFF, use localPosition (World Space friendly).")]
    public bool useAnchoredPosition = false;

    [Header("Property Toggles")]
    public bool useLocalPos   = true;             // ignored if useAnchoredPosition=true
    public bool useAnchoredPos = false;           // used if useAnchoredPosition=true
    public bool useSize       = false;            // for outlines/pills (sizeDelta)
    public bool useScale      = false;            // uniform scale
    public bool useAlpha      = false;            // CanvasGroup alpha

    [Header("Per-Stage Values (match StageController.stages count)")]
    public List<Vector3> localPosPerStage   = new List<Vector3>(); // when useAnchoredPosition=false
    public List<Vector2> anchoredPosPerStage= new List<Vector2>(); // when useAnchoredPosition=true
    public List<Vector2> sizePerStage       = new List<Vector2>(); // when useSize
    public List<float>   scalePerStage      = new List<float>();   // when useScale
    public List<float>   alphaPerStage      = new List<float>();   // when useAlpha

    void Reset()
    {
        rt = GetComponent<RectTransform>();
        cg = GetComponent<CanvasGroup>();
        if (!cg && useAlpha) cg = gameObject.AddComponent<CanvasGroup>();
    }

    void Awake()
    {
        if (!rt) rt = GetComponent<RectTransform>();
        if (!controller) controller = FindObjectOfType<StageController>();
        if (useAlpha && !cg) cg = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
    }

    void OnEnable()
    {
        if (!controller)
        {
            Debug.LogWarning($"[UIStateDriver] No StageController found for {name}.");
            return;
        }
        controller.OnStageChanged += ApplyStage;
        EnsureListSizes();
        // Apply current on enable for visual consistency
        if (controller.CurrentIndex >= 0) ApplyStage(controller.CurrentIndex, controller.CurrentStage, controller.duration, controller.ease);
    }

    void OnDisable()
    {
        if (controller) controller.OnStageChanged -= ApplyStage;
    }

    void OnValidate()
    {
        // Keep lists in sync in-editor when you toggle flags or assign controller
        if (!rt) rt = GetComponent<RectTransform>();
        if (!controller) controller = FindObjectOfType<StageController>();
        if (useAlpha && !cg) cg = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        EnsureListSizes();
    }

    void EnsureListSizes()
    {
        int count = controller && controller.stages != null ? controller.stages.Count : 0;
        if (count <= 0) return;

        // Helpers
        void EnsureVec3(List<Vector3> list, Vector3 def)
        {
            if (list == null) return;
            while (list.Count < count) list.Add(def);
            if (list.Count > count) list.RemoveRange(count, list.Count - count);
        }
        void EnsureVec2(List<Vector2> list, Vector2 def)
        {
            if (list == null) return;
            while (list.Count < count) list.Add(def);
            if (list.Count > count) list.RemoveRange(count, list.Count - count);
        }
        void EnsureF(List<float> list, float def)
        {
            if (list == null) return;
            while (list.Count < count) list.Add(def);
            if (list.Count > count) list.RemoveRange(count, list.Count - count);
        }

        // Reasonable defaults from current state
        Vector3 curLocal = rt ? rt.localPosition : Vector3.zero;
        Vector2 curAnch  = rt ? rt.anchoredPosition : Vector2.zero;
        Vector2 curSize  = rt ? rt.sizeDelta : Vector2.one * 100f;
        float curScale   = rt ? rt.localScale.x : 1f;
        float curAlpha   = cg ? cg.alpha : 1f;

        if (useAnchoredPosition) {
            EnsureVec2(anchoredPosPerStage, curAnch);
        } else {
            EnsureVec3(localPosPerStage, curLocal);
        }
        if (useSize)  EnsureVec2(sizePerStage,  curSize);
        if (useScale) EnsureF  (scalePerStage, curScale);
        if (useAlpha) EnsureF  (alphaPerStage, curAlpha);
    }

    void ApplyStage(int index, StageDef def, float duration, Ease ease)
    {
        if (!rt) return;

        // Kill any previous tweens on these targets
        DOTween.Kill(rt);
        if (cg) DOTween.Kill(cg);

        // Position
        if (useAnchoredPosition && useAnchoredPos && anchoredPosPerStage.Count > index)
        {
            rt.DOAnchorPos(anchoredPosPerStage[index], duration).SetEase(ease);
        }
        else if (!useAnchoredPosition && useLocalPos && localPosPerStage.Count > index)
        {
            rt.DOLocalMove(localPosPerStage[index], duration).SetEase(ease);
        }

        // SizeDelta
        if (useSize && sizePerStage.Count > index)
        {
            rt.DOSizeDelta(sizePerStage[index], duration).SetEase(ease);
        }

        // Uniform Scale
        if (useScale && scalePerStage.Count > index)
        {
            rt.DOScale(scalePerStage[index], duration).SetEase(ease);
        }

        // Alpha
        if (useAlpha && cg && alphaPerStage.Count > index)
        {
            cg.DOFade(alphaPerStage[index], duration).SetEase(ease);
        }
    }
}
