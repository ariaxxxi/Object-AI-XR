#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(TwoStageScrollController.TopBlockItemConfig))]
public class TwoStageScrollControllerEditor : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        var foldout = EditorGUI.Foldout(
            new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight),
            property.isExpanded,
            label,
            true);
        property.isExpanded = foldout;
        if (foldout)
        {
            EditorGUI.indentLevel++;
            DrawFields(property, position);
            EditorGUI.indentLevel--;
        }
        EditorGUI.EndProperty();
    }

    private void DrawFields(SerializedProperty property, Rect position)
    {
        float y = position.y + EditorGUIUtility.singleLineHeight + 2f;
        float lineHeight = EditorGUIUtility.singleLineHeight;
        float width = position.width;

        y = DrawProp(property.FindPropertyRelative("Item"), position.x, y, width, lineHeight);
        y = DrawProp(property.FindPropertyRelative("Interaction"), position.x, y, width, lineHeight);

        var interaction = (TwoStageScrollController.InteractionType)property.FindPropertyRelative("Interaction").enumValueIndex;
        switch (interaction)
        {
            case TwoStageScrollController.InteractionType.AppLauncher:
                y = DrawProp(property.FindPropertyRelative("AppLogo"), position.x, y, width, lineHeight);
                y = DrawProp(property.FindPropertyRelative("AppScreen"), position.x, y, width, lineHeight);
                y = DrawProp(property.FindPropertyRelative("AppPill"), position.x, y, width, lineHeight);
                y = DrawProp(property.FindPropertyRelative("DefaultText"), position.x, y, width, lineHeight);
                break;
            case TwoStageScrollController.InteractionType.Expandable:
                y = DrawProp(property.FindPropertyRelative("Detail"), position.x, y, width, lineHeight);
                break;
            case TwoStageScrollController.InteractionType.QuickAction:
                y = DrawProp(property.FindPropertyRelative("QuickAction"), position.x, y, width, lineHeight);
                break;
        }
    }

    private float DrawProp(SerializedProperty prop, float x, float y, float width, float height)
    {
        var rect = new Rect(x, y, width, height);
        EditorGUI.PropertyField(rect, prop, true);
        return y + height + 2f;
    }
}

#endif
