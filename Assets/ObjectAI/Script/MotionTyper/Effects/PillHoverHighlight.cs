using UnityEngine;
using UnityEngine.UI;   // for Image

public class PillHoverHighlight : MonoBehaviour
{
    [Header("References")]
    public Camera cam;
    // pillTarget is now the GameObject this script is attached to
    // outlineImage is now automatically found on the same GameObject

    [Header("Alpha Settings")]
    public float defaultAlpha = 100f / 255f; // ~0.59
    public float highlightAlpha = 1f;       // 255/255
    private float fadeSpeed = 5f;            // how fast to lerp

    private Color outlineColor;
    private bool isHovered;
    private Image outlineImage;

    void Awake()
    {
        if (!cam) cam = Camera.main;
        
        // Get the outline image from the same GameObject
        outlineImage = GetComponent<Image>();
        if (outlineImage) {
            outlineColor = outlineImage.color;
            outlineColor.a = defaultAlpha;
            outlineImage.color = outlineColor;
        }
    }

    void Update()
    {
        if (!cam || !outlineImage) return;

        // Ray from camera center
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);

        // If pill has a collider, check distance
        if (Physics.Raycast(ray, out RaycastHit hit, 10f))
        {
            isHovered = (hit.transform == transform);
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
