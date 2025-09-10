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
        var overrideDuration = property.FindPropertyRelative("overrideDuration");
        var duration = property.FindPropertyRelative("duration");
        var overrideEase = property.FindPropertyRelative("overrideEase");
        var ease = property.FindPropertyRelative("ease");
        var useDelay = property.FindPropertyRelative("useDelay");
        var delay = property.FindPropertyRelative("delay");
        var useAEEase = property.FindPropertyRelative("useAEEase");
        var aeStartSpeed = property.FindPropertyRelative("aeStartSpeed");
        var aeStartInfluence = property.FindPropertyRelative("aeStartInfluence");
        var aeEndSpeed = property.FindPropertyRelative("aeEndSpeed");
        var aeEndInfluence = property.FindPropertyRelative("aeEndInfluence");
        var aeTangentScale = property.FindPropertyRelative("aeTangentScale");

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

        // Per-stage timing overrides
        EditorGUI.LabelField(new Rect(position.x, y, position.width, line), new GUIContent("Timing Overrides (on enter)"), EditorStyles.boldLabel);
        y += line + vsp;

        r = new Rect(position.x, y, position.width, line);
        overrideDuration.boolValue = EditorGUI.ToggleLeft(r, new GUIContent("Override Duration"), overrideDuration.boolValue);
        y += line + vsp;
        if (overrideDuration.boolValue)
        {
            r = new Rect(position.x + 12, y, position.width - 12, line);
            EditorGUI.PropertyField(r, duration, new GUIContent("Duration"));
            y += line + vsp;
        }

        r = new Rect(position.x, y, position.width, line);
        overrideEase.boolValue = EditorGUI.ToggleLeft(r, new GUIContent("Override Ease"), overrideEase.boolValue);
        y += line + vsp;
        if (overrideEase.boolValue)
        {
            r = new Rect(position.x + 12, y, position.width - 12, line);
            EditorGUI.PropertyField(r, ease, new GUIContent("Ease"));
            y += line + vsp;
        }

        r = new Rect(position.x, y, position.width, line);
        useDelay.boolValue = EditorGUI.ToggleLeft(r, new GUIContent("Use Delay"), useDelay.boolValue);
        y += line + vsp;
        if (useDelay.boolValue)
        {
            r = new Rect(position.x + 12, y, position.width - 12, line);
            EditorGUI.PropertyField(r, delay, new GUIContent("Delay (s)"));
            y += line + vsp;
        }

        // AE-style ease override
        r = new Rect(position.x, y, position.width, line);
        useAEEase.boolValue = EditorGUI.ToggleLeft(r, new GUIContent("Use AE-style Ease"), useAEEase.boolValue);
        y += line + vsp;
        if (useAEEase.boolValue)
        {
            r = new Rect(position.x + 12, y, position.width - 12, line);
            EditorGUI.PropertyField(r, aeStartSpeed, new GUIContent("Start Speed"));
            y += line + vsp;
            r = new Rect(position.x + 12, y, position.width - 12, line);
            EditorGUI.PropertyField(r, aeStartInfluence, new GUIContent("Start Influence (%)"));
            y += line + vsp;
            r = new Rect(position.x + 12, y, position.width - 12, line);
            EditorGUI.PropertyField(r, aeEndSpeed, new GUIContent("End Speed"));
            y += line + vsp;
            r = new Rect(position.x + 12, y, position.width - 12, line);
            EditorGUI.PropertyField(r, aeEndInfluence, new GUIContent("End Influence (%)"));
            y += line + vsp;
            r = new Rect(position.x + 12, y, position.width - 12, line);
            EditorGUI.PropertyField(r, aeTangentScale, new GUIContent("Tangent Scale"));
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
        var overrideDuration = property.FindPropertyRelative("overrideDuration");
        var overrideEase = property.FindPropertyRelative("overrideEase");
        var useDelay = property.FindPropertyRelative("useDelay");
        var useAEEase = property.FindPropertyRelative("useAEEase");

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

        // Timing overrides block header
        h += line + vsp;
        // Duration override toggle + maybe field
        h += line + vsp;
        if (overrideDuration.boolValue) h += line + vsp;
        // Ease override toggle + maybe field
        h += line + vsp;
        if (overrideEase.boolValue) h += line + vsp;
        // Delay toggle + maybe field
        h += line + vsp;
        if (useDelay.boolValue) h += line + vsp;

        // AE-style ease override toggle and fields
        h += line + vsp; // toggle
        if (useAEEase.boolValue)
        {
            h += line + vsp; // start speed
            h += line + vsp; // start influence
            h += line + vsp; // end speed
            h += line + vsp; // end influence
            h += line + vsp; // tangent scale
        }

        return h;
    }
}
#endif
