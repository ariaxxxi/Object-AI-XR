using UnityEngine;

public class GazeHighlight : MonoBehaviour
{
    [Header("References")]
    public Camera cam;                           // defaults to Camera.main
    public GameObject highlightPrefab;           // the small quad prefab
    [Tooltip("Parent for instantiated highlight (optional)")]
    public Transform highlightParent;

    [Header("Hit Settings")]
    public LayerMask hitLayers = ~0;
    public float maxDistance = 20f;
    [Tooltip("Meters. The highlight quad will be scaled to this square size.")]
    public float highlightSize = 1f;          // 0.1 x 0.1 area
    [Tooltip("Meters. Push off the surface to avoid z-fighting.")]
    public float surfaceOffset = 0.0015f;

    [Header("Smoothing")]
    [Tooltip("How fast the highlight follows position/rotation.")]
    public float followLerp = 12f;
    [Tooltip("How fast the highlight fades in/out.")]
    public float fadeLerp = 10f;

    [Header("Visibility")]
    [Tooltip("Alpha when visible (0..1), multiplied into material color.a")]
    [Range(0,1)] public float visibleAlpha = 1f;
    [Range(0,1)] public float hiddenAlpha = 0f;

    // runtime
    Transform _decal;
    Renderer _decalRenderer;
    Color _baseColor;
    float _currentAlpha;
    bool _hasBaseColor;

    void Awake()
    {
        if (!cam) cam = Camera.main;

        if (highlightPrefab)
        {
            GameObject inst = Instantiate(highlightPrefab, highlightParent ? highlightParent : transform);
            _decal = inst.transform;
            _decal.localScale = new Vector3(highlightSize, highlightSize, highlightSize);

            _decalRenderer = inst.GetComponentInChildren<Renderer>();
            if (_decalRenderer && _decalRenderer.material.HasProperty("_Color"))
            {
                _baseColor = _decalRenderer.material.color;
                _hasBaseColor = true;
                _currentAlpha = 0f;
                SetAlphaInstant(hiddenAlpha);
            }
        }
        else
        {
            Debug.LogWarning("[GazeHighlight] Assign a highlightPrefab (small quad with transparent unlit material).");
        }
    }

    void LateUpdate()
    {
        if (!_decal || !cam) return;

        // Center ray from camera
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        bool hitAny = Physics.Raycast(ray, out var hit, maxDistance, hitLayers, QueryTriggerInteraction.Ignore);

        // target pos/rot/alpha
        Vector3 targetPos = _decal.position;
        Quaternion targetRot = _decal.rotation;
        float targetAlpha = hiddenAlpha;

        if (hitAny)
        {
            // Place on surface with slight offset
            targetPos = hit.point + hit.normal * surfaceOffset;

            // Align quad's +Z (its normal) with the surface normal
            targetRot = Quaternion.LookRotation(hit.normal, Vector3.up);

            // Keep size if you need exact 0.1x0.1, but you can also scale with distance if desired
            _decal.localScale = Vector3.Lerp(_decal.localScale,
                                             new Vector3(highlightSize, highlightSize, highlightSize),
                                             Time.unscaledDeltaTime * followLerp);

            targetAlpha = visibleAlpha;
        }

        // Smooth follow
        _decal.position = Vector3.Lerp(_decal.position, targetPos, Time.unscaledDeltaTime * followLerp);
        _decal.rotation = Quaternion.Slerp(_decal.rotation, targetRot, Time.unscaledDeltaTime * followLerp);

        // Smooth fade
        _currentAlpha = Mathf.Lerp(_currentAlpha, targetAlpha, Time.unscaledDeltaTime * fadeLerp);
        ApplyAlpha(_currentAlpha);
    }

    void SetAlphaInstant(float a)
    {
        _currentAlpha = a;
        ApplyAlpha(a);
    }

    void ApplyAlpha(float a)
    {
        if (!_hasBaseColor || !_decalRenderer) return;

        // If material uses _BaseColor (URP), use that; otherwise _Color
        var mat = _decalRenderer.material;
        if (mat.HasProperty("_BaseColor"))
        {
            var c = mat.GetColor("_BaseColor");
            c.r = _baseColor.r; c.g = _baseColor.g; c.b = _baseColor.b; c.a = a;
            mat.SetColor("_BaseColor", c);
        }
        else if (mat.HasProperty("_Color"))
        {
            var c = mat.color;
            c.r = _baseColor.r; c.g = _baseColor.g; c.b = _baseColor.b; c.a = a;
            mat.color = c;
        }
    }
}
