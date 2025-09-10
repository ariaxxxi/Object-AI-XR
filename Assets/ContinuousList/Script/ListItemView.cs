using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ListItemView : MonoBehaviour
{
    // Auto-referenced; not exposed in Inspector
    RectTransform rect;
    Transform depthTarget;
    CanvasGroup outlineGroup;
    RectTransform outlineRect;
    Image outlineImage;
    CanvasGroup contentGroup;
    RectTransform bgRect;
    Image bgImage;

    [HideInInspector] public int index; // assigned by controller

    const float MinOutlineAlpha = 0.1f; // clamp range is [0.2, 1]
    float _contentAlphaFromZ = 1f; // Stores alpha based on Z-position
    float _edgeContainerAlphaFactor = 1f; // Multiplies container visuals (outline/bg) during top squeeze
    [Header("Smoothing")]
    [Tooltip("Higher values make outline alpha ease more slowly to target (smoother). Units are 1/seconds in an exponential ease.")]
    [Range(1f, 20f)] public float outlineAlphaSmoothing = 8f;
    float _outlineVisualAlpha = MinOutlineAlpha;

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
                }
            }
            return contentGroup;
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
        float centerOffset = 0.5f * (itemHeight - newH); // shift up to keep top anchored

        if (outlineRect != null)
        {
            var size = outlineRect.sizeDelta;
            size.y = newH;
            outlineRect.sizeDelta = size;
            var ap = outlineRect.anchoredPosition;
            ap.y = centerOffset;
            outlineRect.anchoredPosition = ap;
        }

        if (outlineImage != null)
        {
            outlineImage.pixelsPerUnitMultiplier = Mathf.Lerp(1f, 2f, t);
        }

        if (bgRect != null)
        {
            var size = bgRect.sizeDelta;
            size.y = newH;
            bgRect.sizeDelta = size;
            var ap = bgRect.anchoredPosition;
            ap.y = centerOffset;
            bgRect.anchoredPosition = ap;
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

        // Apply fast container fade after halfway squeeze
        if (t <= 0.5f)
        {
            _edgeContainerAlphaFactor = 1f;
        }
        else
        {
            float u = Mathf.Clamp01((t - 0.5f) / 0.3f); // quick fade over last 15%
            _edgeContainerAlphaFactor = 1f - u;
        }

        // Optionally dim BG image directly
        if (bgImage != null)
        {
            var c = bgImage.color;
            c.a = _edgeContainerAlphaFactor;
            bgImage.color = c;
        }
    }
}
