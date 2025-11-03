using UnityEngine;
using UnityEngine.UI;
using TMPro; // Add TextMeshPro namespace
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
    CanvasGroup rootCanvasGroup;
    // RectTransform gradientRect;
    // Image gradientImage;
 
    // Cached components for content control
    private RectTransform titleRect;
    private TextMeshProUGUI titleTextMeshPro;
    private RectTransform subtitleRect;
    private TextMeshProUGUI subtitleTextMeshPro;
    private RectTransform timeRect;
    private TextMeshProUGUI timeTextMeshPro;
    private RectTransform thumbnailRect;
    private Image thumbnailImageComponent;
    [HideInInspector] public int index; // assigned by controller
    [Header("Child Content Control")]
    [Tooltip("Text for the 'title' child object's TextMeshPro component")]
    [SerializeField] private string titleText = "Default Title";
    [Tooltip("Text for the 'subtitle' child object's TextMeshPro component")]
    [SerializeField] private string subtitleText = "Default Subtitle";
    [Tooltip("Text for the 'time' child object's TextMeshPro component")]
    [SerializeField] private string timeText = "12m";
    [Tooltip("Source Image for the 'Thumbnail' child object's Image component")]
    [SerializeField] private Sprite thumbnailImage;
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
                }
                else
                {
                    // Debug.LogWarning($"Could not find BG object for {gameObject.name}", this);
                }
            }
            return bgRect;
        }
    }
    // Properties for content components
    RectTransform TimeRect
    {
        get
        {
            if (timeRect == null)
            {
                Transform t = transform.Find("time");
                if (t == null)
                {
                    var all = GetComponentsInChildren<Transform>(true);
                    foreach (var tr in all)
                    {
                        if (tr != null && tr.name == "time") { t = tr; break; }
                    }
                }
                if (t != null)
                {
                    timeRect = t.GetComponent<RectTransform>();
                    timeTextMeshPro = t.GetComponent<TextMeshProUGUI>();
                }
            }
            return timeRect;
        }
    }
    RectTransform TitleRect
    {
        get
        {
            if (titleRect == null)
            {
                Transform t = transform.Find("title");
                if (t == null)
                {
                    var all = GetComponentsInChildren<Transform>(true);
                    foreach (var tr in all)
                    {
                        if (tr != null && tr.name == "title") { t = tr; break; }
                    }
                }
                if (t != null)
                {
                    titleRect = t.GetComponent<RectTransform>();
                    titleTextMeshPro = t.GetComponent<TextMeshProUGUI>();
                }
            }
            return titleRect;
        }
    }
    RectTransform SubtitleRect
    {
        get
        {
            if (subtitleRect == null)
            {
                Transform t = transform.Find("subtitle");
                if (t == null)
                {
                    var all = GetComponentsInChildren<Transform>(true);
                    foreach (var tr in all)
                    {
                        if (tr != null && tr.name == "subtitle") { t = tr; break; }
                    }
                }
                if (t != null)
                {
                    subtitleRect = t.GetComponent<RectTransform>();
                    subtitleTextMeshPro = t.GetComponent<TextMeshProUGUI>();
                }
            }
            return subtitleRect;
        }
    }
    RectTransform ThumbnailRect
    {
        get
        {
            if (thumbnailRect == null)
            {
                Transform t = transform.Find("Thumbnail");
                if (t == null)
                {
                    var all = GetComponentsInChildren<Transform>(true);
                    foreach (var tr in all)
                    {
                        if (tr != null && tr.name == "Thumbnail") { t = tr; break; }
                    }
                }
                if (t != null)
                {
                    thumbnailRect = t.GetComponent<RectTransform>();
                    thumbnailImageComponent = t.GetComponent<Image>();
                }
            }
            return thumbnailRect;
        }
    }
    void Awake()
    {
        // Ensure auto references are set
        if (rect == null) rect = GetComponent<RectTransform>();
        if (depthTarget == null) depthTarget = transform;
        if (rootCanvasGroup == null) rootCanvasGroup = GetComponent<CanvasGroup>();
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
        // Resolve content components and apply values
        var ____ = TimeRect;
        var _____ = TitleRect;
        var ______ = SubtitleRect;
        var _______ = ThumbnailRect;
        ApplyContentValues();
    }
    /// <summary>
    /// Applies the inspector-defined values to the child UI components.
    /// </summary>
    public void ApplyContentValues()
    {
        if (timeTextMeshPro != null)
        {
            timeTextMeshPro.text = timeText;
        }
        if (titleTextMeshPro != null)
        {
            titleTextMeshPro.text = titleText;
        }
        if (subtitleTextMeshPro != null)
        {
            subtitleTextMeshPro.text = subtitleText;
        }
        if (thumbnailImageComponent != null)
        {
            thumbnailImageComponent.sprite = thumbnailImage;
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
    public void SetRootAlphaBasedOnZ(float currentZ, float zBack)
    {
        if (rootCanvasGroup == null) return;
        if (currentZ == zBack)
        {
            // Item is behind the zBack position, make the root object invisible
            rootCanvasGroup.alpha = 0f;
        }
        else
        {
            // Item is at or in front of zBack, make the root object fully visible
            rootCanvasGroup.alpha = 1f;
        }
    }
    public void SetEdgeSqueeze(float normalized, float baseZ)
    {
        float t = Mathf.Clamp01(normalized);
        var dt = DepthTarget;
        if (dt != null)
        {
            var pos = dt.localPosition;
            pos.z = Mathf.Lerp(baseZ, 20f, t);
            dt.localPosition = pos;
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
        var cg = ContentGroup;
        if (cg != null)
        {
            cg.alpha = _contentAlphaFromZ * _edgeContainerAlphaFactor;
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
