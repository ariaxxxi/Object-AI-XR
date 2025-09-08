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

    [HideInInspector] public int index; // assigned by controller

    const float MinOutlineAlpha = 0.2f; // clamp range is [0.2, 1]

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
            var _ = ContentGroup;
        }
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

        var cg = ContentGroup;
        if (cg != null)
        {
            cg.alpha = 1f - Mathf.Clamp01(t * 3f);
        }
    }
}
