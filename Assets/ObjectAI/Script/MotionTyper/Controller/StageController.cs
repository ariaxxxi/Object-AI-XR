using System;
using System.Collections.Generic;
using UnityEngine;

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

    // Motion library (designer-facing names)
    BouncyJumpAppearAndFloating, // old systemDotToIcon
    ShrinkDown,                  // old systemIconToDot
    StopFloating,                // old systemPillToPanel
    StartFloating                // old systemPanelToPill
}

[Serializable]
public class EdgeEvents
{
    [Header("Motion (dropdown)")]
    public MotionType forwardMotion = MotionType.None;   // i -> i+1
    public MotionType backwardMotion = MotionType.None;  // (i+1) -> i
}

public class StageController : MonoBehaviour
{
    [Header("Stages (ordered)")]
    public List<StageDef> stages = new()
    {
        new StageDef{ id="dot",   rangeMin=0f, rangeMax=2f },
        new StageDef{ id="icon",  rangeMin=2f, rangeMax=4f },
        new StageDef{ id="pill",  rangeMin=4f, rangeMax=7f },
        new StageDef{ id="panel", rangeMin=7f, rangeMax=10f },
    };

    [Header("Edge Motions (size = stages.Count - 1)")]
    public List<EdgeEvents> edges = new();

    [Header("Global Transition Settings")]
    public float duration = 1f;
    public DG.Tweening.Ease ease = DG.Tweening.Ease.InOutExpo;

    public event Action<int, StageDef, float, DG.Tweening.Ease> OnStageChanged;

    public int CurrentIndex { get; private set; } = -1;
    public StageDef CurrentStage => (CurrentIndex >= 0 && CurrentIndex < stages.Count) ? stages[CurrentIndex] : null;

    private MotionPlayer motionPlayer;

    void Awake()
    {
        motionPlayer = FindObjectOfType<MotionPlayer>();
    }

    void OnEnable()
    {
        ResizeEdges();
        if (stages.Count > 0) ApplyIndex(0);
    }

    void OnValidate() => ResizeEdges();

    void ResizeEdges()
    {
        int target = Mathf.Max(0, stages.Count - 1);
        while (edges.Count < target) edges.Add(new EdgeEvents());
        if (edges.Count > target) edges.RemoveRange(target, edges.Count - target);
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
            bool isLast = (i == stages.Count - 1);
            if ((v >= s.rangeMin && v < s.rangeMax) || (isLast && v <= s.rangeMax)) return i;
        }
        return Mathf.Clamp(CurrentIndex, 0, Mathf.Max(0, stages.Count - 1));
    }

    void ApplyIndex(int newIndex)
    {
        int prev = CurrentIndex;
        CurrentIndex = Mathf.Clamp(newIndex, 0, stages.Count - 1);
        var def = stages[CurrentIndex];

        if (prev >= 0 && prev != CurrentIndex)
        {
            StepEdges(prev, CurrentIndex);
        }

        OnStageChanged?.Invoke(CurrentIndex, def, duration, ease);
    }

    void StepEdges(int from, int to)
    {
        if (edges == null || edges.Count == 0) return;
        if (motionPlayer == null) motionPlayer = FindObjectOfType<MotionPlayer>();

        if (from < to)
        {
            for (int e = from; e < to; e++)
            {
                var ee = edges[e];
                if (motionPlayer && ee.forwardMotion != MotionType.None)
                    motionPlayer.Play(ee.forwardMotion);
            }
        }
        else
        {
            for (int e = from - 1; e >= to; e--)
            {
                var ee = edges[e];
                if (motionPlayer && ee.backwardMotion != MotionType.None)
                    motionPlayer.Play(ee.backwardMotion);
            }
        }
    }
}
