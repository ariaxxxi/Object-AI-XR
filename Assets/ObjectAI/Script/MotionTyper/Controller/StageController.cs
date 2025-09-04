using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.Events;
using UnityEngine.UI;

[Serializable]
public class StageDef
{
    public string id = "dot";
    [Header("Slider Trigger")] 
    public bool triggerBySlider = true;
    public float rangeMin = 0f;
    public float rangeMax = 2f;

    [Header("Key Trigger")] 
    public bool triggerByKey = false;
    public KeyCode key = KeyCode.None;

    [Header("Manual Trigger")] 
    public bool triggerByManual = false;
    public UnityEngine.Events.UnityEvent onManualTrigger;
}

public enum MotionType
{
    None = 0,
    ThrowUpAndStartFloat, // system: start float (after jump)
    ShrinkDown,                  // system: shrink to tiny
    StopFloatLoop,               // system: stop float (settle)
    StartFloatLoop,              // system: resume float

    // Extended library
    Appear,
    Disappear,
    Wiggle,
    Bounce,
    StartBreatheLoop,
    StopBreatheLoop
}

[Serializable]
public class StageEdgeRule
{
    [Tooltip("From stage index (0..N-1)")]
    public int from = 0;

    [Tooltip("To stage index (0..N-1)")]
    public int to = 1;

    [Header("Library Motion (optional)")]
    public bool useMotion = false;
    public MotionType motion = MotionType.None;

    [Header("Custom Call (optional)")]
    public bool useCustomCall = false;
    public UnityEvent onTraverse;   // call any function(s) here
}

public class StageController : MonoBehaviour
{
    
    [Header("Stages")]
    public List<StageDef> stages = new()
    {
        new StageDef{ id="dot",   rangeMin=0f, rangeMax=2f },
        new StageDef{ id="icon",  rangeMin=2f, rangeMax=4f },
        new StageDef{ id="pill",  rangeMin=4f, rangeMax=7f },
        new StageDef{ id="panel", rangeMin=7f, rangeMax=10f },
    };

    [Header("Transition Events")]
    public List<StageEdgeRule> edges = new(); // ex: 0->1, 1->0, 1->2, 2->1, 2->3, 3->2

    [Header("Global Transition Settings")]
    public float duration = 1f;
    public Ease ease = Ease.InOutExpo;

    [Header("Scoped Motion Library (per controller)")]
    public MotionLibrary motionLibrary; // <<< assign the MotionLibrary for THIS system

    [Header("Slider Input (optional)")]
    [Tooltip("If any stages use Slider trigger, assign the UI Slider to drive values.")]
    public Slider sliderInput;

    // Events for drivers (object-level)
    public event Action<int, StageDef, float, Ease> OnStageChanged;                        // legacy
    public event Action<int, int, StageDef, float, Ease> OnStageChangedDetailed;               // (from,to,...)

    public int CurrentIndex { get; private set; } = -1;
    public StageDef CurrentStage => (CurrentIndex >= 0 && CurrentIndex < stages.Count) ? stages[CurrentIndex] : null;

    void OnEnable()
    {
        // initialize to stage 0
        if (stages.Count > 0) ApplyIndex(0);

        if (sliderInput)
            sliderInput.onValueChanged.AddListener(OnSliderValueChanged);
    }

    void Update()
    {
        // Poll for any per-stage key triggers
        for (int i = 0; i < stages.Count; i++)
        {
            var s = stages[i];
            if (s.triggerByKey && s.key != KeyCode.None)
            {
                if (Input.GetKeyDown(s.key))
                {
                    if (i != CurrentIndex) ApplyIndex(i);
                    break; // only trigger one per frame
                }
            }
        }
    }

    void OnDisable()
    {
        if (sliderInput)
            sliderInput.onValueChanged.RemoveListener(OnSliderValueChanged);
    }

    // ------ Public API ------
    public void RequestStageIndex(int idx)
    {
        idx = Mathf.Clamp(idx, 0, Mathf.Max(0, stages.Count - 1));
        if (idx != CurrentIndex) ApplyIndex(idx);
    }

    public void RequestStageByValue(float value)
    {
        if (stages.Count == 0) return;
        int idx = ResolveIndex(value);
        if (idx != CurrentIndex) ApplyIndex(idx);
    }

    void OnSliderValueChanged(float v) => RequestStageByValue(v);

    public void Nudge(int delta) => RequestStageIndex(Mathf.Clamp(CurrentIndex + delta, 0, Mathf.Max(0, stages.Count - 1)));

    // ------ Internals ------
    int ResolveIndex(float v)
    {
        for (int i = 0; i < stages.Count; i++)
        {
            var s = stages[i];
            bool last = (i == stages.Count - 1);
            if (!s.triggerBySlider) continue; // only consider slider-driven stages
            if ((v >= s.rangeMin && v < s.rangeMax) || (last && v <= s.rangeMax)) return i;
        }
        return Mathf.Clamp(CurrentIndex, 0, Mathf.Max(0, stages.Count - 1));
    }

    /// <summary>
    /// Trigger a stage by its string id (ManualCall or otherwise).
    /// Returns true if a matching stage was found and applied.
    /// </summary>
    public bool TriggerStageById(string stageId)
    {
        if (string.IsNullOrEmpty(stageId) || stages == null) return false;
        for (int i = 0; i < stages.Count; i++)
        {
            if (stages[i] != null && string.Equals(stages[i].id, stageId, StringComparison.Ordinal))
            {
                RequestStageIndex(i);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Manually trigger a stage by index, and invoke that stage's manual event if enabled.
    /// </summary>
    public void RequestStageIndexManual(int idx)
    {
        idx = Mathf.Clamp(idx, 0, Mathf.Max(0, stages.Count - 1));
        if (idx != CurrentIndex) ApplyIndex(idx);
        var s = (idx >= 0 && idx < stages.Count) ? stages[idx] : null;
        if (s != null && s.triggerByManual)
            s.onManualTrigger?.Invoke();
    }

    /// <summary>
    /// Manually trigger a stage by id, and invoke that stage's manual event if enabled.
    /// Returns true if found.
    /// </summary>
    public bool TriggerStageByIdManual(string stageId)
    {
        if (string.IsNullOrEmpty(stageId) || stages == null) return false;
        for (int i = 0; i < stages.Count; i++)
        {
            var s = stages[i];
            if (s != null && string.Equals(s.id, stageId, StringComparison.Ordinal))
            {
                RequestStageIndexManual(i);
                return true;
            }
        }
        return false;
    }

    void ApplyIndex(int newIndex)
    {
        int prev = CurrentIndex;
        CurrentIndex = Mathf.Clamp(newIndex, 0, stages.Count - 1);
        var toDef = stages[CurrentIndex];

        if (prev >= 0 && prev != CurrentIndex)
            PlayPath(prev, CurrentIndex); // triggers motions/events per edge

        // Notify drivers (object-level state tweening)
        OnStageChanged?.Invoke(CurrentIndex, toDef, duration, ease);
        OnStageChangedDetailed?.Invoke(prev, CurrentIndex, toDef, duration, ease);
    }

    void PlayPath(int from, int to)
    {
        var path = FindPathBFS(from, to);
        if (path == null || path.Count == 0)
        {
            // Try direct edge once
            var direct = edges.Find(e => e.from == from && e.to == to);
            if (direct != null) ExecuteEdge(direct);
            return;
        }

        foreach (var edge in path) ExecuteEdge(edge);
    }

    void ExecuteEdge(StageEdgeRule edge)
    {
        // Backward compatibility: If both toggles are false, treat as legacy (auto-detect).
        bool legacy = !edge.useMotion && !edge.useCustomCall;

        // 1) Library motion (optional)
        bool shouldPlayMotion = (legacy && edge.motion != MotionType.None) ||
                                (edge.useMotion && edge.motion != MotionType.None);
        if (motionLibrary && shouldPlayMotion)
            motionLibrary.Play(edge.motion);


        // 2) Custom function(s) (optional)
        if (legacy || edge.useCustomCall)
            edge.onTraverse?.Invoke();

        // If neither motion nor event is set → DO NOTHING (default “inherit behavior”)
        // That means floating or any ongoing behavior continues unchanged.
    }

    List<StageEdgeRule> FindPathBFS(int start, int goal)
    {
        // Build adjacency
        var adj = new Dictionary<int, List<StageEdgeRule>>();
        foreach (var e in edges)
        {
            if (!adj.TryGetValue(e.from, out var list)) adj[e.from] = list = new List<StageEdgeRule>();
            list.Add(e);
        }

        var q = new Queue<int>();
        var came = new Dictionary<int, StageEdgeRule>();
        var visited = new HashSet<int>();

        q.Enqueue(start);
        visited.Add(start);

        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            if (cur == goal) break;

            if (!adj.TryGetValue(cur, out var outs)) continue;
            foreach (var e in outs)
            {
                if (visited.Contains(e.to)) continue;
                visited.Add(e.to);
                came[e.to] = e;
                q.Enqueue(e.to);
            }
        }

        if (!visited.Contains(goal)) return null;

        // Reconstruct
        var path = new List<StageEdgeRule>();
        int node = goal;
        while (node != start)
        {
            var edge = came[node];
            path.Add(edge);
            node = edge.from;
        }
        path.Reverse();
        return path;
    }
    


    #region Editor Preview (instant snap in editor)

#if UNITY_EDITOR
    /// <summary>
    /// Instantly snap all MotionStateDrivers that point to THIS controller to the given stage index.
    /// Works in edit & play mode; doesn’t traverse edges or play motions.
    /// </summary>
    public void PreviewInstant(int index)
    {
        if (stages == null || stages.Count == 0) return;
        index = Mathf.Clamp(index, 0, stages.Count - 1);

        var drivers = UnityEngine.Object.FindObjectsOfType<MotionStateDriver>(true);
        foreach (var d in drivers)
        {
            if (!d) continue;
            if (d.controller == this)
                d.ApplyInstant(index);
        }

        // Keep internal pointer consistent in editor
        CurrentIndex = index;
    }
#endif

    #endregion

}
