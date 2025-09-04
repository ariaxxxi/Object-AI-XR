#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using DG.Tweening;

[CustomEditor(typeof(MotionStateDriver))]
public class MotionStateDriverEditor : Editor
{
    MotionStateDriver d;

    void OnEnable() => d = (MotionStateDriver)target;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("controller"));

        // Targets are auto-bound on the component; no manual exposure in inspector.

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Global Timing (fallback)", EditorStyles.boldLabel);
        d.overrideDuration = EditorGUILayout.Toggle("Override Duration", d.overrideDuration);
        if (d.overrideDuration) d.duration = EditorGUILayout.FloatField("  Duration", d.duration);
        d.overrideEase = EditorGUILayout.Toggle("Override Ease", d.overrideEase);
        if (d.overrideEase) d.ease = (Ease)EditorGUILayout.EnumPopup("  Ease", d.ease);
        d.startDelay = EditorGUILayout.FloatField("Start Delay", d.startDelay);

        DrawEdgeTimingOverrides();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Track Toggles (Essentials)", EditorStyles.boldLabel);
        d.useAnchoredPosition = EditorGUILayout.ToggleLeft("Anchored Position (RectTransform)", d.useAnchoredPosition);
        d.useLocalPosition    = EditorGUILayout.ToggleLeft("Local Position (Transform)", d.useLocalPosition);
        d.useEuler            = EditorGUILayout.ToggleLeft("Local Rotation (Euler)", d.useEuler);
        d.useUniformScale     = EditorGUILayout.ToggleLeft("Uniform Scale", d.useUniformScale);
        d.useSizeDelta        = EditorGUILayout.ToggleLeft("Size Delta (RectTransform)", d.useSizeDelta);
        d.useAlpha            = EditorGUILayout.ToggleLeft("CanvasGroup Alpha", d.useAlpha);

        // Removed: explicit sync button. Lists auto-size when needed.

        // Stage-dependent lists UI (only shown if StageController present)
        if (d.controller == null || d.controller.stages == null || d.controller.stages.Count == 0)
        {
            EditorGUILayout.HelpBox("Assign a StageController with stages to edit per-stage values.", MessageType.Info);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        int count = d.controller.stages.Count;

        // Draw per-stage values for enabled tracks
        if (d.useAnchoredPosition)
            DrawTrack("Anchored Position", count,
                i => SafeGet(d.anchoredPosPerStage, i, d.rt ? d.rt.anchoredPosition : Vector2.zero),
                (i, v) => SafeSet(d.anchoredPosPerStage, i, v, d.rt ? d.rt.anchoredPosition : Vector2.zero, count),
                (i, v) => EditorGUILayout.Vector2Field($"  Stage {i}", v),
                () => d.rt ? d.rt.anchoredPosition : Vector2.zero
            );

        if (d.useLocalPosition)
            DrawTrack("Local Position", count,
                i => SafeGet(d.localPosPerStage, i, d.rt ? d.rt.localPosition : Vector3.zero),
                (i, v) => SafeSet(d.localPosPerStage, i, v, d.rt ? d.rt.localPosition : Vector3.zero, count),
                (i, v) => EditorGUILayout.Vector3Field($"  Stage {i}", v),
                () => d.rt ? d.rt.localPosition : Vector3.zero
            );

        if (d.useEuler)
            DrawTrack("Local Euler", count,
                i => SafeGet(d.eulerPerStage, i, d.rt ? d.rt.localEulerAngles : Vector3.zero),
                (i, v) => SafeSet(d.eulerPerStage, i, v, d.rt ? d.rt.localEulerAngles : Vector3.zero, count),
                (i, v) => EditorGUILayout.Vector3Field($"  Stage {i}", v),
                () => d.rt ? d.rt.localEulerAngles : Vector3.zero
            );

        if (d.useUniformScale)
            DrawTrack("Uniform Scale", count,
                i => SafeGet(d.uniformScalePerStage, i, d.rt ? d.rt.localScale.x : 1f),
                (i, v) => SafeSet(d.uniformScalePerStage, i, v, d.rt ? d.rt.localScale.x : 1f, count),
                (i, v) => EditorGUILayout.FloatField($"  Stage {i}", v),
                () => d.rt ? d.rt.localScale.x : 1f
            );

        if (d.useSizeDelta)
            DrawTrack("Size Delta", count,
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

        serializedObject.ApplyModifiedProperties();
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

    
}
#endif
