using Convai.Domain.Embodiment.Interfaces;
using Convai.Runtime.Animation;
using Convai.Runtime.Embodiment;
using UnityEngine;

namespace Convai.Modules.DialogueAnimation.Runtime.Driver
{
    /// <summary>
    ///     Resolves the active <see cref="ISpeechEnergyProvider" /> from the configured policy
    ///     and the <see cref="EmbodimentContext" />, samples it each tick, and computes
    ///     the talk-layer scale used to modulate animator weights against live speech energy.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         When <see cref="DialogueAnimationRuntimeConfig.UseLipSyncSpeechEnergy" /> is off
    ///         the resolver collapses to a constant 1.0 scale and a
    ///         <see cref="NullSpeechEnergyProvider" />, so the controller never needs to branch
    ///         on the policy.
    ///     </para>
    /// </remarks>
    internal sealed class DialogueSpeechEnergyResolver
    {
        private ISpeechEnergyProvider _provider = NullSpeechEnergyProvider.Instance;

        public ISpeechEnergyProvider Provider => _provider;

        /// <summary>
        ///     Resolves the provider for the given config / context, optionally adopting an
        ///     explicit provider supplied by an event callback.
        /// </summary>
        public ISpeechEnergyProvider Resolve(
            DialogueAnimationRuntimeConfig config,
            EmbodimentContext context,
            ISpeechEnergyProvider explicitProvider = null)
        {
            if (config == null || !config.UseLipSyncSpeechEnergy)
            {
                _provider = NullSpeechEnergyProvider.Instance;
                return _provider;
            }

            ISpeechEnergyProvider resolved =
                explicitProvider ?? context?.EnsureSpeechEnergyProvider() ?? NullSpeechEnergyProvider.Instance;

            if (resolved is IConfigurableSpeechEnergyProvider configurable)
                configurable.Configure(config.SpeechEnergyWindowSeconds);

            _provider = resolved;
            return _provider;
        }

        public void Sample(float deltaTime) => _provider?.Sample(deltaTime);

        public void ResetToNull() => _provider = NullSpeechEnergyProvider.Instance;

        /// <summary>
        ///     Computes the talk-layer scale (0..1) for the current frame using
        ///     <see cref="ISpeechEnergyProvider.Current" /> gain and, when configured, the
        ///     facial dialogue phase as a floor.
        /// </summary>
        public float ComputeLayerScale(
            DialogueAnimationRuntimeConfig config,
            EmbodimentContext context)
        {
            if (config == null || !config.UseLipSyncSpeechEnergy)
                return 1f;

            float energy = (_provider?.Current ?? 0f) * config.SpeechEnergyGain;
            if (!config.IgnoreFacialDialoguePhaseForTalkLayerScale)
            {
                IDialoguePhaseProvider dialoguePhase = context?.DialoguePhase;
                if (dialoguePhase != null)
                    energy = Mathf.Max(energy, dialoguePhase.SpeechBlendFactor);
            }

            return Mathf.Lerp(
                config.SpeechEnergyMinimumTalkLayerScale,
                1f,
                Mathf.Clamp01(energy));
        }
    }
}
