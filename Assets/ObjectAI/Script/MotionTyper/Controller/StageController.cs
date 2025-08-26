using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

[Serializable]
public class StageDef
{
    public string id = "dot";
    public float rangeMin = 0f;
    public float rangeMax = 2f;
}

public enum MotionType
{
    None = 0,
    BouncyJumpAppearAndFloating, // (old systemDotToIcon)
    ShrinkDown,                  // (old systemIconToDot)
    StopFloating,                // (old systemPillToPanel)
    StartFloating                // (old systemPanelToPill)
}

[Serializable]
public class StageEdgeRule
{
    [Tooltip("From stage index (0..N-1)")]
    public int from = 0;
    [Tooltip("To stage index (0..N-1)")]
    public int to = 1;
    [Tooltip("Which motion to play when traversing this edge")]
    public MotionType motion = MotionType.None;
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
    public List<StageEdgeRule> edges = new(); // e.g. add: 0->1, 1->0, 1->2, 2->1, 2->3, 3->2
    public event System.Action<int,int,StageDef,float,DG.Tweening.Ease> OnStageChangedDetailed;

    [Header("Global Transition Settings")]
    public float duration = 1f;
    public Ease ease = Ease.InOutExpo;

    public event Action<int, StageDef, float, Ease> OnStageChanged;

    public int CurrentIndex { get; private set; } = -1;
    public StageDef CurrentStage => (CurrentIndex >= 0 && CurrentIndex < stages.Count) ? stages[CurrentIndex] : null;

    MotionPlayer motionPlayer;

    void Awake()
    {
        motionPlayer = FindObjectOfType<MotionPlayer>();
    }

    void OnEnable()
    {
        if (stages.Count > 0) ApplyIndex(0); // initialize
    }

    // -------- Public requests (from inputs) --------
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

    public void Nudge(int delta) => RequestStageIndex(Mathf.Clamp(CurrentIndex + delta, 0, Mathf.Max(0, stages.Count - 1)));

    // -------- Internals --------
    int ResolveIndex(float v)
    {
        for (int i = 0; i < stages.Count; i++)
        {
            var s = stages[i];
            bool last = (i == stages.Count - 1);
            if ((v >= s.rangeMin && v < s.rangeMax) || (last && v <= s.rangeMax)) return i;
        }
        return Mathf.Clamp(CurrentIndex, 0, Mathf.Max(0, stages.Count - 1));
    }

    void ApplyIndex(int newIndex)
    {
        int prev = CurrentIndex;
        CurrentIndex = Mathf.Clamp(newIndex, 0, stages.Count - 1);

        // 1) Play motions along path (if any)
        if (prev >= 0 && prev != CurrentIndex)
        {
            PlayPath(prev, CurrentIndex);
        }

        // 2) Broadcast to object drivers
        var def = stages[CurrentIndex];
        OnStageChanged?.Invoke(CurrentIndex, def, duration, ease);

        OnStageChangedDetailed?.Invoke(prev, CurrentIndex, def, duration, ease);

    }

    void PlayPath(int from, int to)
    {
        if (motionPlayer == null) motionPlayer = FindObjectOfType<MotionPlayer>();
        if (edges == null || edges.Count == 0 || motionPlayer == null) return;

        // BFS over the directed graph the user defined
        var path = FindPathBFS(from, to);
        if (path != null && path.Count > 0)
        {
            foreach (var edge in path)
                if (edge.motion != MotionType.None) motionPlayer.Play(edge.motion);
            return;
        }

        // Fallback: try single direct edge (from->to) if user defined it
        var direct = edges.Find(e => e.from == from && e.to == to);
        if (direct != null && direct.motion != MotionType.None)
            motionPlayer.Play(direct.motion);
    }

    List<StageEdgeRule> FindPathBFS(int start, int goal)
    {
        // Build adjacency map
        var adj = new Dictionary<int, List<StageEdgeRule>>();
        foreach (var e in edges)
        {
            if (!adj.TryGetValue(e.from, out var list)) adj[e.from] = list = new List<StageEdgeRule>();
            list.Add(e);
        }

        var queue = new Queue<int>();
        var cameFrom = new Dictionary<int, StageEdgeRule>(); // key=node, value=edge that got us here
        var visited = new HashSet<int>();

        queue.Enqueue(start);
        visited.Add(start);

        while (queue.Count > 0)
        {
            int cur = queue.Dequeue();
            if (cur == goal) break;

            if (!adj.TryGetValue(cur, out var outs)) continue;
            foreach (var e in outs)
            {
                if (visited.Contains(e.to)) continue;
                visited.Add(e.to);
                cameFrom[e.to] = e;
                queue.Enqueue(e.to);
            }
        }

        if (!visited.Contains(goal)) return null;

        // Reconstruct edges path: start -> ... -> goal
        var result = new List<StageEdgeRule>();
        int node = goal;
        while (node != start)
        {
            var edge = cameFrom[node];
            result.Add(edge);
            node = edge.from;
        }
        result.Reverse();
        return result;
    }
}
