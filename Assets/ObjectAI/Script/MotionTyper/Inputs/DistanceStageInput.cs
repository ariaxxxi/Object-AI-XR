using UnityEngine;

// Drives Dot(0)/Icon(1)/Pill(2) by camera distance; Panel(3) is left to the button input.
public class DistanceStageInput : StageInputSource
{
    [Header("Targets")]
    public Transform target;     // the object the camera approaches
    public Camera cam;           // if null, uses Camera.main

    [Header("Thresholds (world units)")]
    public float iconThreshold = 2f; // >= this => Dot
    public float pillThreshold = 1f; // < this => Pill; between => Icon

    [Header("Update")]
    public bool runEveryFrame = true;
    public float checkInterval = 0.05f; // if not every frame

    [Header("Hysteresis (to avoid flicker)")]
    public float hysteresis = 0.25f; // expands thresholds +/- to stabilize

    float _timer;
    int _lastIndex = -1;

    void Reset()
    {
        if (!cam) cam = Camera.main;
    }

    void Update()
    {
        if (!runEveryFrame)
        {
            _timer += Time.deltaTime;
            if (_timer < checkInterval) return;
            _timer = 0f;
        }

        if (!cam) cam = Camera.main;
        if (!cam || !target || controller == null || controller.stages.Count < 3) return;

        float dist = Vector3.Distance(cam.transform.position, target.position);

        // Apply hysteresis around thresholds
        float iconTh = iconThreshold;
        float pillTh = pillThreshold;
        if (_lastIndex == 0) iconTh += hysteresis;         // make it a bit harder to leave Dot
        if (_lastIndex == 2) pillTh -= hysteresis;         // make it a bit harder to leave Pill

        int idx =
            (dist >= iconTh)      ? 0 :                    // Dot
            (dist < pillTh)       ? 2 :                    // Pill
                                    1;                     // Icon

        if (idx != _lastIndex)
        {
            _lastIndex = idx;
            RequestIndex(idx);
        }
    }
}
