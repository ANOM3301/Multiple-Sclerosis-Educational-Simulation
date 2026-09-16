using Convai.Domain.Embodiment.Semantics;
using System.Collections.Generic;
using UnityEngine;

namespace Convai.Modules.Gaze.Profiles
{
    /// <summary>
    ///     Authoring asset controlling how the <c>ConvaiGazeCoordinator</c> converts attention
    ///     and conversation-flow signals into a <see cref="Convai.Domain.Embodiment.Readings.GazeIntent" />.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Convai/Embodiment/Gaze Coordination Profile",
        fileName = "ConvaiGazeCoordinationProfile")]
    public sealed class ConvaiGazeCoordinationProfile : ScriptableObject
    {
        [System.Serializable]
        public struct DialogueStateWeights
        {
            [Tooltip("DialogueState this entry applies to.")]
            public DialogueState State;

            [Range(0f, 1f)]
            [Tooltip("Gaze authority toward the attention target (0–1), multiplied by attention commitment in ConvaiGazeCoordinator. Ramp through engaged states (e.g. ~0.9 → ~0.95 → 1 on Speaking) for graded camera lock; use 1 on Speaking for full lens lock when commitment is high.")]
            public float OverallWeight;

            [Range(0f, 1f)]
            [Tooltip("Fraction of authority handled by eyes alone. Higher = more glances, less head commitment.")]
            public float EyeShare;

            [Tooltip("When enabled, this state releases camera/player focus and lets ambient gaze take over.")]
            public bool SuppressAttentionTarget;
        }

        [Header("Per-State Weights")]
        [SerializeField]
        [Tooltip("Per-dialogue-state gaze authority. Unlisted states fall back to Idle defaults.")]
        private List<DialogueStateWeights> stateWeights = new()
        {
            new DialogueStateWeights { State = DialogueState.Idle,        OverallWeight = 0.45f, EyeShare = 0.85f, SuppressAttentionTarget = true },
            new DialogueStateWeights { State = DialogueState.Listening,   OverallWeight = 0.93f, EyeShare = 0.52f },
            new DialogueStateWeights { State = DialogueState.Attending,   OverallWeight = 0.9f,  EyeShare = 0.62f },
            new DialogueStateWeights { State = DialogueState.Thinking,    OverallWeight = 0.95f, EyeShare = 0.8f },
            new DialogueStateWeights { State = DialogueState.Speaking,    OverallWeight = 1f,    EyeShare = 0.45f },
            new DialogueStateWeights { State = DialogueState.Reacting,    OverallWeight = 0.95f, EyeShare = 0.48f },
            new DialogueStateWeights { State = DialogueState.Interrupted, OverallWeight = 0.88f, EyeShare = 0.85f },
            new DialogueStateWeights { State = DialogueState.Settling,    OverallWeight = 0.93f, EyeShare = 0.7f },
        };

        [Header("Smoothing")]
        [SerializeField, Range(0f, 20f)]
        [Tooltip("Exponential smoothing speed applied to OverallWeight changes.")]
        private float weightBlendSpeed = 6f;

        [SerializeField, Range(0f, 20f)]
        [Tooltip("Exponential smoothing speed applied to EyeShare changes.")]
        private float eyeShareBlendSpeed = 4f;

        public float WeightBlendSpeed => weightBlendSpeed;
        public float EyeShareBlendSpeed => eyeShareBlendSpeed;

        /// <summary>Resolves per-state policy, falling back to Idle when the state is unknown.</summary>
        public void GetStatePolicy(
            DialogueState state,
            out float overallWeight,
            out float eyeShare,
            out bool suppressAttentionTarget)
        {
            DialogueStateWeights fallback = default;
            bool foundFallback = false;

            for (int i = 0; i < stateWeights.Count; i++)
            {
                DialogueStateWeights entry = stateWeights[i];
                if (entry.State == state)
                {
                    overallWeight = Mathf.Clamp01(entry.OverallWeight);
                    eyeShare = Mathf.Clamp01(entry.EyeShare);
                    suppressAttentionTarget = entry.SuppressAttentionTarget;
                    return;
                }
                if (entry.State == DialogueState.Idle && !foundFallback)
                {
                    fallback = entry;
                    foundFallback = true;
                }
            }

            overallWeight = foundFallback ? Mathf.Clamp01(fallback.OverallWeight) : 0.5f;
            eyeShare = foundFallback ? Mathf.Clamp01(fallback.EyeShare) : 0.7f;
            suppressAttentionTarget = foundFallback && fallback.SuppressAttentionTarget;
        }

        /// <summary>Resolves per-state weights, falling back to Idle when the state is unknown.</summary>
        public void GetWeights(DialogueState state, out float overallWeight, out float eyeShare)
        {
            GetStatePolicy(state, out overallWeight, out eyeShare, out _);
        }

        private void OnValidate()
        {
            weightBlendSpeed = Mathf.Max(0f, weightBlendSpeed);
            eyeShareBlendSpeed = Mathf.Max(0f, eyeShareBlendSpeed);
            if (stateWeights == null) return;
            for (int i = 0; i < stateWeights.Count; i++)
            {
                DialogueStateWeights entry = stateWeights[i];
                entry.OverallWeight = Mathf.Clamp01(entry.OverallWeight);
                entry.EyeShare = Mathf.Clamp01(entry.EyeShare);
                stateWeights[i] = entry;
            }
        }

        /// <summary>Creates a runtime default profile.</summary>
        public static ConvaiGazeCoordinationProfile CreateDefault()
        {
            ConvaiGazeCoordinationProfile instance = CreateInstance<ConvaiGazeCoordinationProfile>();
            instance.hideFlags = HideFlags.HideAndDontSave;
            return instance;
        }

        /// <summary>
        ///     Conversational tuning: Idle suppresses the attention point; engaged states ramp
        ///     gaze authority toward the focus target (e.g. camera), building to full lock
        ///     on <see cref="DialogueState.Speaking" />. Blend speeds are slightly slower than
        ///     defaults for smooth crossfades.
        /// </summary>
        public static ConvaiGazeCoordinationProfile CreateConversationalPreset()
        {
            ConvaiGazeCoordinationProfile instance = CreateInstance<ConvaiGazeCoordinationProfile>();
            instance.stateWeights = new List<DialogueStateWeights>
            {
                new() { State = DialogueState.Idle,        OverallWeight = 0.45f, EyeShare = 0.85f, SuppressAttentionTarget = true  },
                new() { State = DialogueState.Listening,   OverallWeight = 0.93f, EyeShare = 0.52f, SuppressAttentionTarget = false },
                new() { State = DialogueState.Attending,   OverallWeight = 0.9f,  EyeShare = 0.62f, SuppressAttentionTarget = false },
                new() { State = DialogueState.Thinking,    OverallWeight = 0.95f, EyeShare = 0.8f,  SuppressAttentionTarget = false },
                new() { State = DialogueState.Speaking,    OverallWeight = 1f,    EyeShare = 0.45f, SuppressAttentionTarget = false },
                new() { State = DialogueState.Reacting,    OverallWeight = 0.95f, EyeShare = 0.48f, SuppressAttentionTarget = false },
                new() { State = DialogueState.Interrupted, OverallWeight = 0.88f, EyeShare = 0.85f, SuppressAttentionTarget = false },
                new() { State = DialogueState.Settling,    OverallWeight = 0.93f, EyeShare = 0.7f,  SuppressAttentionTarget = false },
            };
            instance.weightBlendSpeed = 5f;
            instance.eyeShareBlendSpeed = 3.5f;
            return instance;
        }
    }
}
