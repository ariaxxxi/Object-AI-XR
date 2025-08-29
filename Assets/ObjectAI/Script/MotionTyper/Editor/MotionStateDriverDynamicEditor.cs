#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Reflection;
using System.Collections.Generic;

[CustomEditor(typeof(MotionStateDriverDynamic))]
public class MotionStateDriverDynamicEditor : Editor
{
    MotionStateDriverDynamic d;

    void OnEnable() => d = (MotionStateDriverDynamic)target;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("controller"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Global Timing (fallback)", EditorStyles.boldLabel);
        d.overrideDuration = EditorGUILayout.Toggle("Override Duration", d.overrideDuration);
        if (d.overrideDuration) d.duration = EditorGUILayout.FloatField("  Duration", d.duration);
        d.overrideEase = EditorGUILayout.Toggle("Override Ease", d.overrideEase);
        if (d.overrideEase) d.ease = (DG.Tweening.Ease)EditorGUILayout.EnumPopup("  Ease", d.ease);
        d.startDelay = EditorGUILayout.FloatField("Start Delay", d.startDelay);

        EditorGUILayout.Space(6);
        DrawTracks();

        serializedObject.ApplyModifiedProperties();
    }

    void DrawTracks()
    {
        EditorGUILayout.LabelField("Tracks (User-defined)", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("+ Float"))  AddTrack<FloatTrack>("Float");
            if (GUILayout.Button("+ Color"))  AddTrack<ColorTrack>("Color");
            if (GUILayout.Button("+ Vector3"))AddTrack<Vector3Track>("Vector3");
        }

        if (d.controller == null || d.controller.stages == null || d.controller.stages.Count == 0)
        {
            EditorGUILayout.HelpBox("Assign a StageController with stages.", MessageType.Info);
            return;
        }
        int stageCount = d.controller.stages.Count;

        int removeAt = -1;
        for (int i = 0; i < d.tracks.Count; i++)
        {
            var t = d.tracks[i];
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
                    ShowMemberMenu(t);

                t.BuildAccessors();
                t.EnsureSize(stageCount);

                if (t is FloatTrack ft)
                    DrawPerStage(ft.values, stageCount, () => ReadFloat(ft), (idx, v)=> ft.values[idx]=v, v => EditorGUILayout.FloatField($"  Stage {v.idx}", v.value));
                else if (t is ColorTrack ct)
                    DrawPerStage(ct.values, stageCount, () => ReadColor(ct), (idx, v)=> ct.values[idx]=v, v => EditorGUILayout.ColorField($"  Stage {v.idx}", v.value));
                else if (t is Vector3Track vt)
                    DrawPerStage(vt.values, stageCount, () => ReadVector3(vt), (idx, v)=> vt.values[idx]=v, v => EditorGUILayout.Vector3Field($"  Stage {v.idx}", v.value));
            }
        }
        if (removeAt >= 0) d.tracks.RemoveAt(removeAt);
    }

    void AddTrack<T>(string label) where T : TrackBase, new()
    {
        Undo.RecordObject(d, "Add Track " + label);
        d.tracks.Add(new T(){ displayName = label });
        d.EnsureListSizes();
        EditorUtility.SetDirty(d);
    }

    void DrawPerStage<T>(List<T> list, int count, Func<T> captureLive, Action<int,T> setValue, Func<(int idx, T value), T> drawField)
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
                    Undo.RecordObject(d, "Edit Track Stage");
                    setValue(i, next);
                    EditorUtility.SetDirty(d);
                }

                if (GUILayout.Button("Capture", GUILayout.Width(80)))
                {
                    Undo.RecordObject(d, "Capture Track Stage");
                    setValue(i, captureLive());
                    EditorUtility.SetDirty(d);
                }
            }
        }
    }

    // Simple live reads via reflection (editor only)
    float ReadFloat(FloatTrack t)
    {
        if (!t.target || string.IsNullOrEmpty(t.memberName)) return 0f;
        var tp = t.target.GetType();
        var pi = tp.GetProperty(t.memberName, BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        if (pi != null && pi.PropertyType == typeof(float) && pi.CanRead) return (float)pi.GetValue(t.target);
        var fi = tp.GetField(t.memberName, BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        if (fi != null && fi.FieldType == typeof(float)) return (float)fi.GetValue(t.target);
        return 0f;
    }
    Color ReadColor(ColorTrack t)
    {
        if (!t.target || string.IsNullOrEmpty(t.memberName)) return Color.white;
        var tp = t.target.GetType();
        var pi = tp.GetProperty(t.memberName, BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        if (pi != null && pi.PropertyType == typeof(Color) && pi.CanRead) return (Color)pi.GetValue(t.target);
        var fi = tp.GetField(t.memberName, BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        if (fi != null && fi.FieldType == typeof(Color)) return (Color)fi.GetValue(t.target);
        return Color.white;
    }
    Vector3 ReadVector3(Vector3Track t)
    {
        if (!t.target || string.IsNullOrEmpty(t.memberName)) return Vector3.zero;
        var tp = t.target.GetType();
        var pi = tp.GetProperty(t.memberName, BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        if (pi != null && pi.PropertyType == typeof(Vector3) && pi.CanRead) return (Vector3)pi.GetValue(t.target);
        var fi = tp.GetField(t.memberName, BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        if (fi != null && fi.FieldType == typeof(Vector3)) return (Vector3)fi.GetValue(t.target);
        return Vector3.zero;
    }

    void ShowMemberMenu(TrackBase t)
    {
        if (!t.target) return;
        var menu = new GenericMenu();
        var tp = t.target.GetType();

        void AddIfType(MemberInfo mi, Type vtype)
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

        foreach (var p in tp.GetProperties(BindingFlags.Instance|BindingFlags.Public))
        {
            if (!p.CanRead || !p.CanWrite) continue;
            AddIfType(p, typeof(float));
            AddIfType(p, typeof(Color));
            AddIfType(p, typeof(Vector3));
        }
        foreach (var f in tp.GetFields(BindingFlags.Instance|BindingFlags.Public))
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
