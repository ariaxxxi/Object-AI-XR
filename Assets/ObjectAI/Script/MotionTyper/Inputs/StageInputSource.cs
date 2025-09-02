using UnityEngine;

public abstract class StageInputSource : MonoBehaviour
{
    [Tooltip("If null, will FindObjectOfType at runtime.")]
    public StageController controller;

    protected virtual void Awake()
    {
        if (!controller) controller = FindObjectOfType<StageController>();
        if (!controller) Debug.LogWarning($"[StageInputSource] No StageController found on {name}.");
    }

    protected void RequestIndex(int idx)      => controller?.RequestStageIndex(idx);
    protected void RequestByValue(float val)  => controller?.RequestStageByValue(val);
    protected void Nudge(int delta)           => controller?.Nudge(delta);
}
