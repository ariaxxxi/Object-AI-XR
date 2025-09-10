using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using System.Reflection;
using System.Linq.Expressions;



[DisallowMultipleComponent]
public class MotionStateDriverDynamic : MonoBehaviour
{
    [Header("Stage Source")]
    public StageController controller;

    [Header("Global Timing (fallback)")]
    public bool overrideDuration;
    public float duration = 1f;
    public bool overrideEase;
    public Ease ease = Ease.InOutExpo;
    public float startDelay;

    [Header("Tracks (user-defined)")]
    public List<TrackBase> tracks = new();

    [SerializeField] string tweenId;

    void Awake()
    {
        if (string.IsNullOrEmpty(tweenId)) tweenId = "DRV_DYN_" + GetInstanceID();
        foreach (var t in tracks) t?.BuildAccessors();
    }

    void OnEnable()
    {
        if (!controller) controller = FindObjectOfType<StageController>();
        if (controller != null)
        {
            controller.OnStageChanged += ApplyStageLegacy;
            controller.OnStageChangedDetailed += ApplyStageDetailed;
            EnsureListSizes();
            if (controller.CurrentIndex >= 0) ApplyInstant(controller.CurrentIndex);
        }
    }
    void OnDisable()
    {
        if (controller != null)
        {
            controller.OnStageChanged -= ApplyStageLegacy;
            controller.OnStageChangedDetailed -= ApplyStageDetailed;
        }
        DOTween.Kill(tweenId, false);
    }

    void OnValidate()
    {
        if (string.IsNullOrEmpty(tweenId)) tweenId = "DRV_DYN_" + GetInstanceID();
        foreach (var t in tracks) t?.BuildAccessors();
        EnsureListSizes();
    }

    public void EnsureListSizes()
    {
        int count = controller && controller.stages != null ? controller.stages.Count : 0;
        if (count <= 0) return;
        foreach (var t in tracks) t?.EnsureSize(count);
    }

    void ApplyStageLegacy(int toIndex, StageDef def, float globalDuration, Ease globalEase)
        => ApplyStageInternal(toIndex, def, globalDuration, globalEase);
    void ApplyStageDetailed(int fromIndex, int toIndex, StageDef def, float globalDuration, Ease globalEase)
        => ApplyStageInternal(toIndex, def, globalDuration, globalEase);

    void ApplyStageInternal(int toIndex, StageDef def, float globalDuration, Ease globalEase)
    {
        if (toIndex < 0) return;
        DOTween.Kill(tweenId, false);

        float effDur = overrideDuration ? duration : globalDuration;
        Ease  effEase= overrideEase     ? ease     : globalEase;

        bool useOvershoot = effEase == Ease.InOutBack && controller != null;
        float overshoot = useOvershoot ? controller.overshoot : 0f;

        // Build optional AE-style curve
        var customCurve = BuildAEEaseCurve(def);
        if (customCurve != null) useOvershoot = false; // ignore overshoot when using custom curve

        foreach (var t in tracks)
        {
            if (t == null || !t.enabled) continue;
            t.ApplyTween(toIndex, effDur, effEase, tweenId, startDelay, useOvershoot, overshoot, customCurve);
        }
    }

    public void ApplyInstant(int index)
    {
        foreach (var t in tracks)
        {
            if (t == null || !t.enabled) continue;
            t.ApplyInstant(index);
        }
    }

    static AnimationCurve BuildAEEaseCurve(StageDef def)
    {
        if (def == null || !def.useAEEase) return null;
        float tScale = Mathf.Max(0.01f, def.aeTangentScale);
        float outTan0 = def.aeStartSpeed * tScale;
        float inTan1  = def.aeEndSpeed   * tScale;
        var k0 = new Keyframe(0f, 0f, 0f, outTan0);
        var k1 = new Keyframe(1f, 1f, inTan1, 0f);
#if UNITY_2018_1_OR_NEWER
        k0.weightedMode = WeightedMode.Both;
        k1.weightedMode = WeightedMode.Both;
        k0.outWeight = Mathf.Clamp01(def.aeStartInfluence / 100f);
        k1.inWeight  = Mathf.Clamp01(def.aeEndInfluence   / 100f);
#endif
        return new AnimationCurve(k0, k1);
    }
}
