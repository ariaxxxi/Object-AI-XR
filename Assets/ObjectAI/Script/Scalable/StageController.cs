using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using DG.Tweening;

[Serializable]
public class StageDef
{
    public string id = "dot";
    public float rangeMin = 0f;
    public float rangeMax = 2f;
}

[Serializable]
public class EdgeEvents
{
    public UnityEvent onForward;
    public UnityEvent onBackward;
}

public class StageController : MonoBehaviour
{
    [Header("Stages (editable, scalable)")]
    public List<StageDef> stages = new()
    {
        new StageDef{ id="dot",   rangeMin=0f, rangeMax=2f },
        new StageDef{ id="icon",  rangeMin=2f, rangeMax=4f },
        new StageDef{ id="pill",  rangeMin=4f, rangeMax=7f },
        new StageDef{ id="panel", rangeMin=7f, rangeMax=10f },
    };

    [Header("Edge Events (size = stages.Count - 1)")]
    public List<EdgeEvents> edges = new();

    [Header("Global Transition Settings")]
    public float duration = 1f;
    public Ease ease = Ease.InOutExpo;

    public event Action<int, StageDef, float, Ease> OnStageChanged;

    public int CurrentIndex { get; private set; } = -1;
    public StageDef CurrentStage => (CurrentIndex >= 0 && CurrentIndex < stages.Count) ? stages[CurrentIndex] : null;

    void OnEnable()
    {
        ResizeEdges();
        // initialize to first stage
        if (stages.Count > 0) ApplyIndex(0);
    }

    void OnValidate() => ResizeEdges();

    void ResizeEdges()
    {
        int target = Mathf.Max(0, stages.Count - 1);
        while (edges.Count < target) edges.Add(new EdgeEvents());
        if (edges.Count > target) edges.RemoveRange(target, edges.Count - target);
    }

    // ------- Public request API for any input module -------

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

    // Useful if inputs want to jump by ±1, etc.
    public void Nudge(int delta)
    {
        if (stages.Count == 0) return;
        RequestStageIndex(Mathf.Clamp(CurrentIndex + delta, 0, stages.Count - 1));
    }

    // ------- Internals -------

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
        CurrentIndex = newIndex;
        var def = stages[CurrentIndex];

        if (prev >= 0 && prev != CurrentIndex) StepEdges(prev, CurrentIndex);

        OnStageChanged?.Invoke(CurrentIndex, def, duration, ease);
    }

    void StepEdges(int from, int to)
    {
        if (edges == null || edges.Count == 0) return;

        if (from < to)
        {
            for (int e = from; e < to; e++)
                if (e >= 0 && e < edges.Count) edges[e].onForward?.Invoke();
        }
        else
        {
            for (int e = from - 1; e >= to; e--)
                if (e >= 0 && e < edges.Count) edges[e].onBackward?.Invoke();
        }
    }
}
