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
        var touchFlag = property.FindPropertyRelative("triggerByTouch");
        var touchOnBeginOnly = property.FindPropertyRelative("touchOnBeginOnly");
        var buttonFlag = property.FindPropertyRelative("triggerByButton");
        var button = property.FindPropertyRelative("button");
        var distFlag = property.FindPropertyRelative("triggerByDistance");
        var distanceThreshold = property.FindPropertyRelative("distanceThreshold");
        var customFlag = property.FindPropertyRelative("triggerByCustom");
        var onCustom = property.FindPropertyRelative("onCustomTrigger");
        var stageFlag = property.FindPropertyRelative("triggerByStage");
        var triggerStageIndex = property.FindPropertyRelative("triggerStageIndex");
        var triggerStageDelay = property.FindPropertyRelative("triggerStageDelay");

        // ID
        r = new Rect(position.x, y, position.width, line);
        EditorGUI.PropertyField(r, idProp);
        y += line + vsp;

        // Title before toggles
        r = new Rect(position.x, y, position.width, line);
        EditorGUI.LabelField(r, new GUIContent("Stage Trigger"), EditorStyles.boldLabel);
        y += line + vsp;

        // Toggle row 1
        r = new Rect(position.x, y, position.width, line);
        float col = r.width / 4f;
        Rect r1 = new Rect(r.x + 0 * col, y, col - 4, line);
        Rect r2 = new Rect(r.x + 1 * col, y, col - 4, line);
        Rect r3 = new Rect(r.x + 2 * col, y, col - 4, line);
        Rect r4 = new Rect(r.x + 3 * col, y, col - 4, line);
        sliderFlag.boolValue = EditorGUI.ToggleLeft(r1, new GUIContent("Slider"), sliderFlag.boolValue);
        keyFlag.boolValue    = EditorGUI.ToggleLeft(r2, new GUIContent("Key"),    keyFlag.boolValue);
        stageFlag.boolValue  = EditorGUI.ToggleLeft(r3, new GUIContent("Stage"),  stageFlag.boolValue);
        customFlag.boolValue = EditorGUI.ToggleLeft(r4, new GUIContent("Custom"), customFlag.boolValue);
        y += line + vsp;

        // Toggle row 2
        r = new Rect(position.x, y, position.width, line);
        float col2 = r.width / 3f;
        Rect r21 = new Rect(r.x + 0 * col2, y, col2 - 4, line);
        Rect r22 = new Rect(r.x + 1 * col2, y, col2 - 4, line);
        Rect r23 = new Rect(r.x + 2 * col2, y, col2 - 4, line);
        touchFlag.boolValue  = EditorGUI.ToggleLeft(r21, new GUIContent("Touch"),  touchFlag.boolValue);
        buttonFlag.boolValue = EditorGUI.ToggleLeft(r22, new GUIContent("Button"), buttonFlag.boolValue);
        distFlag.boolValue   = EditorGUI.ToggleLeft(r23, new GUIContent("Distance"), distFlag.boolValue);
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

        // Touch fields
        if (touchFlag.boolValue)
        {
            r = new Rect(position.x, y, position.width, line);
            EditorGUI.PropertyField(r, touchOnBeginOnly, new GUIContent("Touch On Begin Only"));
            y += line + vsp;
        }

        // Button field
        if (buttonFlag.boolValue)
        {
            r = new Rect(position.x, y, position.width, line);
            EditorGUI.PropertyField(r, button, new GUIContent("Button"));
            y += line + vsp;

        }

        // Distance fields
        if (distFlag.boolValue)
        {
            r = new Rect(position.x, y, position.width, line);
            EditorGUI.PropertyField(r, distanceThreshold, new GUIContent("Threshold"));
            y += line + vsp;

            // Closer-than behavior is default; no extra toggle
        }

        // Custom event
        if (customFlag.boolValue)
        {
            float h = EditorGUI.GetPropertyHeight(onCustom, true);
            r = new Rect(position.x, y, position.width, h);
            EditorGUI.PropertyField(r, onCustom, new GUIContent("On Custom Trigger"), true);
            y += h + vsp;
        }

        // Stage trigger fields
        if (stageFlag.boolValue)
        {
            r = new Rect(position.x, y, position.width, line);
            EditorGUI.PropertyField(r, triggerStageIndex, new GUIContent("Trigger Stage Index"));
            y += line + vsp;

            r = new Rect(position.x, y, position.width, line);
            EditorGUI.PropertyField(r, triggerStageDelay, new GUIContent("Trigger Stage Delay (s)"));
            y += line + vsp;
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
        var touchFlag = property.FindPropertyRelative("triggerByTouch");
        var buttonFlag = property.FindPropertyRelative("triggerByButton");
        var distFlag = property.FindPropertyRelative("triggerByDistance");
        var customFlag = property.FindPropertyRelative("triggerByCustom");
        var onCustom = property.FindPropertyRelative("onCustomTrigger");
        var stageFlag = property.FindPropertyRelative("triggerByStage");

        // id
        h += line + vsp;
        // title
        h += line + vsp;
        // toggles rows
        h += line + vsp; // row 1
        h += line + vsp; // row 2

        if (sliderFlag.boolValue)
        {
            h += line + vsp; // range min/max row
        }
        if (keyFlag.boolValue) h += line + vsp; // key row
        if (touchFlag.boolValue) h += line + vsp; // touch row
        if (buttonFlag.boolValue) h += line + vsp; // button
        if (distFlag.boolValue)
        {
            h += line + vsp; // threshold
        }
        if (customFlag.boolValue)
        {
            h += EditorGUI.GetPropertyHeight(onCustom, true) + vsp; // event block
        }
        if (stageFlag.boolValue)
        {
            h += line + vsp; // triggerStageId row
            h += line + vsp; // triggerStageDelay row
        }

        return h;
    }
}
#endif
