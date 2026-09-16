#if UNITY_EDITOR
using System;
using Convai.Domain.Models.LipSync;
using Convai.Editor.Inspectors;
using Convai.Editor.UI;
using Convai.Modules.LipSync.Profiles;
using UnityEditor;
using UnityEngine;

namespace Convai.Modules.LipSync.Editor
{
    [CustomEditor(typeof(ConvaiLipSyncProfile))]
    internal sealed class ConvaiLipSyncProfileEditor : UnityEditor.Editor
    {
        private const float CompactLabelWidth = 170f;
        private static readonly Color ConvaiGreenLight = ConvaiInspectorThemeTokens.AccentEmphasis;
        private static readonly Color ConvaiInfo = ConvaiInspectorThemeTokens.Info;
        private static readonly Color HeaderBg = ConvaiInspectorThemeTokens.HeaderBackground;
        private GUIStyle _captionStyle;

        private Texture2D _convaiIcon;
        private SerializedProperty _displayNameProp;

        private GUIStyle _headerStyle;

        private SerializedProperty _profileIdProp;
        private GUIStyle _sectionHeaderStyle;
        private bool _stylesInitialized;
        private SerializedProperty _transportFormatProp;
        private bool _transportStateInitialized;

        private bool _useCustomTransportToken;

        private void OnEnable()
        {
            _profileIdProp = serializedObject.FindProperty("_profileId");
            _displayNameProp = serializedObject.FindProperty("_displayName");
            _transportFormatProp = serializedObject.FindProperty("_transportFormat");

            _convaiIcon = ConvaiBrandedIconProvider.GetConvaiIcon();
            InitializeTransportStateFromSerialized();
        }

        public override void OnInspectorGUI()
        {
            InitializeStyles();
            serializedObject.Update();

            if (_profileIdProp == null || _displayNameProp == null || _transportFormatProp == null)
            {
                DrawDefaultInspector();
                return;
            }

            var asset = (ConvaiLipSyncProfile)target;
            DrawInspectorHeader(asset);

            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = CompactLabelWidth;
            try
            {
                DrawIdentityCard();
                DrawDisplayCard();
                DrawTransportCard();
                DrawValidationMessages();
            }
            finally
            {
                EditorGUIUtility.labelWidth = previousLabelWidth;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized &&
                _headerStyle != null &&
                _sectionHeaderStyle != null &&
                _captionStyle != null)
                return;

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14, alignment = TextAnchor.MiddleLeft, normal = { textColor = ConvaiGreenLight }
            };

            _sectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                normal = { textColor = EditorGUIUtility.isProSkin ? Color.white : new Color(0.1f, 0.1f, 0.1f) }
            };

            _captionStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                wordWrap = true,
                normal =
                {
                    textColor = EditorGUIUtility.isProSkin
                        ? new Color(0.74f, 0.74f, 0.74f)
                        : new Color(0.35f, 0.35f, 0.35f)
                }
            };

            _stylesInitialized = true;
        }

        private void DrawInspectorHeader(ConvaiLipSyncProfile asset)
        {
            const float headerHeight = 58f;
            const float iconSize = 22f;
            const float iconTextSpacing = 6f;
            const float titleHeight = 22f;
            const float subtitleHeight = 18f;
            const float titleSubtitleGap = 0f;
            float textBlockHeight = titleHeight + titleSubtitleGap + subtitleHeight;

            Rect headerRect = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(
                new Rect(headerRect.x - 18f, headerRect.y - 4f, headerRect.width + 36f, headerHeight + 4f), HeaderBg);

            EditorGUILayout.BeginVertical(GUILayout.Height(headerHeight));
            Rect rowRect = GUILayoutUtility.GetRect(0f, headerHeight, GUILayout.ExpandWidth(true),
                GUILayout.Height(headerHeight));

            var iconRect = new Rect(
                rowRect.x,
                rowRect.y + ((rowRect.height - iconSize) * 0.5f),
                iconSize,
                iconSize);
            if (_convaiIcon != null && Event.current.type == EventType.Repaint)
                GUI.DrawTexture(iconRect, _convaiIcon, ScaleMode.ScaleToFit, true);

            float textStartX = iconRect.xMax + iconTextSpacing;
            float textWidth = Mathf.Max(0f, rowRect.width - (iconSize + iconTextSpacing));
            float textStartY = rowRect.y + Mathf.Max(0f, (rowRect.height - textBlockHeight) * 0.5f);
            var titleRect = new Rect(textStartX, textStartY, textWidth, titleHeight);
            var subtitleRect = new Rect(textStartX, titleRect.yMax + titleSubtitleGap, textWidth, subtitleHeight);

            GUI.Label(titleRect, "Lip Sync Profile", _headerStyle);
            GUI.Label(subtitleRect, $"{asset.DisplayName} (id: {asset.ProfileId})", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndVertical();
            GUILayout.Space(6f);
        }

        private void DrawIdentityCard()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Runtime Identity", _sectionHeaderStyle ?? EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Stable key used by profile lookup, component lock, and map targeting.",
                _captionStyle ?? EditorStyles.miniLabel);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(
                _profileIdProp,
                new GUIContent("Profile ID", "Canonical runtime identifier. Example: arkit, metahuman."));
            if (EditorGUI.EndChangeCheck())
            {
                _profileIdProp.stringValue = Normalize(_profileIdProp.stringValue);
                if (!_useCustomTransportToken) _transportFormatProp.stringValue = _profileIdProp.stringValue;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4f);
        }

        private void DrawDisplayCard()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Editor Label", _sectionHeaderStyle ?? EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Shown in dropdowns and tools only. No runtime transport impact.",
                _captionStyle ?? EditorStyles.miniLabel);

            EditorGUILayout.PropertyField(
                _displayNameProp,
                new GUIContent("Display Name", "Human-readable name for inspector and editor lists."));

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4f);
        }

        private void DrawTransportCard()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Backend Transport Token", _sectionHeaderStyle ?? EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Validated against incoming payload format. Mismatch drops packets.",
                _captionStyle ?? EditorStyles.miniLabel);

            if (!_transportStateInitialized) InitializeTransportStateFromSerialized();

            string normalizedProfileId = Normalize(_profileIdProp.stringValue);
            string normalizedTransport = Normalize(_transportFormatProp.stringValue);
            if (!_useCustomTransportToken &&
                !string.Equals(normalizedTransport, normalizedProfileId, StringComparison.Ordinal))
                _transportFormatProp.stringValue = normalizedProfileId;

            EditorGUI.BeginChangeCheck();
            bool nextUseCustom = EditorGUILayout.ToggleLeft(
                new GUIContent("Override default token", "Enable only when backend token differs from Profile ID."),
                _useCustomTransportToken);
            if (EditorGUI.EndChangeCheck())
            {
                _useCustomTransportToken = nextUseCustom;
                if (!_useCustomTransportToken)
                {
                    GUI.FocusControl(string.Empty);
                    _transportFormatProp.stringValue = normalizedProfileId;
                }
                else if (string.IsNullOrWhiteSpace(_transportFormatProp.stringValue))
                    _transportFormatProp.stringValue = normalizedProfileId;
            }

            if (_useCustomTransportToken)
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(
                    _transportFormatProp,
                    new GUIContent("Transport Token",
                        "Format token sent to backend and expected in lip sync payloads."));
                if (EditorGUI.EndChangeCheck())
                    _transportFormatProp.stringValue = Normalize(_transportFormatProp.stringValue);
            }
            else
            {
                _transportFormatProp.stringValue = normalizedProfileId;
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.TextField("Transport Token", normalizedProfileId);
            }

            if (!_useCustomTransportToken)
            {
                EditorGUILayout.LabelField(
                    $"Using Profile ID token: {ToLabelValue(normalizedProfileId)}",
                    _captionStyle ?? EditorStyles.miniLabel);
            }
            else if (GUILayout.Button("Use Profile ID Token"))
            {
                GUI.FocusControl(string.Empty);
                _useCustomTransportToken = false;
                _transportFormatProp.stringValue = normalizedProfileId;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4f);
        }

        private void DrawValidationMessages()
        {
            string profileId = Normalize(_profileIdProp.stringValue);
            string transport = Normalize(_transportFormatProp.stringValue);

            if (string.IsNullOrWhiteSpace(profileId))
                EditorGUILayout.HelpBox("Profile ID cannot be empty.", MessageType.Error);

            if (string.IsNullOrWhiteSpace(transport))
                EditorGUILayout.HelpBox("Transport Token cannot be empty.", MessageType.Error);

            if (_useCustomTransportToken &&
                string.Equals(profileId, transport, StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(profileId))
                EditorGUILayout.HelpBox("Custom override is enabled but token equals Profile ID.", MessageType.Info);
        }

        private void InitializeTransportStateFromSerialized()
        {
            if (_profileIdProp == null || _transportFormatProp == null) return;

            string profileId = Normalize(_profileIdProp.stringValue);
            string transport = Normalize(_transportFormatProp.stringValue);
            _useCustomTransportToken =
                !string.IsNullOrWhiteSpace(transport) &&
                !string.Equals(profileId, transport, StringComparison.Ordinal);
            _transportStateInitialized = true;
        }

        private static string Normalize(string raw) => LipSyncProfileId.Normalize(raw);

        private static string ToLabelValue(string raw) => string.IsNullOrWhiteSpace(raw) ? "(empty)" : raw;
    }
}
#endif
