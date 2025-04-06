using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Attribute that makes a field read-only in the inspector.
/// </summary>
public class ReadOnlyAttribute : PropertyAttribute { }

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
public class ReadOnlyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Save previous GUI state
        var previousGUIState = GUI.enabled;

        // Disable the GUI
        GUI.enabled = false;

        // Draw the property
        EditorGUI.PropertyField(position, property, label);

        // Restore GUI state
        GUI.enabled = previousGUIState;
    }
}
#endif