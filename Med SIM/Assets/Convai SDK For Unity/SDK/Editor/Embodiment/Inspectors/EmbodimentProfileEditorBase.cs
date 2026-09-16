using Convai.Editor.Inspectors;
using UnityEditor;
using UnityEngine;

namespace Convai.Editor.Embodiment.Inspectors
{
    internal abstract class ConvaiEmbodimentProfileEditorBase<TProfile> : ConvaiPremiumInspectorEditor
        where TProfile : UnityEngine.Object
    {
        protected TProfile Profile => (TProfile)target;

        protected virtual string HeaderSubtitle => "Embodiment Profile";
        protected virtual string HeaderStatus => "Asset";
        protected virtual Color HeaderStatusColor => AccentEmphasis;

        public override void OnInspectorGUI()
        {
            EnsurePremiumStyles();
            serializedObject.Update();

            DrawPremiumHeader(HeaderTitle, HeaderSubtitle, HeaderStatus, HeaderStatusColor);
            DrawProfileInspector();

            serializedObject.ApplyModifiedProperties();
        }

        protected abstract string HeaderTitle { get; }
        protected abstract void DrawProfileInspector();

        protected bool LoadSectionState(string sectionId, bool defaultValue) =>
            ConvaiInspectorSectionStateStore.Get(EditorStateHostId, sectionId, defaultValue);

        protected void SaveSectionState(string sectionId, bool value) =>
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, sectionId, value);

        protected bool DrawProfileSection(
            string sectionId,
            string title,
            bool expanded,
            string icon = ConvaiInspectorIconIds.Profile,
            Color? color = null)
        {
            return DrawSection(sectionId, title, expanded, icon, color);
        }

        protected void DrawProperties(params SerializedProperty[] properties)
        {
            if (properties == null) return;

            for (int i = 0; i < properties.Length; i++)
            {
                SerializedProperty property = properties[i];
                if (property != null)
                    EditorGUILayout.PropertyField(property, true);
            }
        }

        protected void DrawProperty(string propertyName, string label = null, bool includeChildren = true)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                DrawWarningBox("Missing Serialized Field", $"{typeof(TProfile).Name}.{propertyName} was not found.");
                return;
            }

            if (string.IsNullOrWhiteSpace(label))
                EditorGUILayout.PropertyField(property, includeChildren);
            else
                EditorGUILayout.PropertyField(property, new GUIContent(label), includeChildren);
        }

        protected SerializedProperty Find(string propertyName) => serializedObject.FindProperty(propertyName);

        protected int ArraySize(string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            return property != null && property.isArray ? property.arraySize : 0;
        }

        protected static string CountLabel(int count, string singular, string plural = null) =>
            count == 1 ? $"1 {singular}" : $"{count} {plural ?? singular + "s"}";
    }
}
