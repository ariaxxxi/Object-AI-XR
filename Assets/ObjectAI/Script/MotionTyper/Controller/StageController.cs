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

    [Header("Touch Trigger")] 
    public bool triggerByTouch = false;
    [Tooltip("If true, triggers only on touch/mouse begin; otherwise any touch held.")]
    public bool touchOnBeginOnly = true;

    [Header("Button Trigger")] 
    public bool triggerByButton = false;
    public UnityEngine.UI.Button button;

    [Header("Distance Trigger")] 
    public bool triggerByDistance = false;
    [Tooltip("Distance threshold in world units.")]
    public float distanceThreshold = 1f;

    [Header("Custom Trigger")] 
    public bool triggerByCustom = false;
    public UnityEngine.Events.UnityEvent onCustomTrigger;

    [Header("Stage Trigger")]
    public bool triggerByStage = false;
    [Tooltip("The Index of the stage that, when triggered, will trigger this stage.")]
    public int triggerStageIndex = -1;
    [Tooltip("Delay in seconds before this stage is triggered after the source stage is triggered.")]
    public float triggerStageDelay = 0f;

    [Header("Timing Overrides (on enter)")]
    [Tooltip("Override transition duration when entering THIS stage.")]
    public bool overrideDuration = false;
    public float duration = 1f;

    [Tooltip("Override transition ease when entering THIS stage.")]
    public bool overrideEase = false;
    public Ease ease = Ease.InOutExpo;

    [Tooltip("Delay driver notifications when entering THIS stage (seconds)." )]
    public bool useDelay = false;
    public float delay = 0f;

    [Header("AE-style Ease Override (on enter)")]
    [Tooltip("Override easing using After Effects-style speed & influence.")]
    public bool useAEEase = false;
    [Tooltip("Outgoing speed from start key (arbitrary units → tangent).")]
    public float aeStartSpeed = 1f;
    [Range(0,100)] public float aeStartInfluence = 33f;
    [Tooltip("Incoming speed to end key (arbitrary units → tangent).")]
    public float aeEndSpeed = 1f;
    [Range(0,100)] public float aeEndInfluence = 33f;
    [Tooltip("Scales speed to tangent; tune to taste.")]
    public float aeTangentScale = 1f;
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
    [Tooltip("Overshoot amplitude for InOutBack ease. Ignored for other ease types.")]
    public float overshoot = 1.70158f;

    [Header("UI Motion Library (optional) (per controller)")]
    public UIMotionLibrary motionLibrary; // <<< assign the UIMotionLibrary for THIS system

    [Header("Slider Input (optional) (per controller)")]
    [Tooltip("If any stages use Slider trigger, assign the UI Slider to drive values.")]
    public Slider sliderInput;

    [Header("Distance Input (optional) (per controller)")]
    [Tooltip("Shared target transform for all stages that use Distance trigger.")]
    public Transform distanceTarget;
    [Tooltip("Camera used for distance measurement; if null, falls back to Camera.main.")]
    public Camera distanceCamera;


    [Header("Debug")] 
    [Tooltip("When enabled, logs the distance between the chosen camera and distance target each frame a distance trigger is evaluated.")]
    public bool logDistance;

    // Events for drivers (object-level)
    public event Action<int, StageDef, float, Ease> OnStageChanged;                        // legacy
    public event Action<int, int, StageDef, float, Ease> OnStageChangedDetailed;               // (from,to,...)

    public int CurrentIndex { get; private set; } = -1;
    public StageDef CurrentStage => (CurrentIndex >= 0 && CurrentIndex < stages.Count) ? stages[CurrentIndex] : null;

    private Dictionary<int, Coroutine> _activeStageTriggers = new Dictionary<int, Coroutine>();
    private readonly List<(UnityEngine.UI.Button btn, UnityEngine.Events.UnityAction act)> _buttonSubscriptions = new List<(UnityEngine.UI.Button btn, UnityEngine.Events.UnityAction act)>();
    private Coroutine _pendingDriverNotify;
    // Track whether we were previously inside each stage's distance threshold to fire only on enter
    private readonly List<bool> _distWasInside = new List<bool>();

    void OnEnable()
    {
        // initialize to stage 0
        if (stages.Count > 0) ApplyIndex(0);

        if (sliderInput)
            sliderInput.onValueChanged.AddListener(OnSliderValueChanged);
        // subscribe to per-stage button presses
        SubscribeButtonTriggers();

        // Initialize distance state to current condition to avoid immediate enter-trigger
        InitializeDistanceStates();
    }

    void Update()
    {
        if (stages == null || stages.Count == 0) return;

        // 1) Touch triggers
        for (int i = 0; i < stages.Count; i++)
        {
            var s = stages[i];
            if (!s.triggerByTouch) continue;

            bool touchBegan = Input.touchSupported ? AnyTouchBegan() : Input.GetMouseButtonDown(0);
            bool touchHeld  = Input.touchSupported ? (Input.touchCount > 0) : Input.GetMouseButton(0);
            bool fire = s.touchOnBeginOnly ? touchBegan : touchHeld;
            if (fire)
            {
                if (i != CurrentIndex) ApplyIndex(i);
                return; // only trigger one per frame
            }
        }

        // 2) Distance triggers (fire once on entering threshold; pick most specific)
        {
            bool anyDistanceStage = false;
            for (int i = 0; i < stages.Count; i++) { if (stages[i].triggerByDistance) { anyDistanceStage = true; break; } }
            if (anyDistanceStage && distanceTarget != null)
            {
                var cam = distanceCamera != null ? distanceCamera : Camera.main;
                if (cam != null)
                {
                    float dist = Vector3.Distance(cam.transform.position, distanceTarget.position);
                    if (logDistance)
                    {
                        string camName = cam ? cam.name : "<no camera>";
                        string tgtName = distanceTarget ? distanceTarget.name : "<no target>";
                        Debug.Log($"[StageController] Distance {camName} -> {tgtName} = {dist:0.###}");
                    }

                    // Ensure state list matches stage count
                    while (_distWasInside.Count < stages.Count) _distWasInside.Add(false);
                    if (_distWasInside.Count > stages.Count) _distWasInside.RemoveRange(stages.Count, _distWasInside.Count - stages.Count);

                    int enterIdx = -1;
                    float enterBestThreshold = float.PositiveInfinity;
                    for (int i = 0; i < stages.Count; i++)
                    {
                        var s = stages[i];
                        if (!s.triggerByDistance) { _distWasInside[i] = false; continue; }
                        bool nowInside = dist <= s.distanceThreshold;
                        bool wasInside = _distWasInside[i];
                        if (!wasInside && nowInside)
                        {
                            // entering this stage's threshold this frame
                            if (s.distanceThreshold < enterBestThreshold)
                            {
                                enterBestThreshold = s.distanceThreshold;
                                enterIdx = i;
                            }
                        }
                        _distWasInside[i] = nowInside;
                    }

                    if (enterIdx >= 0)
                    {
                        if (enterIdx != CurrentIndex) ApplyIndex(enterIdx);
                        return;
                    }
                }
            }
        }

        // 3) Key triggers
        for (int i = 0; i < stages.Count; i++)
        {
            var s = stages[i];
            if (s.triggerByKey && s.key != KeyCode.None)
            {
                if (Input.GetKeyDown(s.key))
                {
                    if (i != CurrentIndex) ApplyIndex(i);
                    return; // only trigger one per frame
                }
            }
        }
    }

    bool AnyTouchBegan()
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            if (Input.GetTouch(i).phase == TouchPhase.Began) return true;
        }
        return false;
    }

    void SubscribeButtonTriggers()
    {
        _buttonSubscriptions.Clear();
        if (stages == null) return;
        for (int i = 0; i < stages.Count; i++)
        {
            var s = stages[i];
            if (s != null && s.triggerByButton && s.button != null)
            {
                int idx = i; // capture local
                UnityEngine.Events.UnityAction act = () => RequestStageIndex(idx);
                s.button.onClick.AddListener(act);
                _buttonSubscriptions.Add((s.button, act));
            }
        }
    }

    void UnsubscribeButtonTriggers()
    {
        foreach (var p in _buttonSubscriptions)
        {
            if (p.btn != null && p.act != null)
                p.btn.onClick.RemoveListener(p.act);
        }
        _buttonSubscriptions.Clear();
    }

    void OnDisable()
    {
        if (sliderInput)
            sliderInput.onValueChanged.RemoveListener(OnSliderValueChanged);
        // unsubscribe button listeners
        UnsubscribeButtonTriggers();
    }

    void InitializeDistanceStates()
    {
        if (stages == null) return;
        while (_distWasInside.Count < stages.Count) _distWasInside.Add(false);
        if (_distWasInside.Count > stages.Count) _distWasInside.RemoveRange(stages.Count, _distWasInside.Count - stages.Count);

        if (distanceTarget == null) { for (int i = 0; i < _distWasInside.Count; i++) _distWasInside[i] = false; return; }
        var cam = distanceCamera != null ? distanceCamera : Camera.main;
        if (cam == null) { for (int i = 0; i < _distWasInside.Count; i++) _distWasInside[i] = false; return; }

        float dist = Vector3.Distance(cam.transform.position, distanceTarget.position);
        for (int i = 0; i < stages.Count; i++)
        {
            var s = stages[i];
            if (s != null && s.triggerByDistance)
                _distWasInside[i] = dist <= s.distanceThreshold;
            else
                _distWasInside[i] = false;
        }
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
    /// Manually trigger a stage by index, and invoke that stage's custom event if enabled.
    /// </summary>
    public void RequestStageIndexManual(int idx)
    {
        idx = Mathf.Clamp(idx, 0, Mathf.Max(0, stages.Count - 1));
        if (idx != CurrentIndex) ApplyIndex(idx);
        var s = (idx >= 0 && idx < stages.Count) ? stages[idx] : null;
        if (s != null && s.triggerByCustom)
            s.onCustomTrigger?.Invoke();
    }

    /// <summary>
    /// Manually trigger a stage by id, and invoke that stage's custom event if enabled.
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

        // Resolve per-stage timing overrides
        float effDuration = toDef != null && toDef.overrideDuration ? toDef.duration : duration;
        Ease  effEase     = toDef != null && toDef.overrideEase     ? toDef.ease     : ease;
        float effDelay    = toDef != null && toDef.useDelay         ? toDef.delay    : 0f;

        // Cancel any pending driver notifications
        if (_pendingDriverNotify != null)
        {
            StopCoroutine(_pendingDriverNotify);
            _pendingDriverNotify = null;
        }

        if (effDelay > 0f)
        {
            _pendingDriverNotify = StartCoroutine(NotifyDriversAfterDelay(effDelay, prev, CurrentIndex, toDef, effDuration, effEase));
        }
        else
        {
            NotifyDrivers(prev, CurrentIndex, toDef, effDuration, effEase);
        }

        // Check for stage-based triggers
        CheckAndTriggerDependentStages(CurrentIndex);
    }

    System.Collections.IEnumerator NotifyDriversAfterDelay(float delaySeconds, int prevIndex, int curIndex, StageDef def, float effDuration, Ease effEase)
    {
        yield return new WaitForSeconds(delaySeconds);
        // Ensure still in same stage
        if (this == null || !isActiveAndEnabled) yield break;
        if (curIndex != CurrentIndex) yield break;
        NotifyDrivers(prevIndex, curIndex, def, effDuration, effEase);
        _pendingDriverNotify = null;
    }

    void NotifyDrivers(int prevIndex, int curIndex, StageDef def, float effDuration, Ease effEase)
    {
        OnStageChanged?.Invoke(curIndex, def, effDuration, effEase);
        OnStageChangedDetailed?.Invoke(prevIndex, curIndex, def, effDuration, effEase);
    }

    private void CheckAndTriggerDependentStages(int triggeredStageIndex)
    {
        for (int i = 0; i < stages.Count; i++)
        {
            if (i == triggeredStageIndex) continue; // Don't trigger self

            var dependentStage = stages[i];
            if (dependentStage.triggerByStage && dependentStage.triggerStageIndex == triggeredStageIndex)
            {
                // If this stage is already scheduled to be triggered, cancel the previous trigger.
                if (_activeStageTriggers.TryGetValue(i, out var existingCoroutine))
                {
                    if (existingCoroutine != null)
                    {
                        StopCoroutine(existingCoroutine);
                    }
                    _activeStageTriggers.Remove(i);
                }
                
                var newCoroutine = StartCoroutine(DelayedStageTrigger(i, dependentStage.triggerStageDelay));
                _activeStageTriggers[i] = newCoroutine;
            }
        }
    }

    private System.Collections.IEnumerator DelayedStageTrigger(int stageIndexToTrigger, float delay)
    {
        yield return new WaitForSeconds(delay);

        // Ensure the stage index is still valid and the controller hasn't been destroyed or disabled.
        if (this == null || !this.isActiveAndEnabled || stageIndexToTrigger < 0 || stageIndexToTrigger >= stages.Count)
        {
            _activeStageTriggers.Remove(stageIndexToTrigger);
            yield break;
        }
        
        // Check if the stage is not already active before triggering.
        if (CurrentIndex != stageIndexToTrigger)
        {
            ApplyIndex(stageIndexToTrigger);
        }

        // Clean up the dictionary entry
        if (_activeStageTriggers.ContainsKey(stageIndexToTrigger))
        {
            _activeStageTriggers.Remove(stageIndexToTrigger);
        }
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
