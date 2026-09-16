using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Convai.Modules.Narrative;
using Convai.RestAPI.Internal.Models;
using Convai.Runtime.Behaviors;
using UnityEditor;
using UnityEngine;

namespace Convai.Editor.Inspectors
{
    /// <summary>
    ///     Custom editor for ConvaiNarrativeDesignTrigger.
    ///     Provides trigger dropdown, auto-fetch, mode-specific UI, and visual gizmos.
    /// </summary>
    [CustomEditor(typeof(ConvaiNarrativeDesignTrigger))]
    public class ConvaiNarrativeDesignTriggerEditor : UnityEditor.Editor
    {
        private bool _activationFoldout = true;
        private SerializedProperty _activationModeProp;
        private SerializedProperty _autoFindCharacterProp;

        private SerializedProperty _autoFindPlayerProp;
        private bool _autoRecoveryFoldout;
        private SerializedProperty _availableTriggersProp;

        private SerializedProperty _characterComponentProp;
        private bool _diagnosticsFoldout;

        private SerializedProperty _enableDiagnosticsProp;
        private bool _eventsFoldout;
        private string _fetchError;
        private bool _isFetching;

        private MonoBehaviour _lastCharacterComponent;
        private SerializedProperty _maxWaitTimeProp;
        private SerializedProperty _onPlayerEnterZoneProp;
        private SerializedProperty _onPlayerExitZoneProp;

        private SerializedProperty _onTriggerActivatedProp;
        private SerializedProperty _onTriggerFailedProp;
        private SerializedProperty _onTriggerQueuedProp;
        private SerializedProperty _playerLayerProp;
        private SerializedProperty _playerTagProp;
        private SerializedProperty _proximityRadiusProp;
        private SerializedProperty _queueUntilReadyProp;
        private SerializedProperty _resetOnSceneLoadProp;
        private SerializedProperty _selectedTriggerIndexProp;
        private SerializedProperty _timeDelayProp;
        private ConvaiNarrativeDesignTrigger _trigger;

        private bool _triggerDetailsFoldout;
        private SerializedProperty _triggerIdProp;
        private SerializedProperty _triggerMessageProp;
        private SerializedProperty _triggerNameProp;
        private SerializedProperty _triggerOnceProp;
        private SerializedProperty _validateOnStartProp;
        private string _lastCharacterId;

        private void OnEnable()
        {
            _trigger = (ConvaiNarrativeDesignTrigger)target;

            _characterComponentProp = serializedObject.FindProperty("_characterComponent");
            _autoFindCharacterProp = serializedObject.FindProperty("_autoFindCharacter");
            _selectedTriggerIndexProp = serializedObject.FindProperty("_selectedTriggerIndex");
            _triggerIdProp = serializedObject.FindProperty("_triggerId");
            _triggerNameProp = serializedObject.FindProperty("_triggerName");
            _triggerMessageProp = serializedObject.FindProperty("_triggerMessage");
            _activationModeProp = serializedObject.FindProperty("_activationMode");
            _proximityRadiusProp = serializedObject.FindProperty("_proximityRadius");
            _timeDelayProp = serializedObject.FindProperty("_timeDelay");
            _triggerOnceProp = serializedObject.FindProperty("_triggerOnce");
            _playerLayerProp = serializedObject.FindProperty("_playerLayer");
            _playerTagProp = serializedObject.FindProperty("_playerTag");
            _availableTriggersProp = serializedObject.FindProperty("_availableTriggers");

            _autoFindPlayerProp = serializedObject.FindProperty("_autoFindPlayer");
            _queueUntilReadyProp = serializedObject.FindProperty("_queueUntilReady");
            _maxWaitTimeProp = serializedObject.FindProperty("_maxWaitTime");
            _resetOnSceneLoadProp = serializedObject.FindProperty("_resetOnSceneLoad");

            _enableDiagnosticsProp = serializedObject.FindProperty("_enableDiagnostics");
            _validateOnStartProp = serializedObject.FindProperty("_validateOnStart");

            _onTriggerActivatedProp = serializedObject.FindProperty("_onTriggerActivated");
            _onPlayerEnterZoneProp = serializedObject.FindProperty("_onPlayerEnterZone");
            _onPlayerExitZoneProp = serializedObject.FindProperty("_onPlayerExitZone");
            _onTriggerFailedProp = serializedObject.FindProperty("_onTriggerFailed");
            _onTriggerQueuedProp = serializedObject.FindProperty("_onTriggerQueued");

            _lastCharacterComponent = _characterComponentProp.objectReferenceValue as MonoBehaviour;
            _lastCharacterId = _trigger.GetCharacterId();
        }

        private void OnSceneGUI()
        {
            if (_trigger == null) return;

            TriggerActivationMode mode = _trigger.ActivationMode;

            if (mode == TriggerActivationMode.Proximity)
            {
                float radius = _trigger.ProximityRadius;
                Handles.color = new Color(0.2f, 0.8f, 0.2f, 0.5f);
                Handles.DrawWireDisc(_trigger.transform.position, Vector3.up, radius);

                Handles.color = new Color(0.2f, 0.8f, 0.2f, 0.1f);
                Handles.DrawSolidDisc(_trigger.transform.position, Vector3.up, radius);

                EditorGUI.BeginChangeCheck();
                float newRadius = Handles.RadiusHandle(Quaternion.identity, _trigger.transform.position, radius);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_trigger, "Change Proximity Radius");
                    _proximityRadiusProp.floatValue = newRadius;
                    serializedObject.ApplyModifiedProperties();
                }

                Handles.Label(_trigger.transform.position + (Vector3.up * 0.5f),
                    $"Proximity: {radius:F1}m",
                    EditorStyles.boldLabel);
            }
        }

        /// <inheritdoc />
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawCharacterSection();

            DrawDivider();

            DrawTriggerSelectionSection();

            DrawDivider();

            DrawActivationSection();

            DrawDivider();

            DrawAutoRecoverySection();

            DrawDivider();

            DrawDiagnosticsSection();

            DrawDivider();

            DrawEventsSection();

            DrawValidationWarnings();

            serializedObject.ApplyModifiedProperties();

            CheckCharacterChange();
        }

        private void DrawDivider()
        {
            EditorGUILayout.Space(4);
            Rect rect = EditorGUILayout.GetControlRect(false, 1);
            rect.height = 1;
            EditorGUI.DrawRect(rect, new Color(0.3f, 0.3f, 0.3f, 0.5f));
            EditorGUILayout.Space(4);
        }

        private void DrawCharacterSection()
        {
            EditorGUILayout.LabelField("Character", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_characterComponentProp, GUIContent.none);
            if (EditorGUI.EndChangeCheck()) serializedObject.ApplyModifiedProperties();

            EditorGUILayout.PropertyField(_autoFindCharacterProp,
                new GUIContent("Auto Find", "Automatically search for a ConvaiCharacter if none is assigned"));

            var charComponent = _characterComponentProp.objectReferenceValue as MonoBehaviour;
            if (charComponent != null && !(charComponent is IConvaiCharacterAgent))
                EditorGUILayout.HelpBox("Component does not implement IConvaiCharacterAgent.", MessageType.Warning);
            else if (charComponent == null && !_autoFindCharacterProp.boolValue)
            {
                EditorGUILayout.HelpBox("No character assigned. Enable 'Auto Find' or assign a character.",
                    MessageType.Warning);
            }
        }

        private void DrawTriggerSelectionSection()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Trigger", EditorStyles.boldLabel, GUILayout.Width(50));

            GUILayout.FlexibleSpace();

            GUI.enabled = !_isFetching && !string.IsNullOrEmpty(_trigger.GetCharacterId());
            if (GUILayout.Button(_isFetching ? "Fetching..." : "Fetch", GUILayout.Width(60))) FetchTriggers();
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_fetchError)) EditorGUILayout.HelpBox(_fetchError, MessageType.Error);

            int triggerCount = _availableTriggersProp?.arraySize ?? 0;

            if (triggerCount > 0)
            {
                List<TriggerData> triggers = _trigger.AvailableTriggers;
                string[] triggerOptions = new string[triggerCount + 1];
                triggerOptions[0] = "-- Select Trigger --";

                for (int i = 0; i < triggerCount; i++)
                {
                    TriggerData t = triggers[i];
                    triggerOptions[i + 1] = !string.IsNullOrEmpty(t.TriggerName) ? t.TriggerName : t.TriggerId;
                }

                int currentIndex = _selectedTriggerIndexProp.intValue + 1;
                if (currentIndex < 0) currentIndex = 0;

                EditorGUI.BeginChangeCheck();
                int newIndex = EditorGUILayout.Popup(currentIndex, triggerOptions);
                if (EditorGUI.EndChangeCheck())
                {
                    int selectedIndex = newIndex - 1;
                    _selectedTriggerIndexProp.intValue = selectedIndex;

                    if (selectedIndex >= 0 && selectedIndex < triggerCount)
                    {
                        TriggerData selected = triggers[selectedIndex];
                        _triggerIdProp.stringValue = selected.TriggerId;
                        _triggerNameProp.stringValue = selected.TriggerName;
                        _triggerMessageProp.stringValue = string.Empty;
                    }
                    else
                    {
                        _triggerIdProp.stringValue = "";
                        _triggerNameProp.stringValue = "";
                        _triggerMessageProp.stringValue = "";
                    }
                }

                if (_selectedTriggerIndexProp.intValue >= 0 && _selectedTriggerIndexProp.intValue < triggerCount)
                {
                    EditorGUI.indentLevel++;
                    _triggerDetailsFoldout = EditorGUILayout.Foldout(_triggerDetailsFoldout, "Details", true);
                    if (_triggerDetailsFoldout)
                    {
                        TriggerData selectedTrigger = triggers[_selectedTriggerIndexProp.intValue];
                        EditorGUI.BeginDisabledGroup(true);
                        EditorGUILayout.TextField("ID", selectedTrigger.TriggerId);
                        EditorGUILayout.TextField("Message", selectedTrigger.TriggerMessage ?? "(none)");
                        EditorGUILayout.TextField("Destination", selectedTrigger.DestinationSection ?? "(none)");
                        EditorGUI.EndDisabledGroup();
                    }

                    EditorGUI.indentLevel--;
                }
            }
            else
                EditorGUILayout.HelpBox("Click 'Fetch' to load triggers from backend.", MessageType.Info);
        }

        private void DrawActivationSection()
        {
            _activationFoldout = EditorGUILayout.Foldout(_activationFoldout, "Activation Settings", true,
                EditorStyles.foldoutHeader);

            if (_activationFoldout)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(_activationModeProp, new GUIContent("Mode"));

                var mode = (TriggerActivationMode)_activationModeProp.enumValueIndex;

                EditorGUILayout.Space(2);

                switch (mode)
                {
                    case TriggerActivationMode.Collision:
                        EditorGUILayout.PropertyField(_playerTagProp, new GUIContent("Player Tag"));
                        EditorGUILayout.PropertyField(_playerLayerProp, new GUIContent("Player Layer"));
                        break;

                    case TriggerActivationMode.TimeBased:
                        EditorGUILayout.PropertyField(_playerTagProp, new GUIContent("Player Tag"));
                        EditorGUILayout.PropertyField(_playerLayerProp, new GUIContent("Player Layer"));
                        EditorGUILayout.PropertyField(_timeDelayProp, new GUIContent("Delay (seconds)"));
                        break;

                    case TriggerActivationMode.Proximity:
                        EditorGUILayout.PropertyField(_proximityRadiusProp, new GUIContent("Radius"));
                        EditorGUILayout.PropertyField(_playerTagProp, new GUIContent("Player Tag"));
                        EditorGUILayout.PropertyField(_playerLayerProp, new GUIContent("Player Layer"));
                        break;

                    case TriggerActivationMode.Manual:
                        EditorGUILayout.HelpBox("Call InvokeTrigger() from code.", MessageType.Info);
                        break;
                }

                EditorGUILayout.Space(2);

                EditorGUILayout.PropertyField(_triggerOnceProp, new GUIContent("Trigger Once"));

                if (UnityEngine.Application.isPlaying)
                {
                    EditorGUILayout.Space(4);

                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.Toggle("Has Triggered", _trigger.HasTriggered);
                    EditorGUILayout.Toggle("Player In Zone", _trigger.PlayerInZone);
                    EditorGUI.EndDisabledGroup();

                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Invoke")) _trigger.InvokeTrigger();
                    if (GUILayout.Button("Reset")) _trigger.ResetTrigger();
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUI.indentLevel--;
            }
        }

        private void DrawAutoRecoverySection()
        {
            _autoRecoveryFoldout = EditorGUILayout.Foldout(_autoRecoveryFoldout, "Auto-Recovery Settings", true,
                EditorStyles.foldoutHeader);

            if (_autoRecoveryFoldout)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(_autoFindPlayerProp,
                    new GUIContent("Auto Find Player", "Automatically find the player for proximity detection"));
                EditorGUILayout.PropertyField(_queueUntilReadyProp,
                    new GUIContent("Queue Until Ready", "Queue trigger until character is in conversation"));

                if (_queueUntilReadyProp.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(_maxWaitTimeProp,
                        new GUIContent("Max Wait Time", "Maximum seconds to wait (0 = infinite)"));
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.PropertyField(_resetOnSceneLoadProp,
                    new GUIContent("Reset On Scene Load", "Automatically reset trigger when scene reloads"));

                EditorGUI.indentLevel--;
            }
        }

        private void DrawDiagnosticsSection()
        {
            _diagnosticsFoldout =
                EditorGUILayout.Foldout(_diagnosticsFoldout, "Diagnostics", true, EditorStyles.foldoutHeader);

            if (_diagnosticsFoldout)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(_enableDiagnosticsProp,
                    new GUIContent("Enable Diagnostics", "Log detailed diagnostic info to console"));
                EditorGUILayout.PropertyField(_validateOnStartProp,
                    new GUIContent("Validate On Start", "Run validation checks when the game starts"));

                if (UnityEngine.Application.isPlaying)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Runtime Status", EditorStyles.boldLabel);

                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.EnumPopup("Current Status", _trigger.CurrentStatus);
                    EditorGUILayout.Toggle("Character Ready", _trigger.IsCharacterReady);

                    string lastError = _trigger.LastErrorMessage;
                    if (!string.IsNullOrEmpty(lastError)) EditorGUILayout.TextField("Last Error", lastError);
                    EditorGUI.EndDisabledGroup();

                    EditorGUILayout.Space(2);
                    if (GUILayout.Button("Print Diagnostics to Console")) _trigger.PrintDiagnostics();
                }

                EditorGUI.indentLevel--;
            }
        }

        private void DrawEventsSection()
        {
            _eventsFoldout = EditorGUILayout.Foldout(_eventsFoldout, "Events", true, EditorStyles.foldoutHeader);

            if (_eventsFoldout)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(_onTriggerActivatedProp, new GUIContent("On Trigger Activated"));
                EditorGUILayout.Space(2);
                EditorGUILayout.PropertyField(_onPlayerEnterZoneProp, new GUIContent("On Player Enter Zone"));
                EditorGUILayout.Space(2);
                EditorGUILayout.PropertyField(_onPlayerExitZoneProp, new GUIContent("On Player Exit Zone"));
                EditorGUILayout.Space(2);
                EditorGUILayout.PropertyField(_onTriggerFailedProp,
                    new GUIContent("On Trigger Failed", "Called with error message when trigger fails"));
                EditorGUILayout.Space(2);
                EditorGUILayout.PropertyField(_onTriggerQueuedProp,
                    new GUIContent("On Trigger Queued", "Called when trigger is queued waiting for character"));

                EditorGUI.indentLevel--;
            }
        }

        private void DrawValidationWarnings()
        {
            var mode = (TriggerActivationMode)_activationModeProp.enumValueIndex;

            if (mode == TriggerActivationMode.Collision || mode == TriggerActivationMode.TimeBased)
            {
                var collider = _trigger.GetComponent<Collider>();
                if (collider == null)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.HelpBox("Requires a Collider component.", MessageType.Warning);
                    if (GUILayout.Button("Add Box Collider")) Undo.AddComponent<BoxCollider>(_trigger.gameObject);
                }
                else if (!collider.isTrigger)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.HelpBox("Enable 'Is Trigger' on Collider.", MessageType.Warning);
                    if (GUILayout.Button("Enable Is Trigger"))
                    {
                        Undo.RecordObject(collider, "Enable Is Trigger");
                        collider.isTrigger = true;
                    }
                }
            }
        }

        private void FetchTriggers() => _ = FetchTriggersAsync();

        private async Task FetchTriggersAsync()
        {
            if (_isFetching) return;

            string characterId = _trigger.GetCharacterId();
            if (string.IsNullOrEmpty(characterId))
            {
                _fetchError = "No character assigned.";
                return;
            }

            _isFetching = true;
            _fetchError = null;
            Repaint();

            try
            {
                FetchResult<List<TriggerData>> result = await NarrativeDesignFetcher.FetchTriggersAsync(characterId);

                if (result.Success)
                {
                    Undo.RecordObject(_trigger, "Fetch Triggers");
                    _trigger.SetAvailableTriggers(result.Data);
                    EditorUtility.SetDirty(_trigger);
                    _fetchError = null;
                }
                else
                    _fetchError = result.Error;
            }
            catch (Exception ex)
            {
                _fetchError = ex.Message;
            }
            finally
            {
                _isFetching = false;
                Repaint();
            }
        }

        private void CheckCharacterChange()
        {
            var currentCharacter = _characterComponentProp.objectReferenceValue as MonoBehaviour;
            string currentCharacterId = _trigger.GetCharacterId();
            bool characterReferenceChanged = currentCharacter != _lastCharacterComponent;
            bool characterIdChanged = !string.Equals(currentCharacterId, _lastCharacterId, StringComparison.Ordinal);

            if (!characterReferenceChanged && !characterIdChanged) return;

            _lastCharacterComponent = currentCharacter;
            _lastCharacterId = currentCharacterId;

            Undo.RecordObject(_trigger, "Clear Narrative Trigger Selection");
            _trigger.SetAvailableTriggers(new List<TriggerData>());
            EditorUtility.SetDirty(_trigger);

            if (currentCharacter != null && !string.IsNullOrEmpty(currentCharacterId))
            {
                if (currentCharacter is IConvaiCharacterAgent)
                {
                    EditorApplication.delayCall += () =>
                    {
                        if (_trigger != null && !_isFetching) FetchTriggers();
                    };
                }
            }
        }
    }
}
