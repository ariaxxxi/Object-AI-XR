using UnityEngine;

public class LookAtCamera : MonoBehaviour
{
    [Header("Target Camera")]
    public Camera targetCamera; // defaults to Camera.main

    [Header("Rotation Settings")]
    [Tooltip("How fast the object rotates to face the camera.")]
    public float rotationSpeed = 5f;

    [Tooltip("Flip 180° around Y axis")]
    public bool flipY = true;

    void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    void LateUpdate()
    {
        if (!targetCamera) return;

        // Direction from object to camera
        Vector3 dir = targetCamera.transform.position - transform.position;

        if (dir.sqrMagnitude < 0.001f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir);

        if (flipY)
        {
            // Multiply by a 180° rotation around Y axis
            targetRot *= Quaternion.Euler(0f, 180f, 0f);
        }

        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
    }
}
