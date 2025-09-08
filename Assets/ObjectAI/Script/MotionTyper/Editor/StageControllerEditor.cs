#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using DG.Tweening;


[CustomEditor(typeof(StageController))]
public class StageControllerEditor : Editor
{
    SerializedProperty stagesProp;
    SerializedProperty edgesProp;
    SerializedProperty durationProp;
    SerializedProperty easeProp;
    SerializedProperty overshootProp;
    SerializedProperty motionLibraryProp;
    SerializedProperty sliderInputProp;
    SerializedProperty distanceTargetProp;
    SerializedProperty distanceCameraProp;
    SerializedProperty logDistanceProp;

    void OnEnable()
    {
        stagesProp        = serializedObject.FindProperty("stages");
        edgesProp         = serializedObject.FindProperty("edges");
        durationProp      = serializedObject.FindProperty("duration");
        easeProp          = serializedObject.FindProperty("ease");
        overshootProp     = serializedObject.FindProperty("overshoot");
        motionLibraryProp = serializedObject.FindProperty("motionLibrary");
        sliderInputProp   = serializedObject.FindProperty("sliderInput");
        distanceTargetProp = serializedObject.FindProperty("distanceTarget");
        distanceCameraProp = serializedObject.FindProperty("distanceCamera");
        logDistanceProp = serializedObject.FindProperty("logDistance");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();



        // Preview controls
        var controller = (StageController)target;
        EditorGUILayout.LabelField("Stage Preview", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope("box"))
        {
            int count = controller.stages != null ? controller.stages.Count : 0;
            if (count == 0)
            {
                EditorGUILayout.LabelField("No stages defined.");
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    var s = controller.stages[i];
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        // Stage label
                        GUILayout.Label($"{i}: {s.id}  [{s.rangeMin:0.##}–{s.rangeMax:0.##}]", GUILayout.MinWidth(160));

                        // Snap (works in edit or play)
                        if (GUILayout.Button("Snap", GUILayout.Width(70)))
                        {
                            controller.PreviewInstant(i);
                            // repaint scene view so you see changes immediately
                            EditorApplication.QueuePlayerLoopUpdate();
                            SceneView.RepaintAll();
                        }

                        // Play (only in play mode)
                        EditorGUI.BeginDisabledGroup(!EditorApplication.isPlaying);
                        if (GUILayout.Button("Play", GUILayout.Width(70)))
                        {
                            controller.RequestStageIndex(i); // runs motions, edges, events
                        }
                        EditorGUI.EndDisabledGroup();
                    }
                }
            }
        }

        // Draw the default fields
        EditorGUILayout.PropertyField(motionLibraryProp);
        EditorGUILayout.PropertyField(sliderInputProp);

        // Distance input (global)
        EditorGUILayout.LabelField("Distance Input (optional)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(distanceTargetProp, new GUIContent("Distance Target"));
        EditorGUILayout.PropertyField(distanceCameraProp, new GUIContent("Distance Camera (optional)"));
        EditorGUILayout.PropertyField(logDistanceProp, new GUIContent("Log Distance (debug)"));

        // Warn if any stage uses Slider but no slider assigned
        bool anySlider = false;
        bool anyDistance = false;
        if (controller.stages != null)
        {
            for (int i = 0; i < controller.stages.Count; i++)
            {
                var s = controller.stages[i];
                if (s != null)
                {
                    if (s.triggerBySlider) anySlider = true;
                    if (s.triggerByDistance) anyDistance = true;
                }
            }
        }
        if (anySlider && sliderInputProp.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox("One or more stages use Slider trigger, but no Slider is assigned.", MessageType.Warning);
        }
        if (anyDistance && distanceTargetProp.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox("One or more stages use Distance trigger, but no Distance Target is assigned.", MessageType.Warning);
        }
        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(stagesProp, includeChildren: true);
        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(edgesProp, includeChildren: true);
        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(durationProp);
        EditorGUILayout.PropertyField(easeProp);
        
        EditorGUI.BeginDisabledGroup(easeProp.enumValueIndex != (int)Ease.InOutBack);
        EditorGUILayout.PropertyField(overshootProp);
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.Space();





        serializedObject.ApplyModifiedProperties();
    }
}
#endif
