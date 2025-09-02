using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class DistanceStageInputMulti : MonoBehaviour
{
    [Serializable]
    public class SystemEntry
    {
        [Header("Who to drive")]
        public StageController controller;     // indices assumed: dot=0, icon=1, pill=2 (override below if different)
        public Transform target;               // measure camera → this

        [Header("Camera (optional)")]
        public Camera cameraOverride;          // else sharedCamera else Camera.main

        [Header("Stage Indices (per controller)")]
        public int dotIndex  = 0;              // > IconMax => Dot
        public int iconIndex = 1;              // PillMax..IconMax => Icon
        public int pillIndex = 2;              // 0..PillMax => Pill

        [Header("Smoothing (seconds)")]
        [Tooltip("Time for the smoothed distance to follow changes. 0 = no smoothing (use raw). Typical 0.15–0.5s.")]
        public float responseTime = 0.25f;

        [Header("Hysteresis (meters)")]
        [Tooltip("Meters added/subtracted at band edges to reduce flicker.")]
        public float hysteresis = 0.05f;

        [Header("Debug")]
        public string label = "System";
        public bool logChanges = false;

        // runtime
        [NonSerialized] public float smoothedDist;
        [NonSerialized] public float rawDist;
        [NonSerialized] public int lastStage = int.MinValue;
        [NonSerialized] public bool initialized;
    }

    [Header("Systems")]
    public List<SystemEntry> systems = new();

    [Header("Camera default")]
    public Camera sharedCamera; // if null, falls back to Camera.main

    [Header("Update")]
    public bool runEveryFrame = true;
    public float checkInterval = 0.05f; // ~20 Hz when not every frame

    [Header("On-Screen Debug")]
    public bool showOnScreen = false;
    public Vector2 screenOrigin = new Vector2(16, 16);
    public float lineHeight = 18f;

    [Header("Global Thresholds (meters)")]
    [SerializeField] private float pillMax = 1.0f;
    [SerializeField] private float iconMax = 1.4f;
    // >1.4 => Dot

    float _accum;

    void Awake()
    {
        if (!sharedCamera) sharedCamera = Camera.main;
    }

    void Update()
    {
        if (!runEveryFrame)
        {
            _accum += Time.unscaledDeltaTime;
            if (_accum < checkInterval) return;
            _accum = 0f;
        }

        float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-4f); // keep EMA stable

        for (int i = 0; i < systems.Count; i++)
        {
            var s = systems[i];
            if (!s.controller || !s.target) continue;

            var cam = s.cameraOverride ? s.cameraOverride : (sharedCamera ? sharedCamera : Camera.main);
            if (!cam) continue;

            // 1) raw distance
            s.rawDist = Vector3.Distance(cam.transform.position, s.target.position);

            // 2) smoothing: EMA with time-constant (responseTime)
            if (!s.initialized)
            {
                s.smoothedDist = s.rawDist;
                s.initialized = true;
            }
            else
            {
                if (s.responseTime <= 0f)
                {
                    // no smoothing: follow raw immediately
                    s.smoothedDist = s.rawDist;
                }
                else
                {
                    // alpha = 1 - exp(-dt/tau)  (time-based, framerate independent)
                    float alpha = 1f - Mathf.Exp(-dt / Mathf.Max(1e-4f, s.responseTime));
                    s.smoothedDist = Mathf.LerpUnclamped(s.smoothedDist, s.rawDist, alpha);
                }
            }

            // 3) hysteresis
            float pillEdge = pillMax;
            float iconEdge = iconMax;
            if (s.lastStage == s.pillIndex) pillEdge += s.hysteresis; // linger in Pill slightly
            if (s.lastStage == s.dotIndex)  iconEdge -= s.hysteresis; // linger in Dot slightly

            // 4) decide stage
            int next =
                (s.smoothedDist <= pillEdge) ? s.pillIndex :
                (s.smoothedDist <= iconEdge) ? s.iconIndex :
                                               s.dotIndex;

            if (next != s.lastStage)
            {
                s.lastStage = next;
                s.controller.RequestStageIndex(next);
                if (s.logChanges)
                    Debug.Log($"[Distance] {s.label}: raw={s.rawDist:F3} sm={s.smoothedDist:F3} → stage={StageName(next, s)}");
            }
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        foreach (var s in systems)
        {
            if (!s?.target) continue;
            UnityEditor.Handles.color = new Color(0.2f, 1f, 0.6f, 0.45f);
            UnityEditor.Handles.DrawWireDisc(s.target.position, Vector3.up, pillMax);
            UnityEditor.Handles.color = new Color(0.2f, 0.6f, 1f, 0.45f);
            UnityEditor.Handles.DrawWireDisc(s.target.position, Vector3.up, iconMax);
        }
    }
#endif

    void OnGUI()
    {
        if (!showOnScreen) return;
        var pos = screenOrigin;
        foreach (var s in systems)
        {
            string camName = s.cameraOverride ? s.cameraOverride.name :
                             (sharedCamera ? sharedCamera.name : (Camera.main ? Camera.main.name : "no cam"));
            string line = $"{s.label}  raw={s.rawDist:F3}  sm={s.smoothedDist:F3}  stage={StageName(s.lastStage, s)}  cam={camName}";
            GUI.Label(new Rect(pos.x, pos.y, 900, lineHeight), line);
            pos.y += lineHeight;
        }
    }

    static string StageName(int idx, SystemEntry s)
    {
        if (idx == s.pillIndex) return "Pill";
        if (idx == s.iconIndex) return "Icon";
        if (idx == s.dotIndex)  return "Dot";
        return $"#{idx}";
    }
}
