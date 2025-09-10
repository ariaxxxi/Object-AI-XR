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

[Serializable]
public abstract class TrackBase
{
    [Header("Binding")]
    public string displayName = "Track";
    public Component target;
    public string memberName;  // property/field name

    [Header("Enable")]
    public bool enabled = true;

    public abstract Type ValueType { get; }
    public abstract int Count { get; }
    public abstract void EnsureSize(int stageCount);
    public abstract void ApplyTween(int idx, float duration, Ease ease, string tweenId, float delay, bool useOvershoot, float overshoot, AnimationCurve customCurve);
    public abstract void ApplyInstant(int idx);
    public abstract void BuildAccessors();
}




[Serializable]
public class FloatTrack : TrackBase
{
    public List<float> values = new();
    Func<float> getter;
    Action<float> setter;

    public override Type ValueType => typeof(float);
    public override int Count => values?.Count ?? 0;

    public override void EnsureSize(int stageCount)
    {
        if (values == null) values = new List<float>();
        float cur = SafeRead();
        while (values.Count < stageCount) values.Add(cur);
        if (values.Count > stageCount) values.RemoveRange(stageCount, values.Count - stageCount);
    }

    public override void BuildAccessors()
    {
        getter = null; setter = null;
        if (!target || string.IsNullOrEmpty(memberName)) return;
        ReflectionAccessors.BuildFloat(target, memberName, out getter, out setter);
    }

    public override void ApplyTween(int idx, float duration, Ease ease, string tweenId, float delay, bool useOvershoot, float overshoot, AnimationCurve customCurve)
    {
        if (setter == null || idx < 0 || idx >= values.Count) return;
        float to = values[idx];
        var tween = DOTween.To(() => getter != null ? getter() : to, x => setter(x), to, duration)
                      .SetDelay(delay).SetId(tweenId);
        if (customCurve != null) tween.SetEase(customCurve);
        else if (useOvershoot) tween.SetEase(ease, overshoot);
        else tween.SetEase(ease);
    }

    public override void ApplyInstant(int idx)
    {
        if (setter == null || idx < 0 || idx >= values.Count) return;
        setter(values[idx]);
    }

    float SafeRead()
    {
        try { return getter != null ? getter() : 0f; } catch { return 0f; }
    }
}

[Serializable]
public class ColorTrack : TrackBase
{
    public List<Color> values = new();
    Func<Color> getter;
    Action<Color> setter;

    public override Type ValueType => typeof(Color);
    public override int Count => values?.Count ?? 0;

    public override void EnsureSize(int stageCount)
    {
        if (values == null) values = new List<Color>();
        Color cur = SafeRead();
        while (values.Count < stageCount) values.Add(cur);
        if (values.Count > stageCount) values.RemoveRange(stageCount, values.Count - stageCount);
    }

    public override void BuildAccessors()
    {
        getter = null; setter = null;
        if (!target || string.IsNullOrEmpty(memberName)) return;
        ReflectionAccessors.BuildColor(target, memberName, out getter, out setter);
    }

    public override void ApplyTween(int idx, float duration, Ease ease, string tweenId, float delay, bool useOvershoot, float overshoot, AnimationCurve customCurve)
    {
        if (setter == null || idx < 0 || idx >= values.Count) return;
        var to = values[idx];
        var tween = DOTween.To(() => getter != null ? getter() : to, c => setter(c), to, duration)
                      .SetDelay(delay).SetId(tweenId);
        if (customCurve != null) tween.SetEase(customCurve);
        else if (useOvershoot) tween.SetEase(ease, overshoot);
        else tween.SetEase(ease);
    }

    public override void ApplyInstant(int idx)
    {
        if (setter == null || idx < 0 || idx >= values.Count) return;
        setter(values[idx]);
    }

    Color SafeRead()
    {
        try { return getter != null ? getter() : Color.white; } catch { return Color.white; }
    }
}

[Serializable]
public class Vector3Track : TrackBase
{
    public List<Vector3> values = new();
    Func<Vector3> getter;
    Action<Vector3> setter;

    public override Type ValueType => typeof(Vector3);
    public override int Count => values?.Count ?? 0;

    public override void EnsureSize(int stageCount)
    {
        if (values == null) values = new List<Vector3>();
        Vector3 cur = SafeRead();
        while (values.Count < stageCount) values.Add(cur);
        if (values.Count > stageCount) values.RemoveRange(stageCount, values.Count - stageCount);
    }

    public override void BuildAccessors()
    {
        getter = null; setter = null;
        if (!target || string.IsNullOrEmpty(memberName)) return;
        ReflectionAccessors.BuildVector3(target, memberName, out getter, out setter);
    }

    public override void ApplyTween(int idx, float duration, Ease ease, string tweenId, float delay, bool useOvershoot, float overshoot, AnimationCurve customCurve)
    {
        if (setter == null || idx < 0 || idx >= values.Count) return;
        var to = values[idx];
        var tween = DOTween.To(() => getter != null ? getter() : to, v => setter(v), to, duration)
                      .SetDelay(delay).SetId(tweenId);
        if (customCurve != null) tween.SetEase(customCurve);
        else if (useOvershoot) tween.SetEase(ease, overshoot);
        else tween.SetEase(ease);
    }

    public override void ApplyInstant(int idx)
    {
        if (setter == null || idx < 0 || idx >= values.Count) return;
        setter(values[idx]);
    }

    Vector3 SafeRead()
    {
        try { return getter != null ? getter() : Vector3.zero; } catch { return Vector3.zero; }
    }
}

public static class ReflectionAccessors
{
    static bool TryMember(Type t, string name, out MemberInfo m, out Type valueType)
    {
        m = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
          ?? (MemberInfo)t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (m == null) { valueType = null; return false; }
        valueType = m is PropertyInfo pi ? pi.PropertyType : ((FieldInfo)m).FieldType;
        return true;
    }

    public static void BuildFloat(Component target, string member, out Func<float> getter, out Action<float> setter)
    {
        getter = null; setter = null;
        if (!TryMember(target.GetType(), member, out var m, out var vt)) return;
        var inst = Expression.Constant(target);
        if (m is PropertyInfo pi)
        {
            if (vt == typeof(float) && pi.CanRead)
                getter = Expression.Lambda<Func<float>>(Expression.Property(inst, pi)).Compile();
            if (vt == typeof(float) && pi.CanWrite)
            {
                var v = Expression.Parameter(typeof(float), "v");
                setter = Expression.Lambda<Action<float>>(Expression.Assign(Expression.Property(inst, pi), v), v).Compile();
            }
        }
        else if (m is FieldInfo fi && vt == typeof(float))
        {
            getter = Expression.Lambda<Func<float>>(Expression.Field(inst, fi)).Compile();
            var v = Expression.Parameter(typeof(float), "v");
            setter = Expression.Lambda<Action<float>>(Expression.Assign(Expression.Field(inst, fi), v), v).Compile();
        }
    }

    public static void BuildColor(Component target, string member, out Func<Color> getter, out Action<Color> setter)
    {
        getter = null; setter = null;
        if (!TryMember(target.GetType(), member, out var m, out var vt)) return;
        var inst = Expression.Constant(target);
        if (m is PropertyInfo pi)
        {
            if (vt == typeof(Color) && pi.CanRead)
                getter = Expression.Lambda<Func<Color>>(Expression.Property(inst, pi)).Compile();
            if (vt == typeof(Color) && pi.CanWrite)
            {
                var v = Expression.Parameter(typeof(Color), "v");
                setter = Expression.Lambda<Action<Color>>(Expression.Assign(Expression.Property(inst, pi), v), v).Compile();
            }
        }
        else if (m is FieldInfo fi && vt == typeof(Color))
        {
            getter = Expression.Lambda<Func<Color>>(Expression.Field(inst, fi)).Compile();
            var v = Expression.Parameter(typeof(Color), "v");
            setter = Expression.Lambda<Action<Color>>(Expression.Assign(Expression.Field(inst, fi), v), v).Compile();
        }
    }

    public static void BuildVector3(Component target, string member, out Func<Vector3> getter, out Action<Vector3> setter)
    {
        getter = null; setter = null;
        if (!TryMember(target.GetType(), member, out var m, out var vt)) return;
        var inst = Expression.Constant(target);
        if (m is PropertyInfo pi)
        {
            if (vt == typeof(Vector3) && pi.CanRead)
                getter = Expression.Lambda<Func<Vector3>>(Expression.Property(inst, pi)).Compile();
            if (vt == typeof(Vector3) && pi.CanWrite)
            {
                var v = Expression.Parameter(typeof(Vector3), "v");
                setter = Expression.Lambda<Action<Vector3>>(Expression.Assign(Expression.Property(inst, pi), v), v).Compile();
            }
        }
        else if (m is FieldInfo fi && vt == typeof(Vector3))
        {
            getter = Expression.Lambda<Func<Vector3>>(Expression.Field(inst, fi)).Compile();
            var v = Expression.Parameter(typeof(Vector3), "v");
            setter = Expression.Lambda<Action<Vector3>>(Expression.Assign(Expression.Field(inst, fi), v), v).Compile();
        }
    }
}
