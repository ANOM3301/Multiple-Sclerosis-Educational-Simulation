using System.Collections.Generic;
using Convai.Domain.Embodiment.Interfaces;
using Convai.Domain.Embodiment.Readings;
using Convai.Domain.Embodiment.Taxonomy;
using Convai.Editor.Inspectors;
using Convai.Modules.Attention.Components;
using Convai.Modules.Attention.Providers;
using Convai.Modules.Attention.Profiles;
using Convai.Modules.Embodiment.Components;
using Convai.Modules.Embodiment.Presets;
using Convai.Modules.ConversationFlow.Components;
using Convai.Modules.Emotion.Components;
using Convai.Modules.Emotion.Profiles;
using Convai.Modules.Emotion.Taxonomy;
using Convai.Modules.FacialAnimation.Components;
using Convai.Modules.Gaze.Components;
using Convai.Modules.Gaze.Profiles;
using Convai.Runtime.Embodiment;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Convai.Editor.Embodiment.Inspectors
{
    [CustomEditor(typeof(EmbodimentContext))]
    internal sealed class EmbodimentContextEditor : BrandedEmbodimentComponentEditor { }

    [CustomEditor(typeof(ConvaiCharacterEmbodimentBinding))]
    internal sealed class ConvaiCharacterEmbodimentBindingEditor : ConvaiPremiumInspectorEditor
    {
        private const string SectionPreset = "Preset";
        private const string SectionAdvanced = "Advanced";

        private SerializedProperty _preset;
        private SerializedProperty _library;
        private SerializedProperty _preserveMissingSlots;
        private int _selectedPresetIndex;
        private bool _showPreset;
        private bool _showAdvanced;

        protected override void OnEnable()
        {
            base.OnEnable();
            _preset = serializedObject.FindProperty("preset");
            _library = serializedObject.FindProperty("library");
            _preserveMissingSlots = serializedObject.FindProperty("preserveMissingSlots");
            _showPreset = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionPreset, true);
            _showAdvanced = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionAdvanced, false);
        }

        protected override void OnDisable()
        {
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionPreset, _showPreset);
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionAdvanced, _showAdvanced);
            base.OnDisable();
        }

        public override void OnInspectorGUI()
        {
            EnsurePremiumStyles();
            serializedObject.Update();

            DrawPremiumHeader("Character Embodiment Binding", "Applies embodiment presets", "Editor", IdleColor);
            DrawInfoBox(
                "What this does",
                "Applies one embodiment preset to the animation, emotion, attention, gaze, and facial modules on this character.");
            DrawPresetSection();
            DrawAdvancedSection();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawPresetSection()
        {
            _showPreset = DrawSection(SectionPreset, "PRESET", _showPreset, ConvaiInspectorIconIds.Profile);
            if (!_showPreset) return;

            DrawSectionBody(() => { EditorGUILayout.PropertyField(_preset, new GUIContent("Embodiment Preset")); });
        }

        private void DrawAdvancedSection()
        {
            _showAdvanced = DrawSection(SectionAdvanced, "ADVANCED", _showAdvanced, ConvaiInspectorIconIds.Routing);
            if (!_showAdvanced) return;

            DrawSectionBody(() =>
            {
                EditorGUILayout.PropertyField(_library, new GUIContent("Preset Library"));
                EditorGUILayout.PropertyField(_preserveMissingSlots, new GUIContent("Keep Unmatched Module Profiles"));
                DrawRuntimePresetSwap();
            });
        }

        private void DrawRuntimePresetSwap()
        {
            var binding = (ConvaiCharacterEmbodimentBinding)target;
            EmbodimentPresetLibrary library = binding.Library;
            if (library == null)
            {
                DrawInfoBox("Runtime Preset Swap", "Assign a preset library here only if this character needs to switch embodiment presets during Play Mode.");
                return;
            }

            IReadOnlyList<CharacterEmbodimentPreset> presets = library.Presets;
            if (presets == null || presets.Count == 0)
            {
                DrawWarningBox("Runtime Preset Swap", "The assigned preset library has no presets to apply.");
                return;
            }

            UnityEngine.GUILayout.Space(4f);
            EditorGUILayout.LabelField("Runtime Preset Swap", EditorStyles.boldLabel);
            if (!UnityEngine.Application.isPlaying)
            {
                DrawInfoBox("Play Mode Only", "Enter Play Mode to switch presets at runtime.");
                return;
            }

            string[] presetIds = BuildPresetIdLabels(presets);
            _selectedPresetIndex = UnityEngine.Mathf.Clamp(_selectedPresetIndex, 0, presets.Count - 1);
            _selectedPresetIndex = EditorGUILayout.Popup("Preset", _selectedPresetIndex, presetIds);

            if (UnityEngine.GUILayout.Button("Apply Selected Preset", UnityEngine.GUILayout.Height(26f)))
            {
                CharacterEmbodimentPreset selected = presets[_selectedPresetIndex];
                if (selected != null)
                {
                    binding.ApplyPresetById(selected.PresetId);
                }
            }
        }

        private static string[] BuildPresetIdLabels(IReadOnlyList<CharacterEmbodimentPreset> presets)
        {
            string[] labels = new string[presets.Count];
            for (int i = 0; i < presets.Count; i++)
            {
                CharacterEmbodimentPreset p = presets[i];
                labels[i] = p != null && !string.IsNullOrWhiteSpace(p.PresetId)
                    ? p.PresetId
                    : $"<null slot {i}>";
            }
            return labels;
        }
    }

    [CustomEditor(typeof(ConvaiConversationFlowController))]
    internal sealed class ConvaiConversationFlowControllerEditor : ConvaiPremiumInspectorEditor
    {
        private const string SectionProfile = "Profile";
        private const string SectionLive = "LiveFlow";
        private const string SectionValidation = "Validation";

        private SerializedProperty _profile;
        private bool _showProfile;
        private bool _showLive;
        private bool _showValidation;

        protected override void OnEnable()
        {
            base.OnEnable();
            _profile = serializedObject.FindProperty("profile");
            _showProfile = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionProfile, true);
            _showLive = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionLive, true);
            _showValidation = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionValidation, true);
        }

        protected override void OnDisable()
        {
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionProfile, _showProfile);
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionLive, _showLive);
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionValidation, _showValidation);
            base.OnDisable();
        }

        public override void OnInspectorGUI()
        {
            EnsurePremiumStyles();
            serializedObject.Update();

            var driver = (ConvaiConversationFlowController)target;
            ConvaiEmbodimentContextEditorInfo contextInfo = ConvaiEmbodimentContextEditorResolver.Resolve(driver);
            EmbodimentContext context = contextInfo.Context;
            string status = UnityEngine.Application.isPlaying ? "Live" : "Editor";
            Color statusColor = UnityEngine.Application.isPlaying ? AccentEmphasis : IdleColor;
            if (!contextInfo.HasSupportedCharacterScope)
            {
                status = "Warning";
                statusColor = Warning;
            }

            DrawPremiumHeader("Conversation Flow Driver", "Realtime Dialogue State", status, statusColor);
            DrawValidationSummary(contextInfo, false);
            DrawInfoBox(
                "What this does",
                "Tracks the current dialogue phase (Idle, Speaking, Reacting) and drives timing transitions so other modules can respond to conversation state.");
            DrawProfileSection();
            DrawLiveSection(driver, context);
            DrawValidationSection(contextInfo);

            serializedObject.ApplyModifiedProperties();
            if (UnityEngine.Application.isPlaying) Repaint();
        }

        private void DrawProfileSection()
        {
            _showProfile = DrawSection(SectionProfile, "PROFILE", _showProfile, ConvaiInspectorIconIds.Profile);
            if (!_showProfile) return;

            DrawSectionBody(() =>
            {
                EditorGUILayout.PropertyField(_profile, new GUIContent("Flow Profile"));
                if (_profile.objectReferenceValue == null)
                    DrawInfoBox("SDK Default Active", "No profile asset is assigned. This component still works and uses the SDK conversation-flow defaults at runtime.");
            });
        }

        private void DrawLiveSection(ConvaiConversationFlowController driver, EmbodimentContext context)
        {
            _showLive = DrawSection(SectionLive, "LIVE STATE", _showLive, ConvaiInspectorIconIds.Live, Info);
            if (!_showLive) return;

            DrawSectionBody(() =>
            {
                if (!UnityEngine.Application.isPlaying)
                {
                    DrawOfflinePlaceholder();
                    return;
                }

                DialogueStateReading reading = driver.Current;
                EditorGUILayout.BeginHorizontal();
                DrawLiveCell("State", reading.Primary.ToString(), AccentEmphasis, 110);
                DrawLiveCell("Blend To", reading.BlendTo.ToString(), DefaultValueColor, 110);
                DrawLiveCell("Blend", reading.BlendWeight.ToString("0.00"), Info, 110);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField("Time In State", $"{reading.TimeInState:F1}s");
                EditorGUILayout.LabelField("Energy Level", reading.EnergyLevel.ToString("0.00"));
            });
        }

        private void DrawValidationSection(ConvaiEmbodimentContextEditorInfo contextInfo)
        {
            _showValidation = DrawSection(SectionValidation, "VALIDATION", _showValidation, ConvaiInspectorIconIds.Validation, Warning);
            if (!_showValidation) return;

            DrawSectionBody(() => DrawValidationSummary(contextInfo, true));
        }

        private void DrawValidationSummary(ConvaiEmbodimentContextEditorInfo contextInfo, bool includeInfo)
        {
            DrawContextResolutionSummary(contextInfo, includeInfo, "driver");
        }
    }

    [CustomEditor(typeof(ConvaiAttentionController))]
    internal sealed class ConvaiAttentionControllerEditor : ConvaiPremiumInspectorEditor
    {
        private const string SectionProfile = "Profile";
        private const string SectionDiscovery = "ProviderDiscovery";
        private const string SectionLive = "LiveAttention";
        private const string SectionValidation = "Validation";

        private SerializedProperty _profile;
        private SerializedProperty _discoverProvidersInHierarchy;
        private SerializedProperty _autoCreateDefaultFocusProvider;
        private bool _showProfile;
        private bool _showDiscovery;
        private bool _showLive;
        private bool _showValidation;

        protected override void OnEnable()
        {
            base.OnEnable();
            _profile = serializedObject.FindProperty("profile");
            _discoverProvidersInHierarchy = serializedObject.FindProperty("discoverProvidersInHierarchy");
            _autoCreateDefaultFocusProvider = serializedObject.FindProperty("autoCreateDefaultFocusProvider");
            _showProfile = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionProfile, true);
            _showDiscovery = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionDiscovery, false);
            _showLive = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionLive, true);
            _showValidation = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionValidation, true);
        }

        protected override void OnDisable()
        {
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionProfile, _showProfile);
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionDiscovery, _showDiscovery);
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionLive, _showLive);
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionValidation, _showValidation);
            base.OnDisable();
        }

        public override void OnInspectorGUI()
        {
            EnsurePremiumStyles();
            serializedObject.Update();

            var controller = (ConvaiAttentionController)target;
            ConvaiEmbodimentContextEditorInfo contextInfo = ConvaiEmbodimentContextEditorResolver.Resolve(controller);
            EmbodimentContext context = contextInfo.Context;
            int providerCount = CountFocusProviders(controller, contextInfo);
            bool hasBlockingWarning = !contextInfo.HasSupportedCharacterScope ||
                                      (providerCount == 0 && !_autoCreateDefaultFocusProvider.boolValue);
            string status = UnityEngine.Application.isPlaying ? "Live" : "Editor";
            Color statusColor = UnityEngine.Application.isPlaying ? AccentEmphasis : IdleColor;
            if (hasBlockingWarning)
            {
                status = "Warning";
                statusColor = Warning;
            }
            else if (!UnityEngine.Application.isPlaying && contextInfo.WillAutoCreateOnCharacter)
            {
                status = "Auto";
                statusColor = AccentEmphasis;
            }

            DrawPremiumHeader("Attention Controller", "Chooses what the character pays attention to", status, statusColor);

            DrawValidationSummary(contextInfo, providerCount, false);
            DrawInfoBox(
                "What this does",
                "Chooses the current focus target for the character. Other modules use this target for eye gaze, head look, and attention-aware animation.");
            DrawProfileSection();
            DrawDiscoverySection(providerCount);
            DrawLiveSection(controller, context);
            DrawValidationSection(contextInfo, providerCount);

            serializedObject.ApplyModifiedProperties();
            if (UnityEngine.Application.isPlaying) Repaint();
        }

        private void DrawProfileSection()
        {
            _showProfile = DrawSection(SectionProfile, "PROFILE", _showProfile, ConvaiInspectorIconIds.Profile);
            if (!_showProfile) return;

            DrawSectionBody(() =>
            {
                EditorGUILayout.PropertyField(_profile, new GUIContent("Attention Profile"));
                var profile = _profile.objectReferenceValue as ConvaiAttentionProfile;
                if (profile == null)
                {
                    DrawInfoBox("SDK Default Active", "No profile asset is assigned. This component still works and uses the SDK attention defaults at runtime.");
                    return;
                }

                EditorGUILayout.LabelField("Acquire / Release", $"{profile.CommitmentAcquireSeconds:0.00}s / {profile.CommitmentReleaseSeconds:0.00}s");
                EditorGUILayout.LabelField("Hold Budget", $"{profile.MaxContinuousHoldSeconds:0.00}s");
                EditorGUILayout.LabelField("Focus Smoothing", $"{profile.FocusPositionLerpSpeed:0.0}");
            });
        }

        private void DrawDiscoverySection(int providerCount)
        {
            _showDiscovery = DrawSection(SectionDiscovery, "FOCUS SOURCES", _showDiscovery, ConvaiInspectorIconIds.Discovery);
            if (!_showDiscovery) return;

            DrawSectionBody(() =>
            {
                EditorGUILayout.PropertyField(_discoverProvidersInHierarchy);
                EditorGUILayout.PropertyField(_autoCreateDefaultFocusProvider);
                EditorGUILayout.Space(2f);
                EditorGUILayout.LabelField("Available Focus Sources", providerCount.ToString());
            });
        }

        private void DrawLiveSection(ConvaiAttentionController controller, EmbodimentContext context)
        {
            _showLive = DrawSection(SectionLive, "LIVE ATTENTION", _showLive, ConvaiInspectorIconIds.Live, Info);
            if (!_showLive) return;

            DrawSectionBody(() =>
            {
                if (!UnityEngine.Application.isPlaying)
                {
                    DrawOfflinePlaceholder();
                    return;
                }

                AttentionReading reading = controller.Current;
                EditorGUILayout.BeginHorizontal();
                DrawLiveCell("Target", reading.Target != null ? reading.Target.name : reading.IsValid ? "World Point" : "None", DefaultValueColor, 120);
                DrawLiveCell("Commitment", reading.Commitment.ToString("0.00"), reading.Commitment > 0.1f ? AccentEmphasis : IdleColor, 110);
                DrawLiveCell("Generation", reading.TargetGenerationId.ToString(), DefaultValueColor, 100);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField("Smoothed Point", reading.IsValid ? FormatVector3(reading.SmoothedPoint) : "-");
                EditorGUILayout.LabelField("Attention Source", context?.AttentionSource != null ? context.AttentionSource.GetType().Name : "Not registered");
            });
        }

        private void DrawValidationSection(ConvaiEmbodimentContextEditorInfo contextInfo, int providerCount)
        {
            _showValidation = DrawSection(SectionValidation, "VALIDATION", _showValidation, ConvaiInspectorIconIds.Validation, Warning);
            if (!_showValidation) return;

            DrawSectionBody(() => DrawValidationSummary(contextInfo, providerCount, true));
        }

        private void DrawValidationSummary(ConvaiEmbodimentContextEditorInfo contextInfo, int providerCount, bool includeInfo)
        {
            DrawContextResolutionSummary(contextInfo, includeInfo, "component");
            if (providerCount == 0 && !_autoCreateDefaultFocusProvider.boolValue)
                DrawWarningBox("No Focus Provider", "No IFocusTargetProvider was found and automatic default focus creation is disabled.");
        }

        private int CountFocusProviders(ConvaiAttentionController controller, ConvaiEmbodimentContextEditorInfo contextInfo)
        {
            if (controller == null) return 0;

            Transform root = _discoverProvidersInHierarchy.boolValue
                ? (contextInfo.RuntimeRoot != null ? contextInfo.RuntimeRoot : controller.transform.root)
                : controller.transform;
            if (root == null) return 0;

            MonoBehaviour[] components = _discoverProvidersInHierarchy.boolValue
                ? root.GetComponentsInChildren<MonoBehaviour>(true)
                : root.GetComponents<MonoBehaviour>();
            int count = 0;
            for (int i = 0; i < components.Length; i++)
                if (components[i] is IFocusTargetProvider)
                    count++;
            return count;
        }
    }

    [CustomEditor(typeof(DefaultFocusTargetProvider))]
    internal sealed class DefaultFocusTargetProviderEditor : BrandedEmbodimentComponentEditor { }

    [CustomEditor(typeof(ConvaiEmotionController))]
    internal sealed class ConvaiEmotionControllerEditor : ConvaiPremiumInspectorEditor
    {
        private static readonly string[] DefaultEmotionLabels =
        {
            "neutral",
            "joy",
            "trust",
            "fear",
            "surprise",
            "sadness",
            "disgust",
            "anger",
            "anticipation"
        };

        private const string SectionProfile = "ProfileOverride";
        private const string SectionRouting = "OutputRouting";
        private const string SectionLive = "LiveEmotion";
        private const string SectionValidation = "Validation";

        private SerializedProperty _detectionMode;
        private SerializedProperty _profile;
        private SerializedProperty _lockEmotion;
        private SerializedProperty _lockedEmotionLabel;
        private SerializedProperty _lockedIntensity;
        private bool _showProfile;
        private bool _showRouting;
        private bool _showLive;
        private bool _showValidation;

        protected override void OnEnable()
        {
            base.OnEnable();
            _detectionMode = serializedObject.FindProperty("detectionMode");
            _profile = serializedObject.FindProperty("profile");
            _lockEmotion = serializedObject.FindProperty("lockEmotion");
            _lockedEmotionLabel = serializedObject.FindProperty("lockedEmotionLabel");
            _lockedIntensity = serializedObject.FindProperty("lockedIntensity");
            _showProfile = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionProfile, true);
            _showRouting = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionRouting, true);
            _showLive = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionLive, true);
            _showValidation = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionValidation, true);
        }

        protected override void OnDisable()
        {
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionProfile, _showProfile);
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionRouting, _showRouting);
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionLive, _showLive);
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionValidation, _showValidation);
            base.OnDisable();
        }

        public override void OnInspectorGUI()
        {
            EnsurePremiumStyles();
            serializedObject.Update();

            var controller = (ConvaiEmotionController)target;
            ConvaiEmbodimentContextEditorInfo contextInfo = ConvaiEmbodimentContextEditorResolver.Resolve(controller);
            EmbodimentContext context = contextInfo.Context;
            string status = UnityEngine.Application.isPlaying ? "Live" : "Editor";
            Color statusColor = UnityEngine.Application.isPlaying ? AccentEmphasis : IdleColor;
            if (!contextInfo.HasSupportedCharacterScope || _lockEmotion.boolValue)
            {
                status = !contextInfo.HasSupportedCharacterScope ? "Warning" : "Locked";
                statusColor = !contextInfo.HasSupportedCharacterScope ? Warning : Info;
            }
            else if (!UnityEngine.Application.isPlaying && contextInfo.WillAutoCreateOnCharacter)
            {
                status = "Auto";
                statusColor = AccentEmphasis;
            }

            DrawPremiumHeader("Emotion Controller", "Drives facial expression from emotion", status, statusColor);

            DrawValidationSummary(contextInfo, false);
            DrawInfoBox(
                "What this does",
                "Converts Convai emotion state into expression output. Use the lock controls only when testing or forcing a specific expression.");
            DrawProfileAndOverrideSection();
            DrawOutputRoutingSection(contextInfo);
            DrawLiveEmotionSection(controller, context);
            DrawValidationSection(contextInfo);

            serializedObject.ApplyModifiedProperties();
            if (UnityEngine.Application.isPlaying) Repaint();
        }

        private void DrawProfileAndOverrideSection()
        {
            _showProfile = DrawSection(SectionProfile, "PROFILE & OVERRIDE", _showProfile, ConvaiInspectorIconIds.Profile);
            if (!_showProfile) return;

            DrawSectionBody(() =>
            {
                EditorGUILayout.PropertyField(_detectionMode, new GUIContent("Detection Mode"));
                if (_detectionMode.enumValueIndex == 0)
                    DrawInfoBox(
                        "Emotions Off",
                        "This character won't request emotion detection, so no bot-emotion frames are received. " +
                        "Choose LLM or NRCLex to enable. (With no emotion controller attached, emotions are off too.)");
                EditorGUILayout.Space(6f);

                EditorGUILayout.PropertyField(_profile, new GUIContent("Emotion Profile"));
                var profile = _profile.objectReferenceValue as ConvaiEmotionProfile;
                if (profile == null)
                    DrawInfoBox("SDK Default Active", "No profile asset is assigned. This component still works and uses the SDK emotion defaults at runtime.");
                else
                {
                    EditorGUILayout.LabelField("Taxonomy", ObjectStatus(profile.Taxonomy));
                    EditorGUILayout.LabelField("Smoothing", $"Lerp {profile.LerpSpeed:0.0} / Decay {profile.DecaySpeed:0.0}");
                    EditorGUILayout.LabelField("Micro Burst", profile.MicroBurstEnabled ? $"{profile.MicroBurstDuration:0.00}s x{profile.MicroBurstOvershoot:0.00}" : "Disabled");
                }

                EditorGUILayout.Space(4f);
                EditorGUILayout.PropertyField(_lockEmotion);
                if (_lockEmotion.boolValue)
                {
                    EditorGUI.indentLevel++;
                    DrawLockedEmotionPopup(profile);
                    EditorGUILayout.PropertyField(_lockedIntensity, new GUIContent("Locked Intensity"));
                    EditorGUI.indentLevel--;
                }
            });
        }

        private void DrawLockedEmotionPopup(ConvaiEmotionProfile profile)
        {
            string[] labels = BuildEmotionLabels(profile, _lockedEmotionLabel.stringValue);
            int currentIndex = IndexOfLabel(labels, _lockedEmotionLabel.stringValue);
            if (currentIndex < 0)
                currentIndex = IndexOfLabel(labels, "neutral");
            if (currentIndex < 0)
                currentIndex = 0;

            int nextIndex = EditorGUILayout.Popup(new GUIContent("Locked Emotion"), currentIndex, labels);
            if (nextIndex >= 0 && nextIndex < labels.Length)
                _lockedEmotionLabel.stringValue = labels[nextIndex];
        }

        private static string[] BuildEmotionLabels(ConvaiEmotionProfile profile, string currentLabel)
        {
            EmotionTaxonomyAsset taxonomy = profile != null ? profile.Taxonomy : null;
            if (taxonomy == null)
                return IncludeCurrentLabel(DefaultEmotionLabels, currentLabel);

            taxonomy.EnsureBuilt();
            IReadOnlyList<EmotionDescriptor> emotions = taxonomy.Emotions;
            if (emotions == null || emotions.Count == 0)
                return IncludeCurrentLabel(DefaultEmotionLabels, currentLabel);

            var labels = new List<string>(emotions.Count + 1);
            for (int i = 0; i < emotions.Count; i++)
            {
                EmotionDescriptor descriptor = emotions[i];
                if (descriptor == null || string.IsNullOrWhiteSpace(descriptor.Label)) continue;
                if (IndexOfLabel(labels, descriptor.Label) < 0)
                    labels.Add(descriptor.Label);
            }

            if (labels.Count == 0)
                labels.AddRange(DefaultEmotionLabels);
            return IncludeCurrentLabel(labels, currentLabel);
        }

        private static string[] IncludeCurrentLabel(IReadOnlyList<string> labels, string currentLabel)
        {
            if (string.IsNullOrWhiteSpace(currentLabel) || IndexOfLabel(labels, currentLabel) >= 0)
                return ToArray(labels);

            var merged = new List<string>(labels.Count + 1);
            for (int i = 0; i < labels.Count; i++)
                merged.Add(labels[i]);
            merged.Add(currentLabel.Trim());
            return merged.ToArray();
        }

        private static string[] ToArray(IReadOnlyList<string> labels)
        {
            if (labels is string[] array) return array;
            var result = new string[labels.Count];
            for (int i = 0; i < labels.Count; i++)
                result[i] = labels[i];
            return result;
        }

        private static int IndexOfLabel(IReadOnlyList<string> labels, string label)
        {
            if (labels == null || string.IsNullOrWhiteSpace(label)) return -1;
            for (int i = 0; i < labels.Count; i++)
                if (string.Equals(labels[i], label, System.StringComparison.OrdinalIgnoreCase))
                    return i;
            return -1;
        }

        private void DrawOutputRoutingSection(ConvaiEmbodimentContextEditorInfo contextInfo)
        {
            _showRouting = DrawSection(SectionRouting, "EXPRESSION OUTPUT", _showRouting, ConvaiInspectorIconIds.Routing);
            if (!_showRouting) return;

            DrawSectionBody(() =>
            {
                EmbodimentContext context = contextInfo.Context;
                var profile = _profile.objectReferenceValue as ConvaiEmotionProfile;
                int blendshapeSlots = profile?.BlendshapeBinding?.Slots?.Count ?? 0;
                int animatorSlots = profile?.AnimatorBinding?.Slots?.Count ?? 0;
                EditorGUILayout.LabelField("Blendshape Slots", blendshapeSlots.ToString());
                EditorGUILayout.LabelField("Animator Slots", animatorSlots.ToString());
                EditorGUILayout.LabelField("Context", contextInfo.ContextStatus);
                EditorGUILayout.LabelField("Runtime Root", contextInfo.RuntimeRootName);
                EditorGUILayout.LabelField("Rig Binding", RuntimeDependencyStatus(contextInfo, context?.RigBinding != null));
                EditorGUILayout.LabelField("Compositor", RuntimeDependencyStatus(contextInfo, context?.Compositor != null));
                EditorGUILayout.LabelField("Animator Conductor", RuntimeDependencyStatus(contextInfo, context?.AnimatorConductor != null));
            });
        }

        private void DrawLiveEmotionSection(ConvaiEmotionController controller, EmbodimentContext context)
        {
            _showLive = DrawSection(SectionLive, "LIVE EMOTION", _showLive, ConvaiInspectorIconIds.Live, Info);
            if (!_showLive) return;

            DrawSectionBody(() =>
            {
                if (!UnityEngine.Application.isPlaying)
                {
                    DrawOfflinePlaceholder();
                    return;
                }

                EmotionReading current = controller.Current;
                EditorGUILayout.BeginHorizontal();
                DrawLiveCell("Dominant", current.DominantLabel, current.IsNeutral ? IdleColor : AccentEmphasis, 120, !current.IsNeutral);
                DrawLiveCell("Score", current.DominantScore.ToString("0.00"), current.DominantScore > 0.1f ? AccentEmphasis : IdleColor, 90);
                DrawLiveCell("Mouth", current.MouthInfluence.ToString("0.00"), current.MouthInfluence > 0.1f ? Info : IdleColor, 90);
                DrawLiveCell("Hold", $"{current.DominantHoldSeconds:0.0}s", DefaultValueColor, 90);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField("Score Count", current.AllScores != null ? current.AllScores.Count.ToString() : "0");
                EditorGUILayout.LabelField("Emotion Source", context?.EmotionStateSource != null ? context.EmotionStateSource.GetType().Name : "Not registered");
            });
        }

        private void DrawValidationSection(ConvaiEmbodimentContextEditorInfo contextInfo)
        {
            _showValidation = DrawSection(SectionValidation, "VALIDATION", _showValidation, ConvaiInspectorIconIds.Validation, Warning);
            if (!_showValidation) return;

            DrawSectionBody(() => DrawValidationSummary(contextInfo, true));
        }

        private void DrawValidationSummary(ConvaiEmbodimentContextEditorInfo contextInfo, bool includeInfo)
        {
            DrawContextResolutionSummary(contextInfo, includeInfo, "component");
        }
    }

    [CustomEditor(typeof(ConvaiGazeCoordinator))]
    internal sealed class ConvaiGazeCoordinatorEditor : ConvaiPremiumInspectorEditor
    {
        private const string SectionProfile = "Profile";
        private const string SectionLive = "LiveGaze";
        private const string SectionValidation = "Validation";

        private SerializedProperty _profile;
        private bool _showProfile;
        private bool _showLive;
        private bool _showValidation;

        protected override void OnEnable()
        {
            base.OnEnable();
            _profile = serializedObject.FindProperty("profile");
            _showProfile = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionProfile, true);
            _showLive = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionLive, true);
            _showValidation = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionValidation, true);
        }

        protected override void OnDisable()
        {
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionProfile, _showProfile);
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionLive, _showLive);
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionValidation, _showValidation);
            base.OnDisable();
        }

        public override void OnInspectorGUI()
        {
            EnsurePremiumStyles();
            serializedObject.Update();

            var coordinator = (ConvaiGazeCoordinator)target;
            ConvaiEmbodimentContextEditorInfo contextInfo = ConvaiEmbodimentContextEditorResolver.Resolve(coordinator);
            EmbodimentContext context = contextInfo.Context;
            string status = UnityEngine.Application.isPlaying ? "Live" : "Editor";
            Color statusColor = UnityEngine.Application.isPlaying ? AccentEmphasis : IdleColor;
            if (!contextInfo.HasSupportedCharacterScope)
            {
                status = "Warning";
                statusColor = Warning;
            }

            DrawPremiumHeader("Gaze Coordinator", "Attention to Intent", status, statusColor);
            DrawValidationSummary(contextInfo, false);
            DrawInfoBox(
                "What this does",
                "Combines the current attention target and conversation phase into a GazeIntent consumed by Eye Gaze and Head Look actuators.");
            DrawProfileSection();
            DrawLiveSection(context);
            DrawValidationSection(contextInfo);

            serializedObject.ApplyModifiedProperties();
            if (UnityEngine.Application.isPlaying) Repaint();
        }

        private void DrawProfileSection()
        {
            _showProfile = DrawSection(SectionProfile, "PROFILE", _showProfile, ConvaiInspectorIconIds.Profile);
            if (!_showProfile) return;

            DrawSectionBody(() =>
            {
                EditorGUILayout.PropertyField(_profile, new GUIContent("Gaze Coordinator Profile"));
                if (_profile.objectReferenceValue == null)
                    DrawInfoBox("SDK Default Active", "No profile asset is assigned. This component still works and uses the SDK gaze coordination defaults at runtime.");
            });
        }

        private void DrawLiveSection(EmbodimentContext context)
        {
            _showLive = DrawSection(SectionLive, "LIVE GAZE INTENT", _showLive, ConvaiInspectorIconIds.Live, Info);
            if (!_showLive) return;

            DrawSectionBody(() =>
            {
                if (!UnityEngine.Application.isPlaying)
                {
                    DrawOfflinePlaceholder();
                    return;
                }

                GazeIntent intent = context?.GazeIntentProvider?.Current ?? GazeIntent.Relaxed;
                EditorGUILayout.BeginHorizontal();
                DrawLiveCell("Weight", intent.OverallWeight.ToString("0.00"), intent.OverallWeight > 0.1f ? AccentEmphasis : IdleColor, 110);
                DrawLiveCell("Eye Share", intent.EyeShare.ToString("0.00"), Info, 110);
                DrawLiveCell("Head Share", (1f - intent.EyeShare).ToString("0.00"), Info, 110);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField("Target Point", FormatVector3(intent.WorldTargetPoint));
            });
        }

        private void DrawValidationSection(ConvaiEmbodimentContextEditorInfo contextInfo)
        {
            _showValidation = DrawSection(SectionValidation, "VALIDATION", _showValidation, ConvaiInspectorIconIds.Validation, Warning);
            if (!_showValidation) return;

            DrawSectionBody(() => DrawValidationSummary(contextInfo, true));
        }

        private void DrawValidationSummary(ConvaiEmbodimentContextEditorInfo contextInfo, bool includeInfo)
        {
            DrawContextResolutionSummary(contextInfo, includeInfo, "coordinator");
        }
    }

    [CustomEditor(typeof(ConvaiEyeGazeActuator))]
    internal sealed class ConvaiEyeGazeActuatorEditor : ConvaiPremiumInspectorEditor
    {
        private const string SectionProfile = "Profile";
        private const string SectionOcularMotion = "OcularMotion";
        private const string SectionBlink = "BlinkMicroMotion";
        private const string SectionLive = "LiveGaze";
        private const string SectionValidation = "Validation";

        private SerializedProperty _profile;
        private bool _showProfile;
        private bool _showOcularMotion;
        private bool _showBlink;
        private bool _showLive;
        private bool _showValidation;

        protected override void OnEnable()
        {
            base.OnEnable();
            _profile = serializedObject.FindProperty("profile");
            _showProfile = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionProfile, true);
            _showOcularMotion = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionOcularMotion, true);
            _showBlink = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionBlink, true);
            _showLive = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionLive, true);
            _showValidation = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionValidation, true);
        }

        protected override void OnDisable()
        {
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionProfile, _showProfile);
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionOcularMotion, _showOcularMotion);
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionBlink, _showBlink);
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionLive, _showLive);
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionValidation, _showValidation);
            base.OnDisable();
        }

        public override void OnInspectorGUI()
        {
            EnsurePremiumStyles();
            serializedObject.Update();

            var actuator = (ConvaiEyeGazeActuator)target;
            ConvaiEmbodimentContextEditorInfo contextInfo = ConvaiEmbodimentContextEditorResolver.Resolve(actuator);
            EmbodimentContext context = contextInfo.Context;
            string status = UnityEngine.Application.isPlaying ? "Live" : "Editor";
            Color statusColor = UnityEngine.Application.isPlaying ? AccentEmphasis : IdleColor;
            if (!contextInfo.HasSupportedCharacterScope)
            {
                status = "Warning";
                statusColor = Warning;
            }
            else if (!UnityEngine.Application.isPlaying &&
                     (contextInfo.WillAutoCreateOnCharacter || context?.RigBinding == null))
            {
                status = "Auto";
                statusColor = AccentEmphasis;
            }

            DrawPremiumHeader("Eye Gaze", "Procedural Eye Motion", status, statusColor);
            DrawValidationSummary(contextInfo, false);
            DrawInfoBox(
                "What this does",
                "Adds natural eye movement, blinking, and small eye motion. It follows the character's current gaze target when available.");
            DrawProfileSection();
            DrawOcularMotionSection();
            DrawBlinkSection();
            DrawLiveGazeSection(context);
            DrawValidationSection(contextInfo);

            serializedObject.ApplyModifiedProperties();
            if (UnityEngine.Application.isPlaying) Repaint();
        }

        private void DrawProfileSection()
        {
            _showProfile = DrawSection(SectionProfile, "PROFILE", _showProfile, ConvaiInspectorIconIds.Profile);
            if (!_showProfile) return;

            DrawSectionBody(() =>
            {
                EditorGUILayout.PropertyField(_profile, new GUIContent("Eye Gaze Profile"));
                if (_profile.objectReferenceValue == null)
                    DrawInfoBox("SDK Default Active", "No profile asset is assigned. This component still works and uses the SDK eye gaze defaults at runtime.");
            });
        }

        private void DrawOcularMotionSection()
        {
            _showOcularMotion = DrawSection(SectionOcularMotion, "EYE MOVEMENT", _showOcularMotion, ConvaiInspectorIconIds.Motion);
            if (!_showOcularMotion) return;

            DrawSectionBody(() =>
            {
                var profile = _profile.objectReferenceValue as ConvaiGazeEyeProfile;
                if (profile == null)
                {
                    DrawInfoBox("Profile Summary", "Assign an Eye Gaze Profile asset to inspect custom eye movement values here. Without one, the SDK defaults are used.");
                    return;
                }

                EditorGUILayout.LabelField("Tracking Sharpness", profile.TrackingSharpness.ToString("0.0"));
                EditorGUILayout.LabelField("Yaw / Pitch Limits", $"{profile.MaxYawDegrees:0.#} / {profile.MaxPitchDegrees:0.#} deg");
                EditorGUILayout.LabelField("Idle Exploration", profile.EnableIdleExploration ? $"{profile.IdleExplorationWeight:0.00} weight" : "Disabled");
            });
        }

        private void DrawBlinkSection()
        {
            _showBlink = DrawSection(SectionBlink, "BLINK & MICRO MOTION", _showBlink, ConvaiInspectorIconIds.Blink);
            if (!_showBlink) return;

            DrawSectionBody(() =>
            {
                var profile = _profile.objectReferenceValue as ConvaiGazeEyeProfile;
                if (profile == null)
                {
                    DrawInfoBox("Profile Summary", "Assign an Eye Gaze Profile asset to inspect custom blink and micro-motion values here. Without one, the SDK defaults are used.");
                    return;
                }

                EditorGUILayout.LabelField("Saccades", profile.EnableSaccades ? $"{profile.SaccadeIntervalMean:0.00}s mean, {profile.SaccadeMaxDegrees:0.0} deg" : "Disabled");
                EditorGUILayout.LabelField("Micro Tremor", profile.EnableMicroTremor ? $"{profile.MicroTremorAmplitude:0.00} deg @ {profile.MicroTremorFrequency:0.#} Hz" : "Disabled");
                EditorGUILayout.LabelField("Blink", profile.EnableBlink ? $"{profile.BlinkIntervalMean:0.00}s mean, {profile.BlinkCycleDuration:0.00}s cycle" : "Disabled");
            });
        }

        private void DrawLiveGazeSection(EmbodimentContext context)
        {
            _showLive = DrawSection(SectionLive, "LIVE GAZE", _showLive, ConvaiInspectorIconIds.Live, Info);
            if (!_showLive) return;

            DrawSectionBody(() =>
            {
                if (!UnityEngine.Application.isPlaying)
                {
                    DrawOfflinePlaceholder();
                    return;
                }

                GazeIntent intent = context?.GazeIntentProvider?.Current ?? GazeIntent.Relaxed;
                EditorGUILayout.BeginHorizontal();
                DrawLiveCell("Weight", intent.OverallWeight.ToString("0.00"), intent.OverallWeight > 0.1f ? AccentEmphasis : IdleColor, 110);
                DrawLiveCell("Eye Weight", intent.EyeShare.ToString("0.00"), Info, 110);
                DrawLiveCell("Head Weight", (1f - intent.EyeShare).ToString("0.00"), Info, 110);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField("Target Point", FormatVector3(intent.WorldTargetPoint));
                EditorGUILayout.LabelField("Gaze Source", context?.GazeIntentProvider != null ? context.GazeIntentProvider.GetType().Name : "Not registered");
            });
        }

        private void DrawValidationSection(ConvaiEmbodimentContextEditorInfo contextInfo)
        {
            _showValidation = DrawSection(SectionValidation, "VALIDATION", _showValidation, ConvaiInspectorIconIds.Validation, Warning);
            if (!_showValidation) return;

            DrawSectionBody(() => DrawValidationSummary(contextInfo, true));
        }

        private void DrawValidationSummary(ConvaiEmbodimentContextEditorInfo contextInfo, bool includeInfo)
        {
            DrawContextResolutionSummary(contextInfo, includeInfo, "actuator");
        }
    }

    [CustomEditor(typeof(ConvaiHeadLookActuator))]
    internal sealed class ConvaiHeadLookActuatorEditor : ConvaiPremiumInspectorEditor
    {
        private const string SectionProfile = "Profile";
        private const string SectionRange = "RangeDistribution";
        private const string SectionLive = "LiveGaze";
        private const string SectionValidation = "Validation";

        private SerializedProperty _profile;
        private bool _showProfile;
        private bool _showRange;
        private bool _showLive;
        private bool _showValidation;

        protected override void OnEnable()
        {
            base.OnEnable();
            _profile = serializedObject.FindProperty("profile");
            _showProfile = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionProfile, true);
            _showRange = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionRange, true);
            _showLive = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionLive, true);
            _showValidation = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionValidation, true);
        }

        protected override void OnDisable()
        {
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionProfile, _showProfile);
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionRange, _showRange);
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionLive, _showLive);
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionValidation, _showValidation);
            base.OnDisable();
        }

        public override void OnInspectorGUI()
        {
            EnsurePremiumStyles();
            serializedObject.Update();

            var actuator = (ConvaiHeadLookActuator)target;
            ConvaiEmbodimentContextEditorInfo contextInfo = ConvaiEmbodimentContextEditorResolver.Resolve(actuator);
            EmbodimentContext context = contextInfo.Context;
            string status = UnityEngine.Application.isPlaying ? "Live" : "Editor";
            Color statusColor = UnityEngine.Application.isPlaying ? AccentEmphasis : IdleColor;
            if (!contextInfo.HasSupportedCharacterScope)
            {
                status = "Warning";
                statusColor = Warning;
            }
            else if (!UnityEngine.Application.isPlaying &&
                     (contextInfo.WillAutoCreateOnCharacter || context?.RigBinding == null))
            {
                status = "Auto";
                statusColor = AccentEmphasis;
            }

            DrawPremiumHeader("Head Look", "Procedural Head Motion", status, statusColor);
            DrawValidationSummary(contextInfo, false);
            DrawInfoBox(
                "What this does",
                "Adds natural head and upper-body looking motion. It follows the same gaze target as Eye Gaze when available.");
            DrawProfileSection();
            DrawRangeSection();
            DrawLiveSection(context, actuator);
            DrawValidationSection(contextInfo);

            serializedObject.ApplyModifiedProperties();
            if (UnityEngine.Application.isPlaying) Repaint();
        }

        private void DrawProfileSection()
        {
            _showProfile = DrawSection(SectionProfile, "PROFILE", _showProfile, ConvaiInspectorIconIds.Profile);
            if (!_showProfile) return;

            DrawSectionBody(() =>
            {
                EditorGUILayout.PropertyField(_profile, new GUIContent("Head Look Profile"));
                if (_profile.objectReferenceValue == null)
                    DrawInfoBox("SDK Default Active", "No profile asset is assigned. This component still works and uses the SDK head look defaults at runtime.");
            });
        }

        private void DrawRangeSection()
        {
            _showRange = DrawSection(SectionRange, "HEAD MOVEMENT LIMITS", _showRange, ConvaiInspectorIconIds.Range);
            if (!_showRange) return;

            DrawSectionBody(() =>
            {
                var profile = _profile.objectReferenceValue as ConvaiGazeHeadProfile;
                if (profile == null)
                {
                    DrawInfoBox("Profile Summary", "Assign a Head Look Profile asset to inspect custom head movement limits here. Without one, the SDK defaults are used.");
                    return;
                }

                EditorGUILayout.LabelField("Neck Yaw / Pitch", $"{profile.MaxNeckYaw:0.#} / {profile.MaxNeckPitch:0.#} deg");
                EditorGUILayout.LabelField("Head Yaw / Pitch", $"{profile.MaxHeadYaw:0.#} / {profile.MaxHeadPitch:0.#} deg");
                EditorGUILayout.LabelField("Smoothing / Deadzone", $"{profile.SmoothingSharpness:0.0} / {profile.DeadzoneDegrees:0.0} deg");
                EditorGUILayout.LabelField("Idle Exploration", profile.EnableIdleExploration
                    ? $"{profile.IdleExplorationYawDegrees:0.#} deg yaw"
                    : "Disabled");
                EditorGUILayout.LabelField("Upper Body Follow", profile.EnableUpperBodyFollow
                    ? $"starts {profile.UpperBodyActivationDegrees:0.#} deg"
                    : "Disabled");
                EditorGUILayout.LabelField("Neck Share", profile.NeckShare.ToString("0.00"));
                EditorGUILayout.LabelField("Minimum Head Contribution", profile.MinimumHeadContribution.ToString("0.00"));
            });
        }

        private void DrawLiveSection(EmbodimentContext context, ConvaiHeadLookActuator actuator)
        {
            _showLive = DrawSection(SectionLive, "LIVE GAZE", _showLive, ConvaiInspectorIconIds.Live, Info);
            if (!_showLive) return;

            DrawSectionBody(() =>
            {
                if (!UnityEngine.Application.isPlaying)
                {
                    DrawOfflinePlaceholder();
                    return;
                }

                GazeIntent intent = context?.GazeIntentProvider?.Current ?? GazeIntent.Relaxed;
                EditorGUILayout.BeginHorizontal();
                DrawLiveCell("Weight", intent.OverallWeight.ToString("0.00"), intent.OverallWeight > 0.1f ? AccentEmphasis : IdleColor, 110);
                DrawLiveCell("Eye Weight", intent.EyeShare.ToString("0.00"), Info, 110);
                DrawLiveCell("Head Weight", (1f - intent.EyeShare).ToString("0.00"), AccentEmphasis, 110);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                DrawLiveCell("Head Authority", actuator.CurrentHeadAuthority.ToString("0.00"),
                    actuator.CurrentHeadAuthority > 0.1f ? AccentEmphasis : IdleColor, 110);
                DrawLiveCell("Idle Explore", actuator.IsIdleExploring ? "On" : "Off",
                    actuator.IsIdleExploring ? Info : IdleColor, 110);
                Vector2 solved = actuator.CurrentSolvedAngles;
                DrawLiveCell("Solved Y/P", $"{solved.x:0.0} / {solved.y:0.0}", Info, 110);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField("Target Point", FormatVector3(intent.WorldTargetPoint));
                EditorGUILayout.LabelField("Gaze Source", context?.GazeIntentProvider != null ? context.GazeIntentProvider.GetType().Name : "Not registered");
            });
        }

        private void DrawValidationSection(ConvaiEmbodimentContextEditorInfo contextInfo)
        {
            _showValidation = DrawSection(SectionValidation, "VALIDATION", _showValidation, ConvaiInspectorIconIds.Validation, Warning);
            if (!_showValidation) return;

            DrawSectionBody(() => DrawValidationSummary(contextInfo, true));
        }

        private void DrawValidationSummary(ConvaiEmbodimentContextEditorInfo contextInfo, bool includeInfo)
        {
            DrawContextResolutionSummary(contextInfo, includeInfo, "actuator");
        }
    }

    [CustomEditor(typeof(ConvaiFacialClipPlayer))]
    internal sealed class ConvaiFacialClipPlayerEditor : ConvaiPremiumInspectorEditor
    {
        private const string SectionClip = "Clip";
        private const string SectionAdvanced = "Advanced";
        private const string SectionValidation = "Validation";

        private SerializedProperty _profile;
        private SerializedProperty _playbackSpeed;
        private SerializedProperty _globalWeight;
        private SerializedProperty _targetMeshes;
        private ReorderableList _targetMeshesList;
        private GUIStyle _meshCountStyle;
        private readonly List<SkinnedMeshRenderer> _assignedMeshes = new();
        private readonly HashSet<EntityId> _seenMeshIds = new();
        private readonly HashSet<string> _uniqueBlendshapes = new();
        private bool _showClip;
        private bool _showAdvanced;
        private bool _showValidation;

        protected override void OnEnable()
        {
            base.OnEnable();
            _profile = serializedObject.FindProperty("profile");
            _playbackSpeed = serializedObject.FindProperty("playbackSpeed");
            _globalWeight = serializedObject.FindProperty("globalWeight");
            _targetMeshes = serializedObject.FindProperty("targetMeshes");
            _showClip = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionClip, true);
            _showAdvanced = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionAdvanced, false);
            _showValidation = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionValidation, true);
        }

        protected override void OnDisable()
        {
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionClip, _showClip);
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionAdvanced, _showAdvanced);
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionValidation, _showValidation);
            base.OnDisable();
        }

        public override void OnInspectorGUI()
        {
            EnsurePremiumStyles();
            EnsureMeshList();
            serializedObject.Update();
            PopulateAssignedMeshes();

            DrawPremiumHeader("Baked Facial Clip Source", "Facial Blendshape Source", "Editor", IdleColor);
            DrawInfoBox(
                "What this does",
                "Plays a baked facial animation profile into the facial blendshape system. Use it for authored expressions or looping facial motion.");
            DrawClipSection();
            DrawAdvancedSection();
            DrawValidationSection();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawClipSection()
        {
            _showClip = DrawSection(SectionClip, "FACIAL CLIP", _showClip, ConvaiInspectorIconIds.Content);
            if (!_showClip) return;

            DrawSectionBody(() =>
            {
                EditorGUILayout.PropertyField(_profile, new GUIContent("Facial Animation Profile"));
                EditorGUILayout.PropertyField(_playbackSpeed, new GUIContent("Playback Speed"));
                EditorGUILayout.PropertyField(_globalWeight, new GUIContent("Overall Weight"));
            });
        }

        private void DrawAdvancedSection()
        {
            _showAdvanced = DrawSection(SectionAdvanced, "TARGET MESHES", _showAdvanced, ConvaiInspectorIconIds.Routing);
            if (!_showAdvanced) return;

            DrawSectionBody(() =>
            {
                _targetMeshesList.DoLayoutList();

                int blendshapeCount = _assignedMeshes.Count > 0 ? GetTotalUniqueBlendshapeCount() : 0;
                EditorGUILayout.BeginHorizontal();
                if (_assignedMeshes.Count > 0)
                {
                    Color previous = GUI.color;
                    GUI.color = blendshapeCount > 0 ? AccentEmphasis : Warning;
                    string icon = blendshapeCount > 0 ? "OK" : "!";
                    GUILayout.Label($"{icon} {_assignedMeshes.Count} Meshes Found ({blendshapeCount} Blendshapes)",
                        EditorStyles.miniLabel);
                    GUI.color = previous;
                }
                else
                {
                    GUILayout.Label("No meshes assigned. Runtime will auto-discover meshes under the character root.",
                        EditorStyles.miniLabel);
                }

                GUILayout.FlexibleSpace();
                Color previousBackground = GUI.backgroundColor;
                GUI.backgroundColor = Accent;
                if (GUILayout.Button("Auto-Find", MiniButtonStyle, GUILayout.Width(80)))
                    AutoFindMeshesInHierarchy();
                GUI.backgroundColor = previousBackground;
                EditorGUILayout.EndHorizontal();
            });
        }

        private void DrawValidationSection()
        {
            _showValidation = DrawSection(SectionValidation, "VALIDATION", _showValidation, ConvaiInspectorIconIds.Validation, Warning);
            if (!_showValidation) return;

            DrawSectionBody(() =>
            {
                if (_profile.objectReferenceValue == null)
                    DrawWarningBox("Profile Missing", "Assign a Facial Animation Profile to send baked blendshape motion at runtime.");
                else
                    DrawInfoBox("Profile Ready", "The assigned profile will play through the facial blendshape system at runtime.");

                if (_targetMeshes.isArray && _targetMeshes.arraySize == 0)
                    DrawInfoBox("Target Meshes", "No mesh overrides are assigned, so runtime will auto-discover meshes under the character root.");
            });
        }

        private void EnsureMeshList()
        {
            if (_meshCountStyle == null)
            {
                _meshCountStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
            }

            if (_targetMeshesList != null) return;

            _targetMeshesList = new ReorderableList(serializedObject, _targetMeshes, true, true, true, true);
            _targetMeshesList.drawHeaderCallback = rect =>
            {
                EditorGUI.LabelField(rect,
                    new GUIContent("Target Meshes", "Skinned mesh renderers that receive baked facial blendshape animation."));
            };
            _targetMeshesList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                SerializedProperty element = _targetMeshes.GetArrayElementAtIndex(index);
                rect.y += 2f;

                var objectRect = new Rect(rect.x, rect.y, rect.width - 110f, EditorGUIUtility.singleLineHeight);
                EditorGUI.PropertyField(objectRect, element, GUIContent.none);

                var countRect = new Rect(rect.xMax - 110f, rect.y, 110f, EditorGUIUtility.singleLineHeight);
                var meshRenderer = element.objectReferenceValue as SkinnedMeshRenderer;
                if (meshRenderer != null && meshRenderer.sharedMesh != null)
                {
                    int blendshapeCount = meshRenderer.sharedMesh.blendShapeCount;
                    _meshCountStyle.normal.textColor = blendshapeCount > 0 ? AccentEmphasis : Warning;
                    EditorGUI.LabelField(countRect, blendshapeCount > 0 ? $"{blendshapeCount} blendshapes" : "0 blendshapes", _meshCountStyle);
                }
                else if (meshRenderer != null)
                {
                    _meshCountStyle.normal.textColor = Warning;
                    EditorGUI.LabelField(countRect, "No Mesh Data", _meshCountStyle);
                }
                else
                {
                    _meshCountStyle.normal.textColor = IdleColor;
                    EditorGUI.LabelField(countRect, "Empty", _meshCountStyle);
                }
            };
        }

        private void PopulateAssignedMeshes()
        {
            _assignedMeshes.Clear();
            _seenMeshIds.Clear();

            if (_targetMeshes == null || !_targetMeshes.isArray) return;

            for (int i = 0; i < _targetMeshes.arraySize; i++)
            {
                var mesh = _targetMeshes.GetArrayElementAtIndex(i).objectReferenceValue as SkinnedMeshRenderer;
                if (mesh == null || !_seenMeshIds.Add(mesh.GetEntityId())) continue;
                _assignedMeshes.Add(mesh);
            }
        }

        private int GetTotalUniqueBlendshapeCount()
        {
            _uniqueBlendshapes.Clear();
            for (int i = 0; i < _assignedMeshes.Count; i++)
            {
                SkinnedMeshRenderer mesh = _assignedMeshes[i];
                if (mesh == null || mesh.sharedMesh == null) continue;

                Mesh sharedMesh = mesh.sharedMesh;
                for (int j = 0; j < sharedMesh.blendShapeCount; j++)
                    _uniqueBlendshapes.Add(sharedMesh.GetBlendShapeName(j));
            }

            return _uniqueBlendshapes.Count;
        }

        private void AutoFindMeshesInHierarchy()
        {
            var source = (ConvaiFacialClipPlayer)target;
            Undo.RecordObject(source, "Auto-Find Baked Facial Meshes");

            Transform root = source.transform;
            SkinnedMeshRenderer[] meshes = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var discovered = new List<SkinnedMeshRenderer>();
            var seen = new HashSet<EntityId>();
            for (int i = 0; i < meshes.Length; i++)
            {
                SkinnedMeshRenderer mesh = meshes[i];
                if (mesh == null || mesh.sharedMesh == null || mesh.sharedMesh.blendShapeCount == 0) continue;
                if (!seen.Add(mesh.GetEntityId())) continue;
                discovered.Add(mesh);
            }

            discovered.Sort((a, b) =>
            {
                int scoreA = GetMeshPriority(a != null ? a.name : string.Empty);
                int scoreB = GetMeshPriority(b != null ? b.name : string.Empty);
                if (scoreA != scoreB) return scoreA.CompareTo(scoreB);

                string nameA = a != null ? a.name : string.Empty;
                string nameB = b != null ? b.name : string.Empty;
                return string.CompareOrdinal(nameA, nameB);
            });

            _targetMeshes.ClearArray();
            for (int i = 0; i < discovered.Count; i++)
            {
                _targetMeshes.InsertArrayElementAtIndex(i);
                _targetMeshes.GetArrayElementAtIndex(i).objectReferenceValue = discovered[i];
            }

            serializedObject.ApplyModifiedProperties();
            PopulateAssignedMeshes();
            EditorUtility.SetDirty(source);
        }

        private static int GetMeshPriority(string meshName)
        {
            string lowerName = meshName.ToLowerInvariant();
            if (lowerName.Contains("cc_base_body") || lowerName.Contains("skinhead") ||
                lowerName.Contains("head") || lowerName.Contains("face"))
                return 0;

            if (lowerName.Contains("teeth") || lowerName.Contains("tooth")) return 1;
            if (lowerName.Contains("tongue")) return 2;
            return 3;
        }
    }

    [CustomEditor(typeof(ConvaiFacialClipRuntimePlayer))]
    internal sealed class ConvaiFacialClipRuntimePlayerEditor : ConvaiPremiumInspectorEditor
    {
        private const string SectionClip = "Clip";
        private const string SectionValidation = "Validation";

        private SerializedProperty _clip;
        private SerializedProperty _playbackSpeed;
        private SerializedProperty _loop;
        private SerializedProperty _weightMultiplier;
        private SerializedProperty _targetMeshes;
        private bool _showClip;
        private bool _showValidation;

        protected override void OnEnable()
        {
            base.OnEnable();
            _clip = serializedObject.FindProperty("clip");
            _playbackSpeed = serializedObject.FindProperty("playbackSpeed");
            _loop = serializedObject.FindProperty("loop");
            _weightMultiplier = serializedObject.FindProperty("weightMultiplier");
            _targetMeshes = serializedObject.FindProperty("targetMeshes");
            _showClip = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionClip, true);
            _showValidation = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionValidation, true);
        }

        protected override void OnDisable()
        {
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionClip, _showClip);
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionValidation, _showValidation);
            base.OnDisable();
        }

        public override void OnInspectorGUI()
        {
            EnsurePremiumStyles();
            serializedObject.Update();

            DrawPremiumHeader("Runtime Facial Clip Source", "Dynamic Clip Input", "Editor", IdleColor);
            DrawInfoBox(
                "What this does",
                "Samples a runtime AnimationClip's blendshape curves each tick and submits them to the facial compositor. Use for per-character dynamic clips; prefer ConvaiFacialClipPlayer for reusable asset-based motion.");
            DrawClipSection();
            DrawValidationSection();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawClipSection()
        {
            _showClip = DrawSection(SectionClip, "CLIP & PLAYBACK", _showClip, ConvaiInspectorIconIds.Content);
            if (!_showClip) return;

            DrawSectionBody(() =>
            {
                EditorGUILayout.PropertyField(_clip, new GUIContent("Animation Clip"));
                EditorGUILayout.PropertyField(_playbackSpeed, new GUIContent("Playback Speed"));
                EditorGUILayout.PropertyField(_loop, new GUIContent("Loop"));
                EditorGUILayout.PropertyField(_weightMultiplier, new GUIContent("Weight Multiplier"));
                EditorGUILayout.Space(4f);
                EditorGUILayout.PropertyField(_targetMeshes, new GUIContent("Target Meshes"), true);
            });
        }

        private void DrawValidationSection()
        {
            _showValidation = DrawSection(SectionValidation, "VALIDATION", _showValidation, ConvaiInspectorIconIds.Validation, Warning);
            if (!_showValidation) return;

            DrawSectionBody(() =>
            {
                if (_clip.objectReferenceValue == null)
                    DrawWarningBox("Clip Missing", "Assign an AnimationClip or call SetClip at runtime to drive blendshape motion.");
                else
                    DrawInfoBox("Clip Ready", "The assigned clip will be sampled into the facial blendshape system at runtime.");
            });
        }
    }
}
