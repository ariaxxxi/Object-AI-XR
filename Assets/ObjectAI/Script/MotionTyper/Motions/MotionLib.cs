using UnityEngine;

public static class MotionLib
{
    // Ensure a FloatingBehaviour exists and start it
    public static void StartFloating(Transform t)
    {
        var fb = t.GetComponent<FloatingBehaviour>() ?? t.gameObject.AddComponent<FloatingBehaviour>();
        fb.StartFloating();
    }

    public static void StopFloating(Transform t)
    {
        var fb = t.GetComponent<FloatingBehaviour>();
        if (fb) fb.StopFloating();
    }
}