using System.Collections.Generic;
using Convai.Modules.Narrative;
using Convai.Runtime.Behaviors;
using UnityEditor;
using UnityEngine;

namespace Convai.Editor.Inspectors
{
    /// <summary>
    ///     Custom editor for ConvaiNarrativeDesignManager.
    ///     Provides auto-fetch functionality, collapsible sections, and visual orphan indicators.
    /// </summary>
    [CustomEditor(typeof(ConvaiNarrativeDesignManager))]
    public class ConvaiNarrativeDesignManagerEditor : UnityEditor.Editor
    {
        private readonly Dictionary<string, bool> _sectionFoldouts = new();
        private SerializedProperty _characterComponentProp;
        private bool _eventsFoldout;

        private bool _hasPendingCharacterChange;
        private SerializedProperty _isFetchingProp;

        private MonoBehaviour _lastCharacterComponent;
        private SerializedProperty _lastFetchErrorProp;
        private SerializedProperty _lastSyncTimeProp;
        private string _lastTrackedCharacterId;
        private ConvaiNarrativeDesignManager _manager;
        private SerializedProperty _onAnySectionChangedProp;
        private SerializedProperty _onSectionDataReceivedProp;
        private SerializedProperty _onSectionsSyncedProp;
        private MonoBehaviour _pendingNewCharacter;
        private string _pendingNewCharacterId;
        private SerializedProperty _sectionConfigsProp;
        private bool _sectionsFoldout = true;

        private GUIStyle _statusLabelStyle;
        private bool _stylesInitialized;
        private bool _templateKeysFoldout;
        private SerializedProperty _templateKeysProp;

        private void OnEnable()
        {
            _manager = (ConvaiNarrativeDesignManager)target;

            _characterComponentProp = serializedObject.FindProperty("_characterComponent");
            _sectionConfigsProp = serializedObject.FindProperty("_sectionConfigs");
            _templateKeysProp = serializedObject.FindProperty("_templateKeys");
            _isFetchingProp = serializedObject.FindProperty("_isFetching");
            _lastSyncTimeProp = serializedObject.FindProperty("_lastSyncTime");
            _lastFetchErrorProp = serializedObject.FindProperty("_lastFetchError");
            _onAnySectionChangedProp = serializedObject.FindProperty("_onAnySectionChanged");
            _onSectionDataReceivedProp = serializedObject.FindProperty("_onSectionDataReceived");
            _onSectionsSyncedProp = serializedObject.FindProperty("_onSectionsSynced");

            _lastCharacterComponent = _characterComponentProp.objectReferenceValue as MonoBehaviour;
            _lastTrackedCharacterId = GetCharacterIdFromComponent(_lastCharacterComponent);
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            _statusLabelStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };

            _stylesInitialized = true;
        }

        /// <inheritdoc />
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            InitializeStyles();

            DrawCharacterField();

            DrawDivider();

            DrawSyncStatus();

            DrawDivider();

            DrawSectionsSection();

            DrawDivider();

            DrawTemplateKeysSection();

            DrawDivider();

            DrawEventsSection();

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

        private void DrawCharacterField()
        {
            EditorGUILayout.LabelField("Character", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_characterComponentProp, GUIContent.none);
            if (EditorGUI.EndChangeCheck()) serializedObject.ApplyModifiedProperties();

            var charComponent = _characterComponentProp.objectReferenceValue as MonoBehaviour;
            if (charComponent != null && !(charComponent is IConvaiCharacterAgent))
            {
                EditorGUILayout.HelpBox("Selected component does not implement IConvaiCharacterAgent.",
                    MessageType.Warning);
            }
        }

        private void DrawSyncStatus()
        {
            if (_manager == null) return;

            EditorGUILayout.BeginHorizontal();

            bool isFetching = _isFetchingProp?.boolValue ?? false;
            GUI.enabled = !isFetching && !string.IsNullOrEmpty(_manager.GetCharacterId());

            if (GUILayout.Button(isFetching ? "Syncing..." : "Sync with Backend", GUILayout.Width(130)))
                _manager.FetchAndSyncFromBackend();

            GUI.enabled = true;

            GUILayout.FlexibleSpace();

            int activeCount = _manager.ActiveSectionCount;
            int orphanedCount = _manager.OrphanedSectionCount;

            string statusText = orphanedCount > 0
                ? $"{activeCount} sections, {orphanedCount} orphaned"
                : $"{activeCount} sections";

            EditorGUILayout.LabelField(statusText, _statusLabelStyle, GUILayout.Width(120));

            EditorGUILayout.EndHorizontal();

            string lastSync = _lastSyncTimeProp.stringValue;
            if (!string.IsNullOrEmpty(lastSync))
                EditorGUILayout.LabelField($"Last sync: {lastSync}", EditorStyles.miniLabel);

            string lastError = _lastFetchErrorProp.stringValue;
            if (!string.IsNullOrEmpty(lastError)) EditorGUILayout.HelpBox(lastError, MessageType.Error);
        }

        private void DrawSectionsSection()
        {
            if (_manager == null) return;

            int count = _manager.ActiveSectionCount + _manager.OrphanedSectionCount;
            string headerText = count > 0 ? $"Narrative Sections ({count})" : "Narrative Sections";

            _sectionsFoldout = EditorGUILayout.Foldout(_sectionsFoldout, headerText, true, EditorStyles.foldoutHeader);

            if (_sectionsFoldout)
            {
                EditorGUI.indentLevel++;

                if (_sectionConfigsProp == null || _sectionConfigsProp.arraySize == 0)
                    EditorGUILayout.HelpBox("No sections. Click 'Sync with Backend' to fetch.", MessageType.Info);
                else
                {
                    EditorGUILayout.Space(4);

                    for (int i = 0; i < _sectionConfigsProp.arraySize; i++)
                    {
                        SerializedProperty sectionProp = _sectionConfigsProp.GetArrayElementAtIndex(i);
                        if (sectionProp != null) DrawSection(sectionProp, i);
                    }
                }

                EditorGUI.indentLevel--;
            }
        }

        private void DrawSection(SerializedProperty sectionProp, int index)
        {
            SerializedProperty sectionIdProp = sectionProp.FindPropertyRelative("_sectionId");
            SerializedProperty sectionNameProp = sectionProp.FindPropertyRelative("_sectionName");
            SerializedProperty isOrphanedProp = sectionProp.FindPropertyRelative("_isOrphaned");
            SerializedProperty onStartProp = sectionProp.FindPropertyRelative("_onSectionStart");
            SerializedProperty onEndProp = sectionProp.FindPropertyRelative("_onSectionEnd");

            string sectionId = sectionIdProp?.stringValue ?? "";
            string sectionName = sectionNameProp?.stringValue ?? "";
            bool isOrphaned = isOrphanedProp?.boolValue ?? false;

            string foldoutKey = string.IsNullOrEmpty(sectionId) ? $"section_{index}" : sectionId;
            if (!_sectionFoldouts.ContainsKey(foldoutKey)) _sectionFoldouts[foldoutKey] = false;

            string displayName = !string.IsNullOrEmpty(sectionName) ? sectionName : sectionId;

            if (isOrphaned) displayName = $"⚠ {displayName} (orphaned)";

            _sectionFoldouts[foldoutKey] = EditorGUILayout.Foldout(_sectionFoldouts[foldoutKey], displayName, true);

            if (_sectionFoldouts[foldoutKey])
            {
                EditorGUI.indentLevel++;

                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextField("Section ID", sectionId);
                EditorGUI.EndDisabledGroup();

                if (isOrphaned) EditorGUILayout.HelpBox("Deleted on backend. Events preserved.", MessageType.Warning);

                EditorGUILayout.Space(2);

                EditorGUILayout.PropertyField(onStartProp, new GUIContent("On Section Start"));
                EditorGUILayout.PropertyField(onEndProp, new GUIContent("On Section End"));

                EditorGUILayout.Space(4);

                EditorGUI.indentLevel--;
            }
        }

        private void DrawTemplateKeysSection()
        {
            _templateKeysFoldout =
                EditorGUILayout.Foldout(_templateKeysFoldout, "Template Keys", true, EditorStyles.foldoutHeader);

            if (_templateKeysFoldout)
            {
                EditorGUI.indentLevel++;

                if (_templateKeysProp != null)
                    EditorGUILayout.PropertyField(_templateKeysProp, new GUIContent("Keys"), true);

                EditorGUILayout.Space(4);

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Send to Server", GUILayout.Width(100)))
                {
                    if (UnityEngine.Application.isPlaying)
                        _manager.SendTemplateKeysUpdate();
                    else
                    {
                        EditorUtility.DisplayDialog("Not in Play Mode",
                            "Template keys can only be sent during Play Mode.", "OK");
                    }
                }

                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel--;
            }
        }

        private void DrawEventsSection()
        {
            _eventsFoldout = EditorGUILayout.Foldout(_eventsFoldout, "Events", true, EditorStyles.foldoutHeader);

            if (_eventsFoldout)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(_onAnySectionChangedProp, new GUIContent("On Any Section Changed"));
                EditorGUILayout.Space(2);
                EditorGUILayout.PropertyField(_onSectionDataReceivedProp, new GUIContent("On Section Data Received"));
                EditorGUILayout.Space(2);
                EditorGUILayout.PropertyField(_onSectionsSyncedProp, new GUIContent("On Sections Synced"));

                EditorGUI.indentLevel--;
            }
        }

        private void CheckCharacterChange()
        {
            if (_characterComponentProp == null || _manager == null) return;

            var currentCharacter = _characterComponentProp.objectReferenceValue as MonoBehaviour;
            string currentCharacterId = GetCharacterIdFromComponent(currentCharacter);

            bool characterIdChanged = !string.IsNullOrEmpty(_lastTrackedCharacterId) &&
                                      !string.IsNullOrEmpty(currentCharacterId) &&
                                      _lastTrackedCharacterId != currentCharacterId;

            bool hasExistingSections = _manager.ActiveSectionCount > 0 || _manager.OrphanedSectionCount > 0;

            if (characterIdChanged && hasExistingSections)
            {
                if (!_hasPendingCharacterChange)
                {
                    _hasPendingCharacterChange = true;
                    _pendingNewCharacter = currentCharacter;
                    _pendingNewCharacterId = currentCharacterId;

                    EditorApplication.delayCall += ShowCharacterChangeDialog;
                }
            }
            else if (currentCharacter != _lastCharacterComponent && currentCharacter != null)
            {
                _lastCharacterComponent = currentCharacter;
                _lastTrackedCharacterId = currentCharacterId;

                if (currentCharacter is IConvaiCharacterAgent)
                {
                    EditorApplication.delayCall += () =>
                    {
                        if (_manager != null && !_manager.IsFetching)
                        {
                            _manager.FetchAndSyncFromBackend();
                            Repaint();
                        }
                    };
                }
            }
        }

        private void ShowCharacterChangeDialog()
        {
            _hasPendingCharacterChange = false;

            if (_manager == null) return;

            bool confirmed = EditorUtility.DisplayDialog(
                "Character Change Detected",
                "You are switching to a different character. This will permanently clear all existing narrative section configurations, including any Unity Events (OnSectionStart/OnSectionEnd) you have configured.\n\n" +
                "This is a one-way operation and cannot be undone.\n\n" +
                "Do you want to proceed?",
                "Yes, Clear and Switch",
                "Cancel"
            );

            if (confirmed)
            {
                _manager.ClearAllSectionConfigs();

                EditorUtility.SetDirty(_manager);

                _lastCharacterComponent = _pendingNewCharacter;
                _lastTrackedCharacterId = _pendingNewCharacterId;

                EditorApplication.delayCall += () =>
                {
                    if (_manager != null && !_manager.IsFetching)
                    {
                        _manager.FetchAndSyncFromBackend();
                        Repaint();
                    }
                };
            }
            else
            {
                serializedObject.Update();
                _characterComponentProp.objectReferenceValue = _lastCharacterComponent;
                serializedObject.ApplyModifiedProperties();
                Repaint();
            }

            _pendingNewCharacter = null;
            _pendingNewCharacterId = null;
        }

        private string GetCharacterIdFromComponent(MonoBehaviour component)
        {
            if (component is IConvaiCharacterAgent agent) return agent.CharacterId;
            return null;
        }
    }
}
