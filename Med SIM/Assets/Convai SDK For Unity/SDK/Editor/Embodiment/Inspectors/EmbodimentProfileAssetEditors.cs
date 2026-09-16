using System.Collections.Generic;
using Convai.Domain.Embodiment.Semantics;
using Convai.Editor.Inspectors;
using Convai.Modules.Attention.Profiles;
using Convai.Modules.Embodiment.Presets;
using Convai.Modules.ConversationFlow.Profiles;
using Convai.Modules.DialogueAnimation.Profiles;
using Convai.Modules.Emotion.Profiles;
using Convai.Modules.Emotion.Taxonomy;
using Convai.Modules.Gaze.Profiles;
using UnityEditor;
using UnityEngine;

namespace Convai.Editor.Embodiment.Inspectors
{
    [CustomEditor(typeof(EmbodimentPresetLibrary))]
    internal sealed class EmbodimentPresetLibraryInspector : ConvaiEmbodimentProfileEditorBase<EmbodimentPresetLibrary>
    {
        internal const string SectionPresets = "Presets";
        internal const string SectionDiagnostics = "Diagnostics";

        private bool _showPresets;
        private bool _showDiagnostics;

        protected override string HeaderTitle => "Embodiment Preset Library";
        protected override string HeaderSubtitle => "Character Preset Catalog";
        protected override string HeaderStatus => Profile.HasDuplicatePresetIds(out _) ? "Warning" : "Ready";
        protected override Color HeaderStatusColor => Profile.HasDuplicatePresetIds(out _) ? Warning : AccentEmphasis;

        protected override void OnEnable()
        {
            base.OnEnable();
            _showPresets = LoadSectionState(SectionPresets, true);
            _showDiagnostics = LoadSectionState(SectionDiagnostics, true);
        }

        protected override void OnDisable()
        {
            SaveSectionState(SectionPresets, _showPresets);
            SaveSectionState(SectionDiagnostics, _showDiagnostics);
            base.OnDisable();
        }

        protected override void DrawProfileInspector()
        {
            _showPresets = DrawProfileSection(SectionPresets, "PRESETS", _showPresets,
                ConvaiInspectorIconIds.Content);
            if (_showPresets)
                DrawSectionBody(() => DrawProperty("presets"));

            _showDiagnostics = DrawProfileSection(SectionDiagnostics, "DIAGNOSTICS", _showDiagnostics,
                ConvaiInspectorIconIds.Validation, Warning);
            if (_showDiagnostics)
            {
                DrawSectionBody(() =>
                {
                    int count = Profile.Presets?.Count ?? 0;
                    EditorGUILayout.LabelField("Preset Count", count.ToString());
                    if (count == 0)
                        DrawWarningBox("Empty Library", "Add at least one Character Embodiment Preset.");
                    else if (Profile.HasDuplicatePresetIds(out string message))
                        DrawWarningBox("Duplicate Preset IDs", message);
                    else
                        DrawInfoBox("Library Ready", "Preset IDs are unique.");
                });
            }
        }
    }

    [CustomEditor(typeof(ConvaiConversationFlowProfile))]
    internal sealed class ConversationFlowProfileInspector : ConvaiEmbodimentProfileEditorBase<ConvaiConversationFlowProfile>
    {
        internal const string SectionTransition = "Transition";
        internal const string SectionDialogueBeats = "DialogueBeats";
        internal const string SectionEnergy = "Energy";

        private bool _showTransition;
        private bool _showDialogueBeats;
        private bool _showEnergy;

        protected override string HeaderTitle => "Conversation Flow Profile";
        protected override string HeaderSubtitle => "Dialogue State Timing";

        protected override void OnEnable()
        {
            base.OnEnable();
            _showTransition = LoadSectionState(SectionTransition, true);
            _showDialogueBeats = LoadSectionState(SectionDialogueBeats, true);
            _showEnergy = LoadSectionState(SectionEnergy, true);
        }

        protected override void OnDisable()
        {
            SaveSectionState(SectionTransition, _showTransition);
            SaveSectionState(SectionDialogueBeats, _showDialogueBeats);
            SaveSectionState(SectionEnergy, _showEnergy);
            base.OnDisable();
        }

        protected override void DrawProfileInspector()
        {
            _showTransition = DrawProfileSection(SectionTransition, "TRANSITION", _showTransition,
                ConvaiInspectorIconIds.Motion);
            if (_showTransition)
                DrawSectionBody(() => DrawProperty("transitionDuration"));

            _showDialogueBeats = DrawProfileSection(SectionDialogueBeats, "DIALOGUE BEATS", _showDialogueBeats,
                ConvaiInspectorIconIds.Live);
            if (_showDialogueBeats)
            {
                DrawSectionBody(() =>
                {
                    DrawProperty("thinkingMinHold");
                    DrawProperty("thinkingMaxHold");
                    DrawProperty("attendingGracePeriod");
                    DrawProperty("settlingDuration");
                    DrawProperty("idleReturnDelay");
                    DrawProperty("interruptedFreezeDuration");
                });
            }

            _showEnergy = DrawProfileSection(SectionEnergy, "ENERGY", _showEnergy,
                ConvaiInspectorIconIds.Range);
            if (_showEnergy)
                DrawSectionBody(() => DrawProperty("speakingBaseEnergy"));
        }
    }

    [CustomEditor(typeof(ConvaiAttentionProfile))]
    internal sealed class AttentionProfileInspector : ConvaiEmbodimentProfileEditorBase<ConvaiAttentionProfile>
    {
        internal const string SectionCommitment = "Commitment";
        internal const string SectionBudget = "Budget";
        internal const string SectionFocus = "Focus";
        internal const string SectionDefaultProvider = "DefaultFocusProvider";

        private bool _showCommitment;
        private bool _showBudget;
        private bool _showFocus;
        private bool _showDefaultProvider;

        protected override string HeaderTitle => "Attention Profile";
        protected override string HeaderSubtitle => "Target Arbitration";

        protected override void OnEnable()
        {
            base.OnEnable();
            _showCommitment = LoadSectionState(SectionCommitment, true);
            _showBudget = LoadSectionState(SectionBudget, true);
            _showFocus = LoadSectionState(SectionFocus, true);
            _showDefaultProvider = LoadSectionState(SectionDefaultProvider, false);
        }

        protected override void OnDisable()
        {
            SaveSectionState(SectionCommitment, _showCommitment);
            SaveSectionState(SectionBudget, _showBudget);
            SaveSectionState(SectionFocus, _showFocus);
            SaveSectionState(SectionDefaultProvider, _showDefaultProvider);
            base.OnDisable();
        }

        protected override void DrawProfileInspector()
        {
            _showCommitment = DrawProfileSection(SectionCommitment, "COMMITMENT", _showCommitment,
                ConvaiInspectorIconIds.Live);
            if (_showCommitment)
                DrawSectionBody(() => DrawProperties(Find("commitmentAcquireSeconds"), Find("commitmentReleaseSeconds"),
                    Find("focusLossHoldSeconds")));

            _showBudget = DrawProfileSection(SectionBudget, "ATTENTION BUDGET", _showBudget,
                ConvaiInspectorIconIds.Range);
            if (_showBudget)
                DrawSectionBody(() => DrawProperties(Find("maxContinuousHoldSeconds"), Find("interestBreakThreshold"),
                    Find("interestDecayPerSecond"), Find("interestRecoveryPerSecond")));

            _showFocus = DrawProfileSection(SectionFocus, "FOCUS SMOOTHING", _showFocus,
                ConvaiInspectorIconIds.Motion);
            if (_showFocus)
                DrawSectionBody(() => DrawProperties(Find("focusPositionLerpSpeed"), Find("focusOffset")));

            _showDefaultProvider = DrawProfileSection(SectionDefaultProvider, "DEFAULT FOCUS PROVIDER",
                _showDefaultProvider, ConvaiInspectorIconIds.Discovery);
            if (_showDefaultProvider)
                DrawSectionBody(() => DrawProperties(Find("defaultFocusBaseRelevance"),
                    Find("defaultFocusTargetHeadHeight"), Find("defaultFocusMaxDistance"),
                    Find("defaultFocusFullRelevanceDistance")));
        }
    }

    [CustomEditor(typeof(ConvaiEmotionProfile))]
    internal sealed class EmotionProfileInspector : ConvaiEmbodimentProfileEditorBase<ConvaiEmotionProfile>
    {
        internal const string SectionTaxonomy = "Taxonomy";
        internal const string SectionResponse = "Response";
        internal const string SectionAlternation = "NeutralAlternation";
        internal const string SectionBindings = "OutputBindings";

        private bool _showTaxonomy;
        private bool _showResponse;
        private bool _showAlternation;
        private bool _showBindings;

        protected override string HeaderTitle => "Emotion Profile";
        protected override string HeaderSubtitle => "Realtime Expression";
        protected override string HeaderStatus => Profile.Taxonomy == null ? "Default Taxonomy" : "Ready";
        protected override Color HeaderStatusColor => Profile.Taxonomy == null ? Info : AccentEmphasis;

        protected override void OnEnable()
        {
            base.OnEnable();
            _showTaxonomy = LoadSectionState(SectionTaxonomy, true);
            _showResponse = LoadSectionState(SectionResponse, true);
            _showAlternation = LoadSectionState(SectionAlternation, true);
            _showBindings = LoadSectionState(SectionBindings, true);
        }

        protected override void OnDisable()
        {
            SaveSectionState(SectionTaxonomy, _showTaxonomy);
            SaveSectionState(SectionResponse, _showResponse);
            SaveSectionState(SectionAlternation, _showAlternation);
            SaveSectionState(SectionBindings, _showBindings);
            base.OnDisable();
        }

        protected override void DrawProfileInspector()
        {
            _showTaxonomy = DrawProfileSection(SectionTaxonomy, "TAXONOMY", _showTaxonomy,
                ConvaiInspectorIconIds.Content);
            if (_showTaxonomy)
            {
                DrawSectionBody(() =>
                {
                    DrawProperty("taxonomy");
                    if (Profile.Taxonomy == null)
                        DrawInfoBox("Runtime Default", "No taxonomy assigned. Runtime will synthesize the default Plutchik-style taxonomy.");
                });
            }

            _showResponse = DrawProfileSection(SectionResponse, "RESPONSE SHAPING", _showResponse,
                ConvaiInspectorIconIds.Motion);
            if (_showResponse)
                DrawSectionBody(() => DrawProperties(Find("lerpSpeed"), Find("decaySpeed"), Find("intensityOffset"),
                    Find("microBurstEnabled"), Find("microBurstDuration"), Find("microBurstOvershoot"),
                    Find("microBurstThreshold")));

            _showAlternation = DrawProfileSection(SectionAlternation, "NEUTRAL ALTERNATION", _showAlternation,
                ConvaiInspectorIconIds.Blink);
            if (_showAlternation)
                DrawSectionBody(() => DrawProperties(Find("neutralAlternationEnabled"), Find("alternationMinInterval"),
                    Find("alternationMaxInterval"), Find("alternationBlendDuration"),
                    Find("alternateOnlyWhileTalking")));

            _showBindings = DrawProfileSection(SectionBindings, "OUTPUT BINDINGS", _showBindings,
                ConvaiInspectorIconIds.Routing);
            if (_showBindings)
            {
                DrawSectionBody(() =>
                {
                    DrawProperty("blendshapeBinding");
                    DrawProperty("animatorBinding");
                    EditorGUILayout.LabelField("Blendshape Slots", (Profile.BlendshapeBinding?.Slots?.Count ?? 0).ToString());
                    EditorGUILayout.LabelField("Animator Slots", (Profile.AnimatorBinding?.Slots?.Count ?? 0).ToString());
                });
            }
        }
    }

    [CustomEditor(typeof(EmotionTaxonomyAsset))]
    internal sealed class EmotionTaxonomyAssetInspector : ConvaiEmbodimentProfileEditorBase<EmotionTaxonomyAsset>
    {
        internal const string SectionEntries = "Entries";
        internal const string SectionDiagnostics = "Diagnostics";

        private bool _showEntries;
        private bool _showDiagnostics;

        protected override string HeaderTitle => "Emotion Taxonomy";
        protected override string HeaderSubtitle => "Emotion Vocabulary";

        protected override void OnEnable()
        {
            base.OnEnable();
            _showEntries = LoadSectionState(SectionEntries, true);
            _showDiagnostics = LoadSectionState(SectionDiagnostics, true);
        }

        protected override void OnDisable()
        {
            SaveSectionState(SectionEntries, _showEntries);
            SaveSectionState(SectionDiagnostics, _showDiagnostics);
            base.OnDisable();
        }

        protected override void DrawProfileInspector()
        {
            _showEntries = DrawProfileSection(SectionEntries, "ENTRIES", _showEntries,
                ConvaiInspectorIconIds.Content);
            if (_showEntries)
                DrawSectionBody(() => DrawProperty("entries"));

            _showDiagnostics = DrawProfileSection(SectionDiagnostics, "DIAGNOSTICS", _showDiagnostics,
                ConvaiInspectorIconIds.Validation, Warning);
            if (_showDiagnostics)
            {
                DrawSectionBody(() =>
                {
                    int entryCount = ArraySize("entries");
                    EditorGUILayout.LabelField("Authored Entries", entryCount.ToString());
                    if (entryCount == 0)
                        DrawWarningBox("Empty Taxonomy", "Runtime will synthesize neutral only if no valid entries exist.");
                    else
                        DrawInfoBox("Taxonomy Authored", "Runtime canonicalizes labels and aliases case-insensitively.");
                });
            }
        }
    }

    [CustomEditor(typeof(ConvaiGazeCoordinationProfile))]
    internal sealed class ConvaiGazeCoordinationProfileInspector : ConvaiEmbodimentProfileEditorBase<ConvaiGazeCoordinationProfile>
    {
        internal const string SectionStateWeights = "StateWeights";
        internal const string SectionSmoothing = "Smoothing";
        internal const string SectionDiagnostics = "Diagnostics";

        private bool _showStateWeights;
        private bool _showSmoothing;
        private bool _showDiagnostics;

        protected override string HeaderTitle => "Gaze Coordinator Profile";
        protected override string HeaderSubtitle => "Attention to Gaze Intent";

        protected override void OnEnable()
        {
            base.OnEnable();
            _showStateWeights = LoadSectionState(SectionStateWeights, true);
            _showSmoothing = LoadSectionState(SectionSmoothing, true);
            _showDiagnostics = LoadSectionState(SectionDiagnostics, true);
        }

        protected override void OnDisable()
        {
            SaveSectionState(SectionStateWeights, _showStateWeights);
            SaveSectionState(SectionSmoothing, _showSmoothing);
            SaveSectionState(SectionDiagnostics, _showDiagnostics);
            base.OnDisable();
        }

        protected override void DrawProfileInspector()
        {
            _showStateWeights = DrawProfileSection(SectionStateWeights, "STATE WEIGHTS", _showStateWeights,
                ConvaiInspectorIconIds.Routing);
            if (_showStateWeights)
                DrawSectionBody(() => DrawProperty("stateWeights"));

            _showSmoothing = DrawProfileSection(SectionSmoothing, "SMOOTHING", _showSmoothing,
                ConvaiInspectorIconIds.Motion);
            if (_showSmoothing)
                DrawSectionBody(() => DrawProperties(Find("weightBlendSpeed"), Find("eyeShareBlendSpeed")));

            _showDiagnostics = DrawProfileSection(SectionDiagnostics, "DIAGNOSTICS", _showDiagnostics,
                ConvaiInspectorIconIds.Validation, Warning);
            if (_showDiagnostics)
            {
                DrawSectionBody(() =>
                {
                    int stateWeightCount = ArraySize("stateWeights");
                    EditorGUILayout.LabelField("State Policies", stateWeightCount.ToString());
                    if (!HasStatePolicy(DialogueState.Idle))
                        DrawWarningBox("Idle Fallback Missing", "Add an Idle state policy so unknown dialogue states resolve predictably.");
                    else
                        DrawInfoBox("Fallback Ready", "Idle policy is available for unknown dialogue states.");
                });
            }
        }

        private bool HasStatePolicy(DialogueState state)
        {
            SerializedProperty list = Find("stateWeights");
            if (list == null || !list.isArray) return false;

            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                SerializedProperty stateProperty = element.FindPropertyRelative("State");
                if (stateProperty != null && stateProperty.enumValueIndex == (int)state)
                    return true;
            }
            return false;
        }
    }

    [CustomEditor(typeof(ConvaiGazeEyeProfile))]
    internal sealed class ConvaiGazeEyeProfileInspector : ConvaiEmbodimentProfileEditorBase<ConvaiGazeEyeProfile>
    {
        internal const string SectionTracking = "Tracking";
        internal const string SectionSaccades = "Saccades";
        internal const string SectionIdle = "IdleExploration";
        internal const string SectionBlink = "Blink";
        internal const string SectionEyelidFollow = "EyelidFollow";

        private bool _showTracking;
        private bool _showSaccades;
        private bool _showIdle;
        private bool _showBlink;
        private bool _showEyelidFollow;

        protected override string HeaderTitle => "Eye Gaze Profile";
        protected override string HeaderSubtitle => "Procedural Eye Motion";

        protected override void OnEnable()
        {
            base.OnEnable();
            _showTracking = LoadSectionState(SectionTracking, true);
            _showSaccades = LoadSectionState(SectionSaccades, true);
            _showIdle = LoadSectionState(SectionIdle, true);
            _showBlink = LoadSectionState(SectionBlink, true);
            _showEyelidFollow = LoadSectionState(SectionEyelidFollow, true);
        }

        protected override void OnDisable()
        {
            SaveSectionState(SectionTracking, _showTracking);
            SaveSectionState(SectionSaccades, _showSaccades);
            SaveSectionState(SectionIdle, _showIdle);
            SaveSectionState(SectionBlink, _showBlink);
            SaveSectionState(SectionEyelidFollow, _showEyelidFollow);
            base.OnDisable();
        }

        protected override void DrawProfileInspector()
        {
            _showTracking = DrawProfileSection(SectionTracking, "TRACKING", _showTracking,
                ConvaiInspectorIconIds.Motion);
            if (_showTracking)
                DrawSectionBody(() => DrawProperties(Find("trackingSharpness"), Find("maxYawDegrees"),
                    Find("maxPitchDegrees")));

            _showSaccades = DrawProfileSection(SectionSaccades, "SACCADES & TREMOR", _showSaccades,
                ConvaiInspectorIconIds.Blink);
            if (_showSaccades)
                DrawSectionBody(() => DrawProperties(Find("enableSaccades"), Find("saccadeIntervalMean"),
                    Find("saccadeIntervalJitter"), Find("saccadeMaxDegrees"), Find("saccadeDuration"),
                    Find("enableMicroTremor"), Find("microTremorAmplitude"), Find("microTremorFrequency")));

            _showIdle = DrawProfileSection(SectionIdle, "IDLE EXPLORATION", _showIdle,
                ConvaiInspectorIconIds.Range);
            if (_showIdle)
                DrawSectionBody(() => DrawProperties(Find("enableIdleExploration"), Find("idleExplorationWeight"),
                    Find("idleExplorationHorizontalDegrees"), Find("idleExplorationUpDegrees"),
                    Find("idleExplorationDownDegrees"), Find("idleExplorationIntervalMin"),
                    Find("idleExplorationIntervalMax"), Find("idleExplorationCenterBias"),
                    Find("idleRecenteringChance")));

            _showBlink = DrawProfileSection(SectionBlink, "BLINK", _showBlink,
                ConvaiInspectorIconIds.Blink);
            if (_showBlink)
                DrawSectionBody(() => DrawProperties(Find("enableBlink"), Find("blinkIntervalMean"),
                    Find("blinkIntervalJitter"), Find("blinkCycleDuration")));

            _showEyelidFollow = DrawProfileSection(SectionEyelidFollow, "EYELID FOLLOW", _showEyelidFollow,
                ConvaiInspectorIconIds.Blink);
            if (_showEyelidFollow)
                DrawSectionBody(() => DrawProperties(Find("enableEyelidFollow"), Find("eyelidFollowSharpness"),
                    Find("downwardLidStartDegrees"), Find("downwardLidFullDegrees"),
                    Find("downwardUpperLidMaxWeight"), Find("downwardBlinkFallbackMaxWeight"),
                    Find("downwardLookShapeMaxWeight"), Find("downwardLowerLidMaxWeight"), Find("upwardLidStartDegrees"),
                    Find("upwardLidFullDegrees"), Find("upwardEyeWideMaxWeight"),
                    Find("upwardLookShapeMaxWeight"), Find("extremeGazeSquintStartDegrees"), Find("extremeGazeSquintFullDegrees"),
                    Find("extremeGazeSquintMaxWeight")));
        }
    }

    [CustomEditor(typeof(ConvaiGazeHeadProfile))]
    internal sealed class ConvaiGazeHeadProfileInspector : ConvaiEmbodimentProfileEditorBase<ConvaiGazeHeadProfile>
    {
        internal const string SectionRange = "Range";
        internal const string SectionSmoothing = "Smoothing";
        internal const string SectionIdleExploration = "IdleExploration";
        internal const string SectionDistribution = "Distribution";
        internal const string SectionUpperBodyFollow = "UpperBodyFollow";

        private bool _showRange;
        private bool _showSmoothing;
        private bool _showIdleExploration;
        private bool _showDistribution;
        private bool _showUpperBodyFollow;

        protected override string HeaderTitle => "Head Look Profile";
        protected override string HeaderSubtitle => "Procedural Head Motion";

        protected override void OnEnable()
        {
            base.OnEnable();
            _showRange = LoadSectionState(SectionRange, true);
            _showSmoothing = LoadSectionState(SectionSmoothing, true);
            _showIdleExploration = LoadSectionState(SectionIdleExploration, true);
            _showDistribution = LoadSectionState(SectionDistribution, true);
            _showUpperBodyFollow = LoadSectionState(SectionUpperBodyFollow, true);
        }

        protected override void OnDisable()
        {
            SaveSectionState(SectionRange, _showRange);
            SaveSectionState(SectionSmoothing, _showSmoothing);
            SaveSectionState(SectionIdleExploration, _showIdleExploration);
            SaveSectionState(SectionDistribution, _showDistribution);
            SaveSectionState(SectionUpperBodyFollow, _showUpperBodyFollow);
            base.OnDisable();
        }

        protected override void DrawProfileInspector()
        {
            _showRange = DrawProfileSection(SectionRange, "RANGE", _showRange,
                ConvaiInspectorIconIds.Range);
            if (_showRange)
                DrawSectionBody(() => DrawProperties(Find("maxNeckYaw"), Find("maxNeckPitch"),
                    Find("maxHeadYaw"), Find("maxHeadPitch")));

            _showSmoothing = DrawProfileSection(SectionSmoothing, "SMOOTHING", _showSmoothing,
                ConvaiInspectorIconIds.Motion);
            if (_showSmoothing)
                DrawSectionBody(() => DrawProperties(Find("smoothingSharpness"), Find("returnSharpness"),
                    Find("idleSharpness"), Find("maxYawSpeedDegrees"), Find("maxPitchSpeedDegrees"),
                    Find("deadzoneDegrees")));

            _showIdleExploration = DrawProfileSection(SectionIdleExploration, "IDLE EXPLORATION",
                _showIdleExploration, ConvaiInspectorIconIds.Motion);
            if (_showIdleExploration)
                DrawSectionBody(() => DrawProperties(Find("enableIdleExploration"), Find("idleExplorationWeight"),
                    Find("idleExplorationYawDegrees"), Find("idleExplorationUpDegrees"),
                    Find("idleExplorationDownDegrees"), Find("idleExplorationIntervalMin"),
                    Find("idleExplorationIntervalMax"), Find("idleExplorationCenterBias"),
                    Find("idleRecenteringChance")));

            _showDistribution = DrawProfileSection(SectionDistribution, "AUTHORITY & DISTRIBUTION",
                _showDistribution, ConvaiInspectorIconIds.Routing);
            if (_showDistribution)
                DrawSectionBody(() => DrawProperties(Find("minimumHeadContribution"), Find("neckShare")));

            _showUpperBodyFollow = DrawProfileSection(SectionUpperBodyFollow, "UPPER BODY FOLLOW",
                _showUpperBodyFollow, ConvaiInspectorIconIds.Routing);
            if (_showUpperBodyFollow)
                DrawSectionBody(() => DrawProperties(Find("enableUpperBodyFollow"), Find("upperBodyFollowShare"),
                    Find("upperBodyActivationDegrees"), Find("maxChestYaw"), Find("maxChestPitch"),
                    Find("maxUpperChestYaw"), Find("maxUpperChestPitch")));
        }
    }

    [CustomEditor(typeof(ConvaiDialogueAnimationProfile))]
    internal sealed class DialogueAnimationProfileInspector : ConvaiEmbodimentProfileEditorBase<ConvaiDialogueAnimationProfile>
    {
        internal const string SectionContent = "Content";
        internal const string SectionRuntime = "Runtime";
        internal const string SectionDiagnostics = "Diagnostics";

        private bool _showContent;
        private bool _showRuntime;
        private bool _showDiagnostics;

        protected override string HeaderTitle => "Dialogue Animation Profile";
        protected override string HeaderSubtitle => "Animation Content Routing";
        protected override string HeaderStatus => HasMissingCoreReferences() ? "Warning" : "Ready";
        protected override Color HeaderStatusColor => HasMissingCoreReferences() ? Warning : AccentEmphasis;

        protected override void OnEnable()
        {
            base.OnEnable();
            _showContent = LoadSectionState(SectionContent, true);
            _showRuntime = LoadSectionState(SectionRuntime, true);
            _showDiagnostics = LoadSectionState(SectionDiagnostics, true);
        }

        protected override void OnDisable()
        {
            SaveSectionState(SectionContent, _showContent);
            SaveSectionState(SectionRuntime, _showRuntime);
            SaveSectionState(SectionDiagnostics, _showDiagnostics);
            base.OnDisable();
        }

        protected override void DrawProfileInspector()
        {
            _showContent = DrawProfileSection(SectionContent, "CONTENT", _showContent,
                ConvaiInspectorIconIds.Content);
            if (_showContent)
                DrawSectionBody(() => DrawProperties(Find("library"), Find("foundationIdleClip"),
                    Find("characterGender")));

            _showRuntime = DrawProfileSection(SectionRuntime, "RUNTIME", _showRuntime,
                ConvaiInspectorIconIds.Animator);
            if (_showRuntime)
                DrawSectionBody(() => DrawProperties(Find("runtimeConfig"), Find("animatorContract"),
                    Find("autoCreateConversationFlow")));

            _showDiagnostics = DrawProfileSection(SectionDiagnostics, "DIAGNOSTICS", _showDiagnostics,
                ConvaiInspectorIconIds.Validation, Warning);
            if (_showDiagnostics)
            {
                DrawSectionBody(() =>
                {
                    if (Profile.Library == null)
                        DrawWarningBox("Library Missing", "Assign a Dialogue Animation Library before using this profile in a preset.");
                    if (Profile.RuntimeConfig == null)
                        DrawWarningBox("Runtime Config Missing", "Assign a runtime config or controller defaults will be used.");
                    if (Profile.AnimatorContract == null)
                        DrawWarningBox("Animator Contract Missing", "Assign a contract when animator layer or parameter policy must be explicit.");
                    if (!HasMissingCoreReferences())
                        DrawInfoBox("Profile Ready", "Content, runtime config, and animator contract are assigned.");
                });
            }
        }

        private bool HasMissingCoreReferences() =>
            Profile.Library == null || Profile.RuntimeConfig == null || Profile.AnimatorContract == null;
    }
}
