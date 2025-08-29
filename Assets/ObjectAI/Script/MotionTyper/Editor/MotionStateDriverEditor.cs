#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MotionStateDriver))]
public class MotionStateDriverEditor : Editor
{
    MotionStateDriver d;

    void OnEnable() => d = (MotionStateDriver)target;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("controller"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Targets", EditorStyles.boldLabel);
        d.rt = (RectTransform)EditorGUILayout.ObjectField("RectTransform", d.rt, typeof(RectTransform), true);
        d.cg = (CanvasGroup)EditorGUILayout.ObjectField("CanvasGroup", d.cg, typeof(CanvasGroup), true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Global Timing (fallback)", EditorStyles.boldLabel);
        d.overrideDuration = EditorGUILayout.Toggle("Override Duration", d.overrideDuration);
        if (d.overrideDuration) d.duration = EditorGUILayout.FloatField("  Duration", d.duration);
        d.overrideEase = EditorGUILayout.Toggle("Override Ease", d.overrideEase);
        if (d.overrideEase) d.ease = (DG.Tweening.Ease)EditorGUILayout.EnumPopup("  Ease", d.ease);
        d.startDelay = EditorGUILayout.FloatField("Start Delay", d.startDelay);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Track Toggles (Essentials)", EditorStyles.boldLabel);
        d.useAnchoredPosition = EditorGUILayout.ToggleLeft("Anchored Position (RectTransform)", d.useAnchoredPosition);
        d.useLocalPosition    = EditorGUILayout.ToggleLeft("Local Position (Transform)", d.useLocalPosition);
        d.useEuler            = EditorGUILayout.ToggleLeft("Local Rotation (Euler)", d.useEuler);
        d.useUniformScale     = EditorGUILayout.ToggleLeft("Uniform Scale", d.useUniformScale);
        d.useSizeDelta        = EditorGUILayout.ToggleLeft("Size Delta (RectTransform)", d.useSizeDelta);
        d.useAlpha            = EditorGUILayout.ToggleLeft("CanvasGroup Alpha", d.useAlpha);

        if (GUILayout.Button("Sync Lists To Stage Count"))
        {
            Undo.RecordObject(d, "Sync Lists");
            d.EnsureListSizes();
            EditorUtility.SetDirty(d);
        }

        if (d.controller == null || d.controller.stages == null || d.controller.stages.Count == 0)
        {
            EditorGUILayout.HelpBox("Assign a StageController with stages to edit per-stage values.", MessageType.Info);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        int count = d.controller.stages.Count;

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Capture All (Current Stage)"))
            {
                int idx = Mathf.Clamp(d.controller.CurrentIndex, 0, count - 1);
                Undo.RecordObject(d, "Capture All (Current Stage)");
                CaptureAll(d, idx);
                EditorUtility.SetDirty(d);
            }
            if (GUILayout.Button("Apply Instant (Current Stage)"))
            {
                int idx = Mathf.Clamp(d.controller.CurrentIndex, 0, count - 1);
                d.ApplyInstant(idx);
                EditorUtility.SetDirty(d);
                SceneView.RepaintAll();
            }
        }

        // Draw only toggled tracks
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

    // Generic per-track drawer
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

    static void CaptureAll(MotionStateDriver d, int idx)
    {
        d.EnsureListSizes();

        if (d.rt)
        {
            if (d.useAnchoredPosition)
                d.anchoredPosPerStage[idx] = d.rt.anchoredPosition;
            if (d.useLocalPosition)
                d.localPosPerStage[idx] = d.rt.localPosition;
            if (d.useEuler)
                d.eulerPerStage[idx] = d.rt.localEulerAngles;
            if (d.useSizeDelta)
                d.sizePerStage[idx] = d.rt.sizeDelta;
            if (d.useUniformScale)
                d.uniformScalePerStage[idx] = d.rt.localScale.x;
        }
        if (d.useAlpha && d.cg)
            d.alphaPerStage[idx] = d.cg.alpha;
    }
}
#endif
