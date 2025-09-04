#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(StageEdgeRule))]
public class StageEdgeRuleDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        float y = position.y;
        float line = EditorGUIUtility.singleLineHeight;
        float vsp = EditorGUIUtility.standardVerticalSpacing;

        var fromProp = property.FindPropertyRelative("from");
        var toProp = property.FindPropertyRelative("to");
        var useMotion = property.FindPropertyRelative("useMotion");
        var motion = property.FindPropertyRelative("motion");
        var useCustom = property.FindPropertyRelative("useCustomCall");
        var onTraverse = property.FindPropertyRelative("onTraverse");

        Rect r;

        // From/To row
        r = new Rect(position.x, y, position.width, line);
        float half = (r.width - 6f) / 2f;
        Rect rFrom = new Rect(r.x, y, half, line);
        Rect rTo   = new Rect(r.x + half + 6f, y, half, line);
        EditorGUI.PropertyField(rFrom, fromProp);
        EditorGUI.PropertyField(rTo,   toProp);
        y += line + vsp;

        // Toggles row
        r = new Rect(position.x, y, position.width, line);
        float col = r.width / 2f;
        Rect r1 = new Rect(r.x + 0 * col, y, col - 4, line);
        Rect r2 = new Rect(r.x + 1 * col, y, col - 4, line);
        useMotion.boolValue = EditorGUI.ToggleLeft(r1, new GUIContent("Motion"), useMotion.boolValue);
        useCustom.boolValue = EditorGUI.ToggleLeft(r2, new GUIContent("Custom Call"), useCustom.boolValue);
        y += line + vsp;

        // Motion field
        if (useMotion.boolValue)
        {
            r = new Rect(position.x, y, position.width, line);
            EditorGUI.PropertyField(r, motion);
            y += line + vsp;
        }

        // Event field
        if (useCustom.boolValue)
        {
            float h = EditorGUI.GetPropertyHeight(onTraverse, true);
            r = new Rect(position.x, y, position.width, h);
            EditorGUI.PropertyField(r, onTraverse, true);
            y += h + vsp;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float h = 0f;
        float line = EditorGUIUtility.singleLineHeight;
        float vsp = EditorGUIUtility.standardVerticalSpacing;

        var useMotion = property.FindPropertyRelative("useMotion");
        var useCustom = property.FindPropertyRelative("useCustomCall");
        var onTraverse = property.FindPropertyRelative("onTraverse");

        // from/to
        h += line + vsp;
        // toggles
        h += line + vsp;
        if (useMotion.boolValue)
            h += line + vsp;
        if (useCustom.boolValue)
            h += EditorGUI.GetPropertyHeight(onTraverse, true) + vsp;

        return h;
    }
}
#endif

