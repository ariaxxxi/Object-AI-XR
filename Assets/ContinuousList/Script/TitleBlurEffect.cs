using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public class TitleBlurEffect : MonoBehaviour
{
    [Header("Blur Settings")]
    [Tooltip("The material to use for the blur effect. This material should use a shader that supports a _BlurAmount property.")]
    public Material blurMaterial;

    private Image _image;
    private Material _materialInstance; // To avoid modifying the shared material

    // Property to be set by ListMotionController
    public float BlurAmount
    {
        get => _blurAmount;
        set
        {
            _blurAmount = value;
            if (_materialInstance != null)
            {
                _materialInstance.SetFloat("_BlurAmount", _blurAmount);
            }
        }
    }
    private float _blurAmount = 0f;

    void Awake()
    {
        _image = GetComponent<Image>();
        if (_image == null)
        {
            Debug.LogError("TitleBlurEffect requires an Image component on the same GameObject.", this);
            enabled = false;
            return;
        }

        if (blurMaterial == null)
        {
            Debug.LogError("Blur Material is not assigned. Please assign a material with a blur shader.", this);
            enabled = false;
            return;
        }

        // Create a unique instance of the material so we don't affect other objects using the same material
        _materialInstance = new Material(blurMaterial);
        _image.material = _materialInstance;
    }

    void OnDestroy()
    {
        // Clean up the instantiated material to prevent memory leaks
        if (_materialInstance != null)
        {
            // Check if the image still exists and is using this material before destroying
            if (_image != null && _image.material == _materialInstance)
            {
                 _image.material = null; // Clear the reference before destroying
            }
            Destroy(_materialInstance);
        }
    }
}
