using Convai.Editor.PropertyDrawers;
using Convai.Shared.Types;
using UnityEditor;
using UnityEngine;

namespace Convai.Editor.Inspectors
{
    /// <summary>
    ///     The serialized properties the room-control groups draw. They come from the scene component or from a
    ///     Room Manager Profile asset, so every field is optional: a null property means "this host does not own
    ///     that setting" and the row is skipped.
    /// </summary>
    internal sealed class ConvaiRoomControlProperties
    {
        public SerializedProperty ConnectionType;
        public SerializedProperty VideoTrackName;
        public SerializedProperty ServerEndpoint;
        public SerializedProperty ConnectOnStart;

        public SerializedProperty TurnTakingOptions;

        /// <summary>Always component-owned, so it stays editable even while profile values are read-only.</summary>
        public SerializedProperty PushToTalkKey;

        public SerializedProperty UserVadSettings;

        public SerializedProperty VisionContextMode;
        public SerializedProperty VisionInputSettings;
        public SerializedProperty VisionRespondModes;

        public SerializedProperty RoomRejoinTtlSeconds;
        public SerializedProperty ResumePolicy;
        public SerializedProperty MaxReconnectAttempts;
        public SerializedProperty SpawnAgentOnRejoin;
        public SerializedProperty StartWaitTimeoutMs;
        public SerializedProperty AutoMicStartDelaySeconds;
    }

    /// <summary>
    ///     Draws Advanced Room Control as uniform collapsible groups. Shared by the ConvaiRoomManager component
    ///     inspector and the Room Manager Profile asset inspector so the two cannot drift apart, and so every
    ///     group sits at the same nesting depth instead of mixing bare labels with nested foldouts.
    /// </summary>
    internal sealed class ConvaiRoomControlInspectorView
    {
        private const string ConnectionSubsectionId = "RoomControl.Connection";
        private const string TurnTakingSubsectionId = "RoomControl.TurnTaking";
        private const string VoiceActivitySubsectionId = "RoomControl.VoiceActivity";
        private const string VisionSubsectionId = "RoomControl.Vision";
        private const string ReconnectSubsectionId = "RoomControl.Reconnect";

        private readonly string _hostId;
        private bool _showConnection;
        private bool _showReconnect;
        private bool _showTurnTaking;
        private bool _showVision;
        private bool _showVoiceActivity;

        public ConvaiRoomControlInspectorView(string hostId)
        {
            _hostId = hostId;
        }

        /// <summary>Restores group expansion from the shared inspector state store. Call from OnEnable.</summary>
        public void LoadState()
        {
            _showConnection = ConvaiInspectorSectionStateStore.Get(_hostId, ConnectionSubsectionId, true);
            _showTurnTaking = ConvaiInspectorSectionStateStore.Get(_hostId, TurnTakingSubsectionId, true);
            _showVoiceActivity = ConvaiInspectorSectionStateStore.Get(_hostId, VoiceActivitySubsectionId, false);
            _showVision = ConvaiInspectorSectionStateStore.Get(_hostId, VisionSubsectionId, false);
            _showReconnect = ConvaiInspectorSectionStateStore.Get(_hostId, ReconnectSubsectionId, false);
        }

        /// <summary>Persists group expansion. Call from OnDisable.</summary>
        public void SaveState()
        {
            ConvaiInspectorSectionStateStore.Set(_hostId, ConnectionSubsectionId, _showConnection);
            ConvaiInspectorSectionStateStore.Set(_hostId, TurnTakingSubsectionId, _showTurnTaking);
            ConvaiInspectorSectionStateStore.Set(_hostId, VoiceActivitySubsectionId, _showVoiceActivity);
            ConvaiInspectorSectionStateStore.Set(_hostId, VisionSubsectionId, _showVision);
            ConvaiInspectorSectionStateStore.Set(_hostId, ReconnectSubsectionId, _showReconnect);
        }

        /// <param name="properties">Properties to draw; null entries are skipped.</param>
        /// <param name="readOnly">True while the values are owned by a Room Manager Profile asset.</param>
        /// <param name="showPushToTalkKey">
        ///     Whether the component-owned push-to-talk key belongs in the Turn Taking group, decided by the host
        ///     because in asset mode the effective mode comes from the profile rather than the drawn property.
        /// </param>
        public void Draw(ConvaiRoomControlProperties properties, bool readOnly, bool showPushToTalkKey)
        {
            if (properties == null)
                return;

            DrawConnection(properties, readOnly);
            DrawTurnTaking(properties, readOnly, showPushToTalkKey);
            DrawVoiceActivity(properties, readOnly);
            DrawVision(properties, readOnly);
            DrawReconnect(properties, readOnly);
        }

        private void DrawConnection(ConvaiRoomControlProperties properties, bool readOnly)
        {
            _showConnection = ConvaiInspectorSectionChrome.DrawSubsectionHeader("Connection", _showConnection);
            if (!_showConnection)
                return;

            ConvaiInspectorSectionChrome.BeginSubsectionBody();
            try
            {
                using (new EditorGUI.DisabledScope(readOnly))
                {
                    // Video track name is only editable on the profile asset; the backend expects a specific
                    // format, so the scene component never exposes it.
                    if (properties.ConnectionType != null)
                        EditorGUILayout.PropertyField(properties.ConnectionType, ConvaiInspectorContent.ConnectionType);

                    if (properties.VideoTrackName != null &&
                        properties.ConnectionType != null &&
                        (ConvaiConnectionType)properties.ConnectionType.enumValueIndex == ConvaiConnectionType.Video)
                        EditorGUILayout.PropertyField(properties.VideoTrackName,
                            ConvaiInspectorContent.VideoTrackName);

                    if (properties.ServerEndpoint != null)
                        ConvaiInspectorFieldUtility.DrawServerEndpointField(properties.ServerEndpoint,
                            ConvaiInspectorContent.Server);

                    if (properties.ConnectOnStart != null)
                        EditorGUILayout.PropertyField(properties.ConnectOnStart, ConvaiInspectorContent.ConnectOnStart);
                }
            }
            finally
            {
                ConvaiInspectorSectionChrome.EndSubsectionBody();
            }
        }

        private void DrawTurnTaking(
            ConvaiRoomControlProperties properties,
            bool readOnly,
            bool showPushToTalkKey)
        {
            if (properties.TurnTakingOptions == null)
                return;

            _showTurnTaking = ConvaiInspectorSectionChrome.DrawSubsectionHeader("Turn Taking", _showTurnTaking);
            if (!_showTurnTaking)
                return;

            ConvaiInspectorSectionChrome.BeginSubsectionBody();
            try
            {
                using (new EditorGUI.DisabledScope(readOnly))
                    TurnTakingOptionsDrawer.DrawContentLayout(properties.TurnTakingOptions);

                if (showPushToTalkKey && properties.PushToTalkKey != null)
                    EditorGUILayout.PropertyField(properties.PushToTalkKey, ConvaiInspectorContent.PushToTalkKey);
            }
            finally
            {
                ConvaiInspectorSectionChrome.EndSubsectionBody();
            }
        }

        private void DrawVoiceActivity(ConvaiRoomControlProperties properties, bool readOnly)
        {
            if (properties.UserVadSettings == null)
                return;

            _showVoiceActivity = ConvaiInspectorSectionChrome.DrawSubsectionHeader(
                "Voice Activity Detection",
                _showVoiceActivity);
            if (!_showVoiceActivity)
                return;

            ConvaiInspectorSectionChrome.BeginSubsectionBody();
            try
            {
                ConvaiUserVadSettingsInspectorUtility.DrawUserVadSettings(
                    properties.UserVadSettings,
                    readOnly,
                    false);
            }
            finally
            {
                ConvaiInspectorSectionChrome.EndSubsectionBody();
            }
        }

        private void DrawVision(ConvaiRoomControlProperties properties, bool readOnly)
        {
            if (properties.VisionContextMode == null &&
                properties.VisionInputSettings == null &&
                properties.VisionRespondModes == null)
                return;

            _showVision = ConvaiInspectorSectionChrome.DrawSubsectionHeader("Dynamic Vision Context", _showVision);
            if (!_showVision)
                return;

            ConvaiInspectorSectionChrome.BeginSubsectionBody();
            try
            {
                using (new EditorGUI.DisabledScope(readOnly))
                {
                    if (properties.VisionContextMode != null)
                        EditorGUILayout.PropertyField(properties.VisionContextMode,
                            ConvaiInspectorContent.VisionContextMode);

                    if (properties.VisionInputSettings != null)
                    {
                        DrawGroupLabel("Frame Sampling");
                        DrawChildrenFlat(properties.VisionInputSettings);
                    }

                    if (properties.VisionRespondModes != null)
                    {
                        DrawGroupLabel("Respond Modes");
                        DrawChildrenFlat(properties.VisionRespondModes);
                    }
                }
            }
            finally
            {
                ConvaiInspectorSectionChrome.EndSubsectionBody();
            }
        }

        private void DrawReconnect(ConvaiRoomControlProperties properties, bool readOnly)
        {
            _showReconnect = ConvaiInspectorSectionChrome.DrawSubsectionHeader("Reconnect", _showReconnect);
            if (!_showReconnect)
                return;

            ConvaiInspectorSectionChrome.BeginSubsectionBody();
            try
            {
                using (new EditorGUI.DisabledScope(readOnly))
                {
                    DrawOptionalProperty(properties.RoomRejoinTtlSeconds, ConvaiInspectorContent.RoomRejoinTtlSeconds);
                    DrawOptionalProperty(properties.ResumePolicy, ConvaiInspectorContent.ResumePolicy);
                    DrawOptionalProperty(properties.MaxReconnectAttempts,
                        ConvaiInspectorContent.MaxReconnectAttempts);
                    DrawOptionalProperty(properties.SpawnAgentOnRejoin, ConvaiInspectorContent.SpawnAgentOnRejoin);
                    DrawOptionalProperty(properties.StartWaitTimeoutMs, ConvaiInspectorContent.StartWaitTimeoutMs);
                    DrawOptionalProperty(properties.AutoMicStartDelaySeconds,
                        ConvaiInspectorContent.AutoMicStartDelaySeconds);
                }
            }
            finally
            {
                ConvaiInspectorSectionChrome.EndSubsectionBody();
            }
        }

        private static void DrawOptionalProperty(SerializedProperty property, GUIContent label)
        {
            if (property != null)
                EditorGUILayout.PropertyField(property, label);
        }

        private static void DrawGroupLabel(string title) =>
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);

        /// <summary>
        ///     Draws a nested settings object's fields inline. Keeps every group one level deep instead of
        ///     hiding half the settings behind an extra Unity foldout.
        /// </summary>
        private static void DrawChildrenFlat(SerializedProperty parent)
        {
            SerializedProperty iterator = parent.Copy();
            SerializedProperty end = iterator.GetEndProperty();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = false;
                EditorGUILayout.PropertyField(iterator, true);
            }
        }
    }
}
