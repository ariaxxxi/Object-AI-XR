#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(StageController))]
public class StageControllerEditor : Editor
{
    SerializedProperty stagesProp;
    SerializedProperty edgesProp;
    SerializedProperty durationProp;
    SerializedProperty easeProp;
    SerializedProperty motionPlayerProp;

    void OnEnable()
    {
        stagesProp        = serializedObject.FindProperty("stages");
        edgesProp         = serializedObject.FindProperty("edges");
        durationProp      = serializedObject.FindProperty("duration");
        easeProp          = serializedObject.FindProperty("ease");
        motionPlayerProp  = serializedObject.FindProperty("motionPlayer");
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
        EditorGUILayout.PropertyField(motionPlayerProp);
        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(stagesProp, includeChildren: true);
        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(edgesProp, includeChildren: true);
        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(durationProp);
        EditorGUILayout.PropertyField(easeProp);
        EditorGUILayout.Space();





        serializedObject.ApplyModifiedProperties();
    }
}
#endif
