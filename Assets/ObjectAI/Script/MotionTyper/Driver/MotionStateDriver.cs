using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

[DisallowMultipleComponent]
public class MotionStateDriver : MonoBehaviour
{
    [Header("Refs")]
    public StageController controller;       // auto-find if null
    public RectTransform rt;                 // auto-get if null
    public CanvasGroup cg;                   // needed if useAlpha (auto-add)
    public Graphic graphic;                  // for color (UI)
    public Renderer targetRenderer;          // for color (3D)

    [Header("Position Mode")]
    [Tooltip("OFF: localPosition (World Space). ON: anchoredPosition (Screen Space).")]
    public bool useAnchoredPosition = false;

    [Header("Track Toggles")]
    public bool useLocalPos = true;          // ignored if useAnchoredPosition==true
    public bool useAnchoredPos = false;      // used if useAnchoredPosition==true
    public bool useEuler = false;            // local rotation (Euler)
    public bool useUniformScale = false;     // uniform scale
    public bool useSizeDelta = false;        // UI width/height
    public bool useAlpha = false;            // CanvasGroup alpha
    public bool useColor = false;            // UI Graphic color or Renderer material color

    [Header("Per-Stage Tracks (auto-sized)")]
    public List<Vector3> localPosPerStage = new();
    public List<Vector2> anchoredPosPerStage = new();
    public List<Vector3> eulerPerStage = new();
    public List<float>   uniformScalePerStage = new();
    public List<Vector2> sizePerStage = new();
    public List<float>   alphaPerStage = new();
    public List<Color>   colorPerStage = new();

    [Header("Overrides (optional)")]
    public bool overrideDuration = false;
    public float duration = 1f;
    public bool overrideEase = false;
    public Ease ease = Ease.InOutExpo;

    void Reset()
    {
        rt = GetComponent<RectTransform>();
        cg = GetComponent<CanvasGroup>();
        graphic = GetComponent<Graphic>();
        targetRenderer = GetComponent<Renderer>();
        if (!cg && useAlpha) cg = gameObject.AddComponent<CanvasGroup>();
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
        controller.OnStageChanged += ApplyStage;
        EnsureListSizes();
        if (controller.CurrentIndex >= 0)
            ApplyStage(controller.CurrentIndex, controller.CurrentStage, controller.duration, controller.ease);
    }

    void OnDisable()
    {
        if (controller) controller.OnStageChanged -= ApplyStage;
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

    // --------- Ensure tracks match stage count ---------
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
        Vector2 curAnch = rt ? rt.anchoredPosition : Vector2.zero;
        Vector3 curEuler = rt ? rt.localEulerAngles : Vector3.zero;
        float curScale = rt ? rt.localScale.x : 1f;
        Vector2 curSize = rt ? rt.sizeDelta : new Vector2(100, 100);
        float curAlpha = cg ? cg.alpha : 1f;
        Color curColor = graphic ? graphic.color : (targetRenderer ? GetRendererColor(targetRenderer) : Color.white);

        if (useAnchoredPosition) Fit(anchoredPosPerStage, curAnch); else Fit(localPosPerStage, curLocal);
        if (useEuler)            Fit(eulerPerStage, curEuler);
        if (useUniformScale)     Fit(uniformScalePerStage, curScale);
        if (useSizeDelta)        Fit(sizePerStage, curSize);
        if (useAlpha)            Fit(alphaPerStage, curAlpha);
        if (useColor)            Fit(colorPerStage, curColor);
    }

    // --------- Apply tweens on stage change ---------
    public void ApplyStage(int index, StageDef def, float globalDuration, Ease globalEase)
    {
        if (!rt) return;

        float d = overrideDuration ? duration : globalDuration;
        Ease  e = overrideEase ? ease : globalEase;

        DOTween.Kill(rt);
        if (cg) DOTween.Kill(cg);
        if (graphic) DOTween.Kill(graphic);

        // Position
        if (useAnchoredPosition && useAnchoredPos && anchoredPosPerStage.Count > index)
            rt.DOAnchorPos(anchoredPosPerStage[index], d).SetEase(e);
        else if (!useAnchoredPosition && useLocalPos && localPosPerStage.Count > index)
            rt.DOLocalMove(localPosPerStage[index], d).SetEase(e);

        // Rotation
        if (useEuler && eulerPerStage.Count > index)
            rt.DOLocalRotate(eulerPerStage[index], d).SetEase(e);

        // Size
        if (useSizeDelta && sizePerStage.Count > index)
            rt.DOSizeDelta(sizePerStage[index], d).SetEase(e);

        // Scale (uniform)
        if (useUniformScale && uniformScalePerStage.Count > index)
            rt.DOScale(uniformScalePerStage[index], d).SetEase(e);

        // Alpha
        if (useAlpha && cg && alphaPerStage.Count > index)
            cg.DOFade(alphaPerStage[index], d).SetEase(e);

        // Color
        if (useColor && colorPerStage.Count > index)
        {
            var target = colorPerStage[index];

            if (graphic)
            {
                graphic.DOColor(target, d).SetEase(e);
            }
            else if (targetRenderer)
            {
                // Try _BaseColor first (URP), then _Color
                var mat = targetRenderer.material;
                if (mat.HasProperty("_BaseColor"))
                    DOTween.To(() => mat.GetColor("_BaseColor"), c => mat.SetColor("_BaseColor", c), target, d).SetEase(e);
                else if (mat.HasProperty("_Color"))
                    DOTween.To(() => mat.color, c => mat.color = c, target, d).SetEase(e);
            }
        }
    }

    // --------- (Optional) Instant apply for editor preview ---------
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

    // Utility to read renderer color
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
