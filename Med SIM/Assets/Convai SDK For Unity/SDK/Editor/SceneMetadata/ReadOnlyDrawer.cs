using Convai.Runtime.SceneMetadata;
using UnityEditor;
using UnityEngine;

namespace Convai.Editor.SceneMetadata
{
    /// <summary>
    ///     PropertyDrawer for the ReadOnlyAttribute.
    ///     Renders properties as read-only in the Unity Inspector.
    /// </summary>
    [CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
    public class ReadOnlyDrawer : PropertyDrawer
    {
        /// <inheritdoc />
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            GUI.enabled = false;
            EditorGUI.PropertyField(position, property, label, true);
            GUI.enabled = true;
        }
    }
}
