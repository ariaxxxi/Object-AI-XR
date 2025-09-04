#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(StageDef))]
public class StageDefDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Draw without the default foldout wrapper; mimic array element header
        EditorGUI.BeginProperty(position, label, property);

        float y = position.y;
        float line = EditorGUIUtility.singleLineHeight;
        float vsp = EditorGUIUtility.standardVerticalSpacing;
        Rect r;

        var idProp = property.FindPropertyRelative("id");
        var sliderFlag = property.FindPropertyRelative("triggerBySlider");
        var rangeMin = property.FindPropertyRelative("rangeMin");
        var rangeMax = property.FindPropertyRelative("rangeMax");
        var keyFlag = property.FindPropertyRelative("triggerByKey");
        var key = property.FindPropertyRelative("key");
        var manualFlag = property.FindPropertyRelative("triggerByManual");
        var onManual = property.FindPropertyRelative("onManualTrigger");

        // ID
        r = new Rect(position.x, y, position.width, line);
        EditorGUI.PropertyField(r, idProp);
        y += line + vsp;

        // Title before toggles
        r = new Rect(position.x, y, position.width, line);
        EditorGUI.LabelField(r, new GUIContent("Stage Trigger"), EditorStyles.boldLabel);
        y += line + vsp;

        // Toggle row
        r = new Rect(position.x, y, position.width, line);
        float col = r.width / 3f;
        Rect r1 = new Rect(r.x + 0 * col, y, col - 4, line);
        Rect r2 = new Rect(r.x + 1 * col, y, col - 4, line);
        Rect r3 = new Rect(r.x + 2 * col, y, col - 4, line);
        sliderFlag.boolValue = EditorGUI.ToggleLeft(r1, new GUIContent("Slider"), sliderFlag.boolValue);
        keyFlag.boolValue    = EditorGUI.ToggleLeft(r2, new GUIContent("Key"),    keyFlag.boolValue);
        manualFlag.boolValue = EditorGUI.ToggleLeft(r3, new GUIContent("Manual"), manualFlag.boolValue);
        y += line + vsp;

        // Slider fields
        if (sliderFlag.boolValue)
        {
            r = new Rect(position.x, y, position.width, line);
            EditorGUI.BeginChangeCheck();
            float min = rangeMin.floatValue;
            float max = rangeMax.floatValue;
            // Draw as two fields side by side
            float half = (r.width - 6f) / 2f;
            Rect rMin = new Rect(r.x, y, half, line);
            Rect rMax = new Rect(r.x + half + 6f, y, half, line);
            EditorGUI.PropertyField(rMin, rangeMin, new GUIContent("Range Min"));
            EditorGUI.PropertyField(rMax, rangeMax, new GUIContent("Range Max"));
            if (EditorGUI.EndChangeCheck())
            {
                // keep min <= max
                if (rangeMin.floatValue > rangeMax.floatValue)
                    rangeMax.floatValue = rangeMin.floatValue;
            }
            y += line + vsp;
        }

        // Key field
        if (keyFlag.boolValue)
        {
            r = new Rect(position.x, y, position.width, line);
            EditorGUI.PropertyField(r, key, new GUIContent("Key"));
            y += line + vsp;
        }

        // Manual event
        if (manualFlag.boolValue)
        {
            float h = EditorGUI.GetPropertyHeight(onManual, true);
            r = new Rect(position.x, y, position.width, h);
            EditorGUI.PropertyField(r, onManual, new GUIContent("On Manual Trigger"), true);
            y += h + vsp;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float h = 0f;
        float line = EditorGUIUtility.singleLineHeight;
        float vsp = EditorGUIUtility.standardVerticalSpacing;

        var sliderFlag = property.FindPropertyRelative("triggerBySlider");
        var keyFlag = property.FindPropertyRelative("triggerByKey");
        var manualFlag = property.FindPropertyRelative("triggerByManual");
        var onManual = property.FindPropertyRelative("onManualTrigger");

        // id
        h += line + vsp;
        // title
        h += line + vsp;
        // toggles row
        h += line + vsp;

        if (sliderFlag.boolValue)
        {
            h += line + vsp; // range min/max row
        }
        if (keyFlag.boolValue)
        {
            h += line + vsp; // key row
        }
        if (manualFlag.boolValue)
        {
            h += EditorGUI.GetPropertyHeight(onManual, true) + vsp; // event block
        }

        return h;
    }
}
#endif
