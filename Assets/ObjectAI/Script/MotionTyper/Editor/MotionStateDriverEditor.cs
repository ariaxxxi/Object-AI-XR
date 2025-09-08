#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using DG.Tweening;
using System;
using System.Collections.Generic;

[CustomEditor(typeof(MotionStateDriver))]
public class MotionStateDriverEditor : Editor
{
    MotionStateDriver d;

    void OnEnable() => d = (MotionStateDriver)target;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // ----------------------------------Stage Controller-----------------------------------------------

        EditorGUILayout.PropertyField(serializedObject.FindProperty("controller"));

        // ----------------------------------Motion Tracks-----------------------------------------------
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Motion Tracks", EditorStyles.boldLabel);
        d.useLocalPosition    = EditorGUILayout.ToggleLeft("RectTransform Position", d.useLocalPosition);
        d.useEuler            = EditorGUILayout.ToggleLeft("RectTransform Rotation", d.useEuler);
        d.useUniformScale     = EditorGUILayout.ToggleLeft("RectTransform Scale", d.useUniformScale);
        d.useSizeDelta        = EditorGUILayout.ToggleLeft("RectTransform Width Height", d.useSizeDelta);
        d.useAlpha            = EditorGUILayout.ToggleLeft("CanvasGroup Alpha", d.useAlpha);
        d.useTransform3DPosition = EditorGUILayout.ToggleLeft("3D Position", d.useTransform3DPosition);
        d.useTransform3DRotation = EditorGUILayout.ToggleLeft("3D Rotation", d.useTransform3DRotation);
        d.useTransform3DScale    = EditorGUILayout.ToggleLeft("3D Scale", d.useTransform3DScale);

        // ------- per-track generic drawers (unchanged) -------
        void DrawTrack<T>(
            string title,
            int count,
            System.Func<int, T> get,
            System.Action<int, T> set,
            System.Func<int, T, T> renderField,
            System.Func<T> captureLive)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);

            for (int i = 0; i < count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var cur = get(i);
                    var next = renderField(i, cur);
                    if (!Equals(cur, next))
                    {
                        Undo.RecordObject(d, $"Edit {title} Stage {i}");
                        set(i, next);
                        EditorUtility.SetDirty(d);
                    }

                    if (GUILayout.Button("Capture", GUILayout.Width(80)))
                    {
                        Undo.RecordObject(d, $"Capture {title} Stage {i}");
                        set(i, captureLive());
                        EditorUtility.SetDirty(d);
                    }
                }
            }
        }

        static T SafeGet<T>(System.Collections.Generic.List<T> list, int index, T fallback)
        {
            if (list == null || index < 0 || index >= list.Count) return fallback;
            return list[index];
        }

        static void SafeSet<T>(System.Collections.Generic.List<T> list, int index, T value, T fallback, int requiredCount)
        {
            if (list == null) return;
            while (list.Count < requiredCount) list.Add(fallback);
            if (index < 0 || index >= list.Count) return;
            list[index] = value;
        }



        // Stage-dependent lists UI (only shown if StageController present)
        if (d.controller == null || d.controller.stages == null || d.controller.stages.Count == 0)
        {
            EditorGUILayout.HelpBox("Assign a StageController with stages to edit per-stage values.", MessageType.Info);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        int count = d.controller.stages.Count;

        // Draw per-stage values for enabled tracks
        if (d.useLocalPosition)
            DrawTrack("RectTransform Position", count,
                i => SafeGet(d.localPosPerStage, i, d.rt ? d.rt.localPosition : Vector3.zero),
                (i, v) => SafeSet(d.localPosPerStage, i, v, d.rt ? d.rt.localPosition : Vector3.zero, count),
                (i, v) => EditorGUILayout.Vector3Field($"  Stage {i}", v),
                () => d.rt ? d.rt.localPosition : Vector3.zero
            );

        if (d.useEuler)
            DrawTrack("RectTransform Rotation", count,
                i => SafeGet(d.eulerPerStage, i, d.rt ? d.rt.localEulerAngles : Vector3.zero),
                (i, v) => SafeSet(d.eulerPerStage, i, v, d.rt ? d.rt.localEulerAngles : Vector3.zero, count),
                (i, v) => EditorGUILayout.Vector3Field($"  Stage {i}", v),
                () => d.rt ? d.rt.localEulerAngles : Vector3.zero
            );

        if (d.useUniformScale)
            DrawTrack("RectTransform Scale", count,
                i => SafeGet(d.scalePerStage, i, d.rt ? d.rt.localScale : Vector3.one),
                (i, v) => SafeSet(d.scalePerStage, i, v, d.rt ? d.rt.localScale : Vector3.one, count),
                (i, v) => EditorGUILayout.Vector3Field($"  Stage {i}", v),
                () => d.rt ? d.rt.localScale : Vector3.one
            );

        if (d.useSizeDelta)
            DrawTrack("RectTransform Width Height", count,
                i => SafeGet(d.sizePerStage, i, d.rt ? d.rt.sizeDelta : new Vector2(100, 100)),
                (i, v) => SafeSet(d.sizePerStage, i, v, d.rt ? d.rt.sizeDelta : new Vector2(100, 100), count),
                (i, v) => EditorGUILayout.Vector2Field($"  Stage {i}", v),
                () => d.rt ? d.rt.sizeDelta : new Vector2(100, 100)
            );

        if (d.useAlpha)
            DrawTrack("CanvasGroup Alpha", count,
                i => SafeGet(d.alphaPerStage, i, d.cg ? d.cg.alpha : 1f),
                (i, v) => SafeSet(d.alphaPerStage, i, Mathf.Clamp01(v), d.cg ? d.cg.alpha : 1f, count),
                (i, v) => Mathf.Clamp01(EditorGUILayout.Slider($"  Stage {i}", v, 0f, 1f)),
                () => d.cg ? d.cg.alpha : 1f
            );

        if (d.useTransform3DPosition)
            DrawTrack("3D Position", count,
                i => SafeGet(d.transform3DPositionPerStage, i, d.tf ? d.tf.localPosition : Vector3.zero),
                (i, v) => SafeSet(d.transform3DPositionPerStage, i, v, d.tf ? d.tf.localPosition : Vector3.zero, count),
                (i, v) => EditorGUILayout.Vector3Field($"  Stage {i}", v),
                () => d.tf ? d.tf.localPosition : Vector3.zero
            );

        if (d.useTransform3DRotation)
            DrawTrack("3D Rotation", count,
                i => SafeGet(d.transform3DRotationPerStage, i, d.tf ? d.tf.localEulerAngles : Vector3.zero),
                (i, v) => SafeSet(d.transform3DRotationPerStage, i, v, d.tf ? d.tf.localEulerAngles : Vector3.zero, count),
                (i, v) => EditorGUILayout.Vector3Field($"  Stage {i}", v),
                () => d.tf ? d.tf.localEulerAngles : Vector3.zero
            );

        if (d.useTransform3DScale)
            DrawTrack("3D Scale", count,
                i => SafeGet(d.transform3DScalePerStage, i, d.tf ? d.tf.localScale : Vector3.one),
                (i, v) => SafeSet(d.transform3DScalePerStage, i, v, d.tf ? d.tf.localScale : Vector3.one, count),
                (i, v) => EditorGUILayout.Vector3Field($"  Stage {i}", v),
                () => d.tf ? d.tf.localScale : Vector3.one
            );

        serializedObject.ApplyModifiedProperties();



        // ----------------------------------Timing Overrides-----------------------------------------------

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            d.showGlobalTimingOverrides = EditorGUILayout.ToggleLeft("", d.showGlobalTimingOverrides, GUILayout.Width(18));
            EditorGUILayout.LabelField("Global Timing Overrides", EditorStyles.boldLabel);
        }

        if (d.showGlobalTimingOverrides)
        {
            d.overrideDuration = EditorGUILayout.Toggle("Override Duration", d.overrideDuration);
            if (d.overrideDuration) d.duration = EditorGUILayout.FloatField("  Duration", d.duration);
            d.overrideEase = EditorGUILayout.Toggle("Override Ease", d.overrideEase);
            if (d.overrideEase) d.ease = (Ease)EditorGUILayout.EnumPopup("  Ease", d.ease);
            d.startDelay = EditorGUILayout.FloatField("Start Delay", d.startDelay);
        }

        DrawEdgeTimingOverrides();

        // ----------------------------------Dynamic Tracks-----------------------------------------------
        EditorGUILayout.Space(8);
        DrawDynamicTracks();
    }



    void DrawEdgeTimingOverrides()
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Per-Edge Timing Overrides", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("+ Add Edge Override"))
            {
                Undo.RecordObject(d, "Add Edge Override");
                d.edgeTimingOverrides.Add(new MotionStateDriver.EdgeTimingOverride());
                EditorUtility.SetDirty(d);
            }
            if (d.controller == null || d.controller.stages == null || d.controller.stages.Count == 0)
            {
                EditorGUILayout.HelpBox("Assign a StageController to pick stage indices.", MessageType.None);
            }
        }

        if (d.edgeTimingOverrides == null) return;

        int removeAt = -1;
        for (int i = 0; i < d.edgeTimingOverrides.Count; i++)
        {
            var o = d.edgeTimingOverrides[i];
            if (o == null) { removeAt = i; continue; }

            EditorGUILayout.Space(2);
            using (new EditorGUILayout.VerticalScope("box"))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    o.enabled = EditorGUILayout.ToggleLeft("", o.enabled, GUILayout.Width(18));
                    EditorGUILayout.LabelField($"Edge {i}", EditorStyles.boldLabel);
                    if (GUILayout.Button("×", GUILayout.Width(22))) removeAt = i;
                }

                // From/To pickers
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (d.controller && d.controller.stages != null && d.controller.stages.Count > 0)
                    {
                        int max = d.controller.stages.Count - 1;
                        o.from = Mathf.Clamp(EditorGUILayout.IntField("From Stage", o.from), 0, max);
                        o.to   = Mathf.Clamp(EditorGUILayout.IntField("To Stage",   o.to),   0, max);
                    }
                    else
                    {
                        o.from = EditorGUILayout.IntField("From Stage", o.from);
                        o.to   = EditorGUILayout.IntField("To Stage",   o.to);
                    }
                }

                // Overrides
                o.useDuration = EditorGUILayout.ToggleLeft("Override Duration", o.useDuration);
                if (o.useDuration) o.duration = EditorGUILayout.FloatField("  Duration", o.duration);

                o.useEase = EditorGUILayout.ToggleLeft("Override Ease", o.useEase);
                if (o.useEase) o.ease = (Ease)EditorGUILayout.EnumPopup("  Ease", o.ease);

                o.useDelay = EditorGUILayout.ToggleLeft("Override Delay", o.useDelay);
                if (o.useDelay) o.delay = EditorGUILayout.FloatField("  Delay", o.delay);
            }
        }

        if (removeAt >= 0)
        {
            Undo.RecordObject(d, "Remove Edge Override");
            d.edgeTimingOverrides.RemoveAt(removeAt);
            EditorUtility.SetDirty(d);
        }
    }




    // ---------------------------------- Dynamic Tracks UI -----------------------------------------------
    void DrawDynamicTracks()
    {
        EditorGUILayout.LabelField("Dynamic Tracks (optional)", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("+ Float"))  AddDynTrack<FloatTrack>("Float");
            if (GUILayout.Button("+ Color"))  AddDynTrack<ColorTrack>("Color");
            if (GUILayout.Button("+ Vector3"))AddDynTrack<Vector3Track>("Vector3");
        }

        if (d.controller == null || d.controller.stages == null || d.controller.stages.Count == 0)
        {
            EditorGUILayout.HelpBox("Assign a StageController with stages to edit dynamic track values.", MessageType.Info);
            return;
        }

        int stageCount = d.controller.stages.Count;

        if (d.dynamicTracks == null) d.dynamicTracks = new List<TrackBase>();

        int removeAt = -1;
        for (int i = 0; i < d.dynamicTracks.Count; i++)
        {
            var t = d.dynamicTracks[i];
            if (t == null) { removeAt = i; continue; }

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope("box"))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    t.enabled = EditorGUILayout.ToggleLeft("", t.enabled, GUILayout.Width(18));
                    t.displayName = EditorGUILayout.TextField($"{t.GetType().Name}  •", t.displayName);
                    if (GUILayout.Button("×", GUILayout.Width(24))) removeAt = i;
                }

                t.target = (Component)EditorGUILayout.ObjectField("Target", t.target, typeof(Component), true);
                t.memberName = EditorGUILayout.TextField("Member (property/field)", t.memberName);

                if (t.target && GUILayout.Button("Pick Member…"))
                    ShowDynMemberMenu(t);

                t.BuildAccessors();
                t.EnsureSize(stageCount);

                if (t is FloatTrack ft)
                    DrawDynPerStage(ft.values, stageCount, () => ReadFloat(ft), (idx, v)=> ft.values[idx]=v, v => EditorGUILayout.FloatField($"  Stage {v.idx}", v.value));
                else if (t is ColorTrack ct)
                    DrawDynPerStage(ct.values, stageCount, () => ReadColor(ct), (idx, v)=> ct.values[idx]=v, v => EditorGUILayout.ColorField($"  Stage {v.idx}", v.value));
                else if (t is Vector3Track vt)
                    DrawDynPerStage(vt.values, stageCount, () => ReadVector3(vt), (idx, v)=> vt.values[idx]=v, v => EditorGUILayout.Vector3Field($"  Stage {v.idx}", v.value));
            }
        }
        if (removeAt >= 0)
        {
            Undo.RecordObject(d, "Remove Dynamic Track");
            d.dynamicTracks.RemoveAt(removeAt);
            EditorUtility.SetDirty(d);
        }
    }

    void AddDynTrack<T>(string label) where T : TrackBase, new()
    {
        Undo.RecordObject(d, "Add Dynamic Track " + label);
        if (d.dynamicTracks == null) d.dynamicTracks = new List<TrackBase>();
        d.dynamicTracks.Add(new T(){ displayName = label });
        d.EnsureListSizes();
        EditorUtility.SetDirty(d);
    }

    void DrawDynPerStage<T>(List<T> list, int count, Func<T> captureLive, Action<int,T> setValue, Func<(int idx, T value), T> drawField)
    {
        if (list == null) return;
        while (list.Count < count) list.Add(default);
        for (int i = 0; i < count; i++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var cur = list[i];
                var next = drawField((i, cur));
                if (!Equals(cur, next))
                {
                    Undo.RecordObject(d, "Edit Dynamic Track Stage");
                    setValue(i, next);
                    EditorUtility.SetDirty(d);
                }

                if (GUILayout.Button("Capture", GUILayout.Width(80)))
                {
                    Undo.RecordObject(d, "Capture Dynamic Track Stage");
                    setValue(i, captureLive());
                    EditorUtility.SetDirty(d);
                }
            }
        }
    }

    // Live reflection reads (editor-only)
    float ReadFloat(FloatTrack t)
    {
        if (!t.target || string.IsNullOrEmpty(t.memberName)) return 0f;
        var tp = t.target.GetType();
        var pi = tp.GetProperty(t.memberName, System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
        if (pi != null && pi.PropertyType == typeof(float) && pi.CanRead) return (float)pi.GetValue(t.target);
        var fi = tp.GetField(t.memberName, System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
        if (fi != null && fi.FieldType == typeof(float)) return (float)fi.GetValue(t.target);
        return 0f;
    }
    Color ReadColor(ColorTrack t)
    {
        if (!t.target || string.IsNullOrEmpty(t.memberName)) return Color.white;
        var tp = t.target.GetType();
        var pi = tp.GetProperty(t.memberName, System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
        if (pi != null && pi.PropertyType == typeof(Color) && pi.CanRead) return (Color)pi.GetValue(t.target);
        var fi = tp.GetField(t.memberName, System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
        if (fi != null && fi.FieldType == typeof(Color)) return (Color)fi.GetValue(t.target);
        return Color.white;
    }
    Vector3 ReadVector3(Vector3Track t)
    {
        if (!t.target || string.IsNullOrEmpty(t.memberName)) return Vector3.zero;
        var tp = t.target.GetType();
        var pi = tp.GetProperty(t.memberName, System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
        if (pi != null && pi.PropertyType == typeof(Vector3) && pi.CanRead) return (Vector3)pi.GetValue(t.target);
        var fi = tp.GetField(t.memberName, System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
        if (fi != null && fi.FieldType == typeof(Vector3)) return (Vector3)fi.GetValue(t.target);
        return Vector3.zero;
    }

    void ShowDynMemberMenu(TrackBase t)
    {
        if (!t.target) return;
        var menu = new GenericMenu();
        var tp = t.target.GetType();

        void AddIfType(System.Reflection.MemberInfo mi, Type vtype)
        {
            if (t.ValueType != vtype) return;
            menu.AddItem(new GUIContent(mi.Name), false, () =>
            {
                Undo.RecordObject(d, "Pick Member");
                t.memberName = mi.Name;
                t.BuildAccessors();
                EditorUtility.SetDirty(d);
            });
        }

        foreach (var p in tp.GetProperties(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public))
        {
            if (!p.CanRead || !p.CanWrite) continue;
            AddIfType(p, typeof(float));
            AddIfType(p, typeof(Color));
            AddIfType(p, typeof(Vector3));
        }
        foreach (var f in tp.GetFields(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public))
        {
            AddIfType(f, typeof(float));
            AddIfType(f, typeof(Color));
            AddIfType(f, typeof(Vector3));
        }
        if (menu.GetItemCount() == 0) menu.AddDisabledItem(new GUIContent("No compatible members"));
        menu.ShowAsContext();
    }
    
}
#endif
