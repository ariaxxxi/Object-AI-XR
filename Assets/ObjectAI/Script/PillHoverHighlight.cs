using UnityEngine;
using UnityEngine.UI;   // for Image

public class PillHoverHighlight : MonoBehaviour
{
    [Header("References")]
    public Camera cam;
    public Transform pillTarget;      // the pill object with a collider
    public Image outlineImage;        // UI outline (world-space canvas element)

    [Header("Alpha Settings")]
    public float defaultAlpha = 100f / 255f; // ~0.59
    public float highlightAlpha = 1f;       // 255/255
    public float fadeSpeed = 5f;            // how fast to lerp

    private Color outlineColor;
    private bool isHovered;

    void Awake()
    {
        if (!cam) cam = Camera.main;
        if (outlineImage) {
            outlineColor = outlineImage.color;
            outlineColor.a = defaultAlpha;
            outlineImage.color = outlineColor;
        }
    }

    void Update()
    {
        if (!cam || !pillTarget || !outlineImage) return;

        // Ray from camera center
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);

        // If pill has a collider, check distance
        if (Physics.Raycast(ray, out RaycastHit hit, 10f))
        {
            isHovered = (hit.transform == pillTarget);
        }
        else
        {
            isHovered = false;
        }

        // Smooth fade
        float targetAlpha = isHovered ? highlightAlpha : defaultAlpha;
        outlineColor = outlineImage.color;
        outlineColor.a = Mathf.Lerp(outlineColor.a, targetAlpha, Time.deltaTime * fadeSpeed);
        outlineImage.color = outlineColor;
    }
}
