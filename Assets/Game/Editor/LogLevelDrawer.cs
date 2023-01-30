using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(Framework.Game.LogLevel))]
public class LogLevelDrawer : PropertyDrawer
{
    public override void OnGUI(
        Rect position, SerializedProperty property, GUIContent label
    )
    {
        Draw(position, property, label);
    }

    public static void Draw(
        Rect position, SerializedProperty property, GUIContent label
    )
    {
        EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
        EditorGUI.BeginChangeCheck();
        int mask = property.intValue;
        mask = EditorGUI.MaskField(
            position, label, mask,
			property.enumDisplayNames
		);
        if (EditorGUI.EndChangeCheck())
        {
			property.intValue = mask;
		}
        EditorGUI.showMixedValue = false;
    }
}