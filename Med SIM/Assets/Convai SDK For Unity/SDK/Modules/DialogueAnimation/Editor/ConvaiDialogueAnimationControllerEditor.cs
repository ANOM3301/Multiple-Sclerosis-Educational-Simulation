using System.Collections.Generic;
using Convai.Editor.Inspectors;
using Convai.Modules.DialogueAnimation.Components;
using Convai.Modules.DialogueAnimation.Core;
using Convai.Modules.DialogueAnimation.Runtime;
using Convai.Runtime.Embodiment;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Convai.Modules.DialogueAnimation.Editor
{
    /// <summary>
    ///     Custom inspector for <see cref="ConvaiDialogueAnimationController" /> that
    ///     validates the assigned animator controller against the component's state and
    ///     placeholder contract before the user enters play mode.
    /// </summary>
    [CustomEditor(typeof(ConvaiDialogueAnimationController))]
    internal sealed class ConvaiDialogueAnimationControllerEditor : ConvaiPremiumInspectorEditor
    {
        private const string SectionContent = "Content";
        private const string SectionAnimatorWiring = "AnimatorWiring";
        private const string SectionContract = "Contract";
        private const string SectionRuntimeStatus = "RuntimeStatus";
        private const string SectionValidation = "Validation";

        private static readonly string[] RequiredStateFields =
        {
            "_baseIdleStateName",
            "_idleOverlayStateA",
            "_idleOverlayStateB",
            "_bodyTalkStateA",
            "_bodyTalkStateB",
            "_headTalkStateA",
            "_headTalkStateB"
        };

        private SerializedProperty _profile;
        private SerializedProperty _library;
        private SerializedProperty _config;
        private SerializedProperty _characterGender;
        private SerializedProperty _animatorOverride;
        private SerializedProperty _contract;
        private SerializedProperty _baseIdleLayerIndex;
        private SerializedProperty _idleOverlayLayerIndex;
        private SerializedProperty _bodyTalkLayerIndex;
        private SerializedProperty _headTalkLayerIndex;
        private SerializedProperty _foundationIdleClip;
        private SerializedProperty _autoCreateConversationFlow;

        private bool _showContent;
        private bool _showAnimatorWiring;
        private bool _showContract;
        private bool _showRuntimeStatus;
        private bool _showValidation;

        protected override void OnEnable()
        {
            base.OnEnable();
            CacheProperties();
            _showContent = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionContent, true);
            _showAnimatorWiring = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionAnimatorWiring, true);
            _showContract = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionContract, false);
            _showRuntimeStatus = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionRuntimeStatus, true);
            _showValidation = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionValidation, true);
        }

        protected override void OnDisable()
        {
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionContent, _showContent);
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionAnimatorWiring, _showAnimatorWiring);
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionContract, _showContract);
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionRuntimeStatus, _showRuntimeStatus);
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionValidation, _showValidation);
            base.OnDisable();
        }

        private void CacheProperties()
        {
            _profile = serializedObject.FindProperty("profile"); // Base class field (no underscore)
            _library = serializedObject.FindProperty("_library");
            _config = serializedObject.FindProperty("_config");
            _characterGender = serializedObject.FindProperty("_characterGender");
            _animatorOverride = serializedObject.FindProperty("_animatorOverride");
            _contract = serializedObject.FindProperty("_contract");
            _baseIdleLayerIndex = serializedObject.FindProperty("_baseIdleLayerIndex");
            _idleOverlayLayerIndex = serializedObject.FindProperty("_idleOverlayLayerIndex");
            _bodyTalkLayerIndex = serializedObject.FindProperty("_bodyTalkLayerIndex");
            _headTalkLayerIndex = serializedObject.FindProperty("_headTalkLayerIndex");
            _foundationIdleClip = serializedObject.FindProperty("_foundationIdleClip");
            _autoCreateConversationFlow = serializedObject.FindProperty("_autoCreateConversationFlow");
        }

        public override void OnInspectorGUI()
        {
            EnsurePremiumStyles();
            serializedObject.Update();

            var controllerComponent = (ConvaiDialogueAnimationController)target;
            AnimatorController controller = ResolveController(controllerComponent);
            ConvaiEmbodimentContextEditorInfo contextInfo = ConvaiEmbodimentContextEditorResolver.Resolve(controllerComponent);
            EmbodimentContext context = contextInfo.Context;
            bool hasBlockingWarning = controllerComponent.Library == null || controllerComponent.Config == null ||
                                      controller == null || !contextInfo.HasSupportedCharacterScope;
            string status = hasBlockingWarning
                ? "Warning"
                : UnityEngine.Application.isPlaying
                    ? "Live"
                    : contextInfo.WillAutoCreateOnCharacter
                        ? "Auto"
                        : "Editor";
            Color statusColor = hasBlockingWarning
                ? Warning
                : UnityEngine.Application.isPlaying || contextInfo.WillAutoCreateOnCharacter
                    ? AccentEmphasis
                    : IdleColor;

            DrawPremiumHeader(
                "Dialogue Animation",
                "Plays idle and talking animation",
                status,
                statusColor);

            DrawTopWarnings(controllerComponent, contextInfo, controller);
            DrawInfoBox(
                "What this does",
                "Chooses and blends idle, body-talk, and head-talk animation clips while the character is listening, speaking, or reacting.");
            DrawContentSection();
            DrawAnimatorWiringSection();
            DrawContractSection();
            DrawRuntimeStatusSection(controllerComponent, context, controller);

            serializedObject.ApplyModifiedProperties();
            DrawValidationSection(controllerComponent, contextInfo);

            if (UnityEngine.Application.isPlaying) Repaint();
        }

        private void DrawContentSection()
        {
            _showContent = DrawSection(SectionContent, "CONTENT", _showContent, ConvaiInspectorIconIds.Content);
            if (!_showContent) return;

            DrawSectionBody(() =>
            {
                if (_profile != null)
                    EditorGUILayout.PropertyField(_profile, new GUIContent("Dialogue Animation Profile"));

                bool profileAssigned = _profile != null && _profile.objectReferenceValue != null;
                using (new EditorGUI.DisabledScope(profileAssigned))
                {
                    if (_library != null)
                        EditorGUILayout.PropertyField(_library, new GUIContent("Animation Library"));
                    if (_config != null)
                        EditorGUILayout.PropertyField(_config, new GUIContent("Runtime Settings"));
                    if (_foundationIdleClip != null)
                        EditorGUILayout.PropertyField(_foundationIdleClip, new GUIContent("Base Idle Clip"));
                    if (_characterGender != null)
                        EditorGUILayout.PropertyField(_characterGender, new GUIContent("Character Body Type"));
                    if (_autoCreateConversationFlow != null)
                        EditorGUILayout.PropertyField(_autoCreateConversationFlow, new GUIContent("Create Dialogue State Source"));
                }

                if (profileAssigned)
                    DrawInfoBox("Profile Controls Content", "The assigned profile provides the library, runtime settings, base idle clip, and character body type.");
            });
        }

        private void DrawAnimatorWiringSection()
        {
            _showAnimatorWiring = DrawSection(SectionAnimatorWiring, "ANIMATOR", _showAnimatorWiring, ConvaiInspectorIconIds.Animator);
            if (!_showAnimatorWiring) return;

            DrawSectionBody(() =>
            {
                if (_animatorOverride != null)
                    EditorGUILayout.PropertyField(_animatorOverride, new GUIContent("Animator Override"));
                DrawInfoBox("Automatic Animator", "Leave this empty to use the first Animator found on this character or its children.");
            });
        }

        private void DrawContractSection()
        {
            _showContract = DrawSection(SectionContract, "ADVANCED ANIMATOR CONTRACT", _showContract, ConvaiInspectorIconIds.Contract);
            if (!_showContract) return;

            DrawSectionBody(() =>
            {
                if (_contract != null)
                    EditorGUILayout.PropertyField(_contract, new GUIContent("Animator Contract"));
                if (_contract != null && _contract.objectReferenceValue != null)
                {
                    DrawInfoBox("Contract Assigned", "Layer indices, state names, and placeholder clip names are read from the assigned contract asset.");
                    return;
                }

                DrawInfoBox("Default Contract", "These fields only need changes when using a custom Animator Controller with different layer, state, or placeholder clip names.");
                DrawLayerFields();
                EditorGUILayout.Space(4f);
                DrawStateNameFields();
                EditorGUILayout.Space(4f);
                DrawPlaceholderNameFields();
            });
        }

        private void DrawLayerFields()
        {
            EditorGUILayout.LabelField("Animator Layers", EditorStyles.boldLabel);
            if (_baseIdleLayerIndex != null)
                EditorGUILayout.PropertyField(_baseIdleLayerIndex, new GUIContent("Base Idle Layer"));
            if (_idleOverlayLayerIndex != null)
                EditorGUILayout.PropertyField(_idleOverlayLayerIndex, new GUIContent("Idle Overlay Layer"));
            if (_bodyTalkLayerIndex != null)
                EditorGUILayout.PropertyField(_bodyTalkLayerIndex, new GUIContent("Body Talk Layer"));
            if (_headTalkLayerIndex != null)
                EditorGUILayout.PropertyField(_headTalkLayerIndex, new GUIContent("Head Talk Layer"));
        }

        private void DrawStateNameFields()
        {
            EditorGUILayout.LabelField("Animator State Names", EditorStyles.boldLabel);
            for (int i = 0; i < RequiredStateFields.Length; i++)
            {
                SerializedProperty property = serializedObject.FindProperty(RequiredStateFields[i]);
                if (property != null)
                    EditorGUILayout.PropertyField(property);
            }
        }

        private void DrawPlaceholderNameFields()
        {
            string[] placeholderFields =
            {
                "_basePlaceholderName",
                "_idleOverlayPlaceholderA",
                "_idleOverlayPlaceholderB",
                "_bodyTalkPlaceholderA",
                "_bodyTalkPlaceholderB",
                "_headTalkPlaceholderA",
                "_headTalkPlaceholderB"
            };

            EditorGUILayout.LabelField("Animator Placeholder Clip Names", EditorStyles.boldLabel);
            for (int i = 0; i < placeholderFields.Length; i++)
            {
                SerializedProperty property = serializedObject.FindProperty(placeholderFields[i]);
                if (property != null)
                    EditorGUILayout.PropertyField(property);
            }
        }

        private void DrawRuntimeStatusSection(
            ConvaiDialogueAnimationController controller,
            EmbodimentContext context,
            AnimatorController animatorController)
        {
            _showRuntimeStatus = DrawSection(SectionRuntimeStatus, "LIVE STATUS", _showRuntimeStatus, ConvaiInspectorIconIds.Live, Info);
            if (!_showRuntimeStatus) return;

            DrawSectionBody(() =>
            {
                if (!UnityEngine.Application.isPlaying)
                {
                    DrawOfflinePlaceholder();
                    return;
                }

                EditorGUILayout.BeginHorizontal();
                DrawLiveCell("Idle Clip", controller.LastIdleIndex.ToString(), DefaultValueColor, 92);
                DrawLiveCell("Talk Clip", controller.LastTalkIndex.ToString(), DefaultValueColor, 92);
                DrawLiveCell("Base Weight", controller.CurrentBaseIdleLayerWeight.ToString("0.00"), DefaultValueColor, 104);
                DrawLiveCell("Idle Weight", controller.CurrentIdleOverlayLayerWeight.ToString("0.00"),
                    controller.CurrentIdleOverlayLayerWeight > 0.01f ? AccentEmphasis : IdleColor, 84);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                DrawLiveCell("Head Talk", controller.CurrentHeadTalkLayerWeight.ToString("0.00"),
                    controller.CurrentHeadTalkLayerWeight > 0.01f ? AccentEmphasis : IdleColor, 104);
                DrawLiveCell("Body Talk", controller.CurrentBodyTalkLayerWeight.ToString("0.00"),
                    controller.CurrentBodyTalkLayerWeight > 0.01f ? AccentEmphasis : IdleColor, 112);
                DrawLiveCell("Idle Pool", controller.HasValidIdleLibrary ? "Valid" : "Empty",
                    controller.HasValidIdleLibrary ? AccentEmphasis : Warning, 90);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField("Foundation Idle", ObjectStatus(controller.CurrentFoundationIdleClip));
                EditorGUILayout.LabelField("Idle Overlay", ObjectStatus(controller.CurrentIdleOverlayClip));
                EditorGUILayout.LabelField("Body Talk", ObjectStatus(controller.CurrentBodyTalkClip));
                EditorGUILayout.LabelField("Talk Clip", ObjectStatus(controller.CurrentTalkClip));
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Base Idle Layer", LayerStatus(animatorController, controller.RuntimeBaseIdleLayerIndex));
                EditorGUILayout.LabelField("Idle Overlay Layer", LayerStatus(animatorController, controller.RuntimeIdleOverlayLayerIndex));
                EditorGUILayout.LabelField("Body Talk Layer", LayerStatus(animatorController, controller.RuntimeBodyTalkLayerIndex));
                EditorGUILayout.LabelField("Head Talk Layer", LayerStatus(animatorController, controller.RuntimeHeadTalkLayerIndex));
                EditorGUILayout.LabelField("Conversation Flow", context?.ConversationFlowSource != null ? context.ConversationFlowSource.GetType().Name : "Not registered");
                EditorGUILayout.LabelField("Emotion Source", context?.EmotionStateSource != null ? context.EmotionStateSource.GetType().Name : "Not registered");
            });
        }

        private static string LayerStatus(AnimatorController controller, int layerIndex)
        {
            if (controller == null) return "Controller missing";
            if (layerIndex < 0 || layerIndex >= controller.layers.Length)
                return $"Layer {layerIndex}: missing";

            AnimatorControllerLayer layer = controller.layers[layerIndex];
            string mask = layer.avatarMask != null ? layer.avatarMask.name : "No mask";
            return $"Layer {layerIndex}: {layer.name} | {mask} | {layer.blendingMode}";
        }

        private void DrawValidationSection(
            ConvaiDialogueAnimationController controllerComponent,
            ConvaiEmbodimentContextEditorInfo contextInfo)
        {
            _showValidation = DrawSection(SectionValidation, "VALIDATION", _showValidation, ConvaiInspectorIconIds.Validation, Warning);
            if (!_showValidation) return;

            DrawSectionBody(() =>
            {
                DrawContextResolutionSummary(contextInfo, true, "component");
                DrawValidation(controllerComponent);
            });
        }

        private void DrawTopWarnings(
            ConvaiDialogueAnimationController controllerComponent,
            ConvaiEmbodimentContextEditorInfo contextInfo,
            AnimatorController controller)
        {
            DrawContextResolutionSummary(contextInfo, false, "component");

            if (controllerComponent.Library == null)
                DrawWarningBox("Animation Library Missing", "Assign a Dialogue Animation Profile or an Animation Library so the component has clips to play.");

            if (controllerComponent.Config == null)
                DrawWarningBox("Runtime Settings Missing", "Assign a Dialogue Animation Profile or Runtime Settings so the component knows how to blend clips.");

            if (controller == null)
                DrawWarningBox("Animator Controller Missing", "No Animator Controller was found on this GameObject or its children.");
        }

        private void DrawValidation(ConvaiDialogueAnimationController controllerComponent)
        {
            AnimatorController controller = ResolveController(controllerComponent);
            if (controller == null)
            {
                DrawWarningBox(
                    "Animator Controller Missing",
                    "Assign a Dialogue Animator Controller before entering Play Mode.");
                return;
            }

            if (controller.layers.Length < 4)
            {
                DrawErrorBox(
                    "Layer Count Invalid",
                    "The dialogue animation controller expects at least four animator layers " +
                    "(base idle, idle overlay, body talk, head talk).");
                return;
            }

            if (!ValidateLayerIndices(controller, serializedObject))
                return;

            ValidateController(controller, serializedObject);
        }

        private static AnimatorController ResolveController(ConvaiDialogueAnimationController driver)
        {
            Animator animator = driver.GetComponentInChildren<Animator>(true);
            if (animator == null)
                return null;

            RuntimeAnimatorController rac = animator.runtimeAnimatorController;
            if (rac is AnimatorOverrideController aoc)
                return aoc.runtimeAnimatorController as AnimatorController;

            return rac as AnimatorController;
        }

        private void ValidateController(AnimatorController controller, SerializedObject so)
        {
            HashSet<string> stateNames = CollectStateNames(controller);
            List<string> missing = new(8);

            string[] requiredStateNames = ResolveStateNames(so);
            for (int i = 0; i < requiredStateNames.Length; i++)
            {
                string stateName = requiredStateNames[i];
                if (!string.IsNullOrEmpty(stateName) && !stateNames.Contains(stateName))
                    missing.Add(stateName);
            }

            if (missing.Count > 0)
            {
                DrawErrorBox(
                    "Required States Missing",
                    "The assigned controller is missing required states: " +
                    string.Join(", ", missing) +
                    ". Add states with these exact names or update the state-name fields above.");
                return;
            }

            HashSet<string> clipNames = CollectClipNames(controller);
            string[] placeholderNames = ResolvePlaceholderNames(so);
            List<string> missingPlaceholders = new(8);
            for (int i = 0; i < placeholderNames.Length; i++)
            {
                string name = placeholderNames[i];
                if (!string.IsNullOrEmpty(name) && !clipNames.Contains(name))
                    missingPlaceholders.Add(name);
            }

            if (missingPlaceholders.Count > 0)
            {
                DrawErrorBox(
                    "Placeholder Clips Missing",
                    "The controller does not reference placeholder clips named: " +
                    string.Join(", ", missingPlaceholders) +
                    ". AnimatorOverrideController keys by clip name, so the placeholder clip " +
                    "names must match these fields.");
                DrawInfoBox(
                    "Sample Animator",
                    "Use the shared sample animator `ConvaiSample_DialogueAnimator` " +
                    "(package: SamplesShared/Art/Animations/Dialogue/Controllers/) as this character's " +
                    "Animator Runtime Controller, or merge its four layers and placeholder motions.");
                return;
            }

        }

        private bool ValidateLayerIndices(AnimatorController controller, SerializedObject so)
        {
            int layerCount = controller.layers.Length;
            DialogueAnimatorContract contract = ResolveContract(so);
            int baseLayer = contract != null ? contract.BaseIdleLayerIndex : so.FindProperty("_baseIdleLayerIndex").intValue;
            int idleOverlay = contract != null ? contract.IdleOverlayLayerIndex : so.FindProperty("_idleOverlayLayerIndex").intValue;
            int bodyTalk = contract != null ? contract.BodyTalkLayerIndex : so.FindProperty("_bodyTalkLayerIndex").intValue;
            int headTalk = contract != null ? contract.HeadTalkLayerIndex : so.FindProperty("_headTalkLayerIndex").intValue;

            if (!IsLayerInRange(baseLayer, layerCount)
                || !IsLayerInRange(idleOverlay, layerCount)
                || !IsLayerInRange(bodyTalk, layerCount)
                || !IsLayerInRange(headTalk, layerCount))
            {
                DrawErrorBox(
                    "Layer Index Out Of Range",
                    $"Layer indices must be within 0..{layerCount - 1}. Current values: " +
                    $"baseIdle={baseLayer}, idleOverlay={idleOverlay}, bodyTalk={bodyTalk}, headTalk={headTalk}.");
                return false;
            }

            if (baseLayer == idleOverlay || baseLayer == bodyTalk || baseLayer == headTalk ||
                idleOverlay == bodyTalk || idleOverlay == headTalk || bodyTalk == headTalk)
            {
                DrawErrorBox(
                    "Layer Indices Must Be Unique",
                    "Base idle, idle overlay, body talk, and head talk layer indices must be unique.");
                return false;
            }

            return true;
        }

        private static bool IsLayerInRange(int layerIndex, int layerCount) =>
            layerIndex >= 0 && layerIndex < layerCount;

        private static DialogueAnimatorContract ResolveContract(SerializedObject so) =>
            so.FindProperty("_contract")?.objectReferenceValue as DialogueAnimatorContract;

        private static string[] ResolveStateNames(SerializedObject so)
        {
            DialogueAnimatorContract contract = ResolveContract(so);
            if (contract != null)
            {
                return new[]
                {
                    contract.BaseIdleStateName,
                    contract.IdleOverlayStateA,
                    contract.IdleOverlayStateB,
                    contract.BodyTalkStateA,
                    contract.BodyTalkStateB,
                    contract.HeadTalkStateA,
                    contract.HeadTalkStateB
                };
            }

            var names = new string[RequiredStateFields.Length];
            for (int i = 0; i < RequiredStateFields.Length; i++)
                names[i] = so.FindProperty(RequiredStateFields[i]).stringValue;
            return names;
        }

        private static string[] ResolvePlaceholderNames(SerializedObject so)
        {
            DialogueAnimatorContract contract = ResolveContract(so);
            if (contract != null)
            {
                return new[]
                {
                    contract.BasePlaceholderName,
                    contract.IdleOverlayPlaceholderA,
                    contract.IdleOverlayPlaceholderB,
                    contract.BodyTalkPlaceholderA,
                    contract.BodyTalkPlaceholderB,
                    contract.HeadTalkPlaceholderA,
                    contract.HeadTalkPlaceholderB
                };
            }

            string[] placeholderFields =
            {
                "_basePlaceholderName",
                "_idleOverlayPlaceholderA",
                "_idleOverlayPlaceholderB",
                "_bodyTalkPlaceholderA",
                "_bodyTalkPlaceholderB",
                "_headTalkPlaceholderA",
                "_headTalkPlaceholderB"
            };

            var names = new string[placeholderFields.Length];
            for (int i = 0; i < placeholderFields.Length; i++)
                names[i] = so.FindProperty(placeholderFields[i]).stringValue;
            return names;
        }

        private static HashSet<string> CollectStateNames(AnimatorController controller)
        {
            var names = new HashSet<string>(32);
            AnimatorControllerLayer[] layers = controller.layers;
            for (int l = 0; l < layers.Length; l++)
            {
                AnimatorStateMachine machine = layers[l].stateMachine;
                if (machine == null) continue;
                CollectFromMachine(machine, names);
            }
            return names;
        }

        private static void CollectFromMachine(AnimatorStateMachine machine, HashSet<string> names)
        {
            for (int i = 0; i < machine.states.Length; i++)
                names.Add(machine.states[i].state.name);

            for (int i = 0; i < machine.stateMachines.Length; i++)
                CollectFromMachine(machine.stateMachines[i].stateMachine, names);
        }

        private static HashSet<string> CollectClipNames(AnimatorController controller)
        {
            var names = new HashSet<string>(32);
            AnimationClip[] clips = controller.animationClips;
            for (int i = 0; i < clips.Length; i++)
                if (clips[i] != null) names.Add(clips[i].name);
            return names;
        }
    }
}
