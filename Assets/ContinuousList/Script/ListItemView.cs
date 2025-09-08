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
        float a = Mathf.Lerp(MinOutlineAlpha, 1f, t);

        g.alpha = a;
    }

    public void SetContentAlphaBasedOnZ(float currentZ, float zMid, float zFront)
    {
        // Calculate the interpolation factor 't' based on the current Z position
        float t = 0f;
        if (zFront != zMid) // Avoid division by zero
        {
            t = Mathf.Clamp01((currentZ - zMid) / (zFront - zMid));
        }
        
        // Map [0,1] → [0.2,1] and store it
        _contentAlphaFromZ = Mathf.Lerp(0.1f, 1f, t);
    }

    public void SetEdgeSqueeze(float normalized, float itemHeight)
    {
        float t = Mathf.Clamp01(normalized);

        if (outlineRect != null)
        {
            var size = outlineRect.sizeDelta;
            size.y = Mathf.Lerp(itemHeight, 0f, t);
            outlineRect.sizeDelta = size;
        }

        if (outlineImage != null)
        {
            outlineImage.pixelsPerUnitMultiplier = Mathf.Lerp(1f, 2f, t);
        }

        if (bgRect != null)
        {
            var size = bgRect.sizeDelta;
            size.y = Mathf.Lerp(itemHeight, 0f, t);
            bgRect.sizeDelta = size;
        }

        if (bgImage != null)
        {
            bgImage.pixelsPerUnitMultiplier = Mathf.Lerp(5f, 10f, t);
        }

        var cg = ContentGroup;
        if (cg != null)
        {
            // Calculate alpha based on edge squeeze (fade out at top)
            float edgeAlpha = 1f - Mathf.Clamp01(t * 3f);
            // Final alpha is the product of Z-based alpha and edge-squeeze alpha
            cg.alpha = _contentAlphaFromZ * edgeAlpha;
        }
    }
}
