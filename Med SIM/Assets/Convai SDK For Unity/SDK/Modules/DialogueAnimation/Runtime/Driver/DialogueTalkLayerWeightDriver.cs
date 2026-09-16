using UnityEngine;

namespace Convai.Modules.DialogueAnimation.Runtime.Driver
{
    /// <summary>
    ///     Owns the head- and body-talk layer weight blenders. Each tick, sets fade
    ///     targets based on the active <see cref="DialogueTalkSession" /> + speaking flag,
    ///     advances both blenders, and writes the resulting weights through the
    ///     <see cref="DialogueConductorBinding" /> so the controller never touches the animator
    ///     directly for talk weights.
    /// </summary>
    internal sealed class DialogueTalkLayerWeightDriver
    {
        private AnimatorLayerBlender _headTalkBlender;
        private AnimatorLayerBlender _bodyTalkBlender;

        public float CurrentHeadWeight => _headTalkBlender?.CurrentWeight ?? 0f;

        public float CurrentBodyWeight => _bodyTalkBlender?.CurrentWeight ?? 0f;

        public void Initialize()
        {
            _headTalkBlender = new AnimatorLayerBlender();
            _headTalkBlender.ForceWeight(0f);
            _bodyTalkBlender = new AnimatorLayerBlender();
            _bodyTalkBlender.ForceWeight(0f);
        }

        public void Reset()
        {
            _headTalkBlender = null;
            _bodyTalkBlender = null;
        }

        /// <summary>
        ///     Pushes new fade targets into both talk blenders for this frame. Callers should
        ///     follow with <see cref="FlushAndWrite" /> to advance the blenders and write the
        ///     resulting weights to the animator.
        /// </summary>
        public void UpdateTargets(
            in DialogueTalkSession session,
            bool targetSpeaking,
            float speechLayerScale,
            DialogueAnimationRuntimeConfig config)
        {
            if (config == null) return;

            float scale = Mathf.Clamp01(speechLayerScale);
            float headPeak = config.HeadTalkLayerPeakWeight * scale;
            float bodyPeak = config.BodyTalkLayerPeakWeight * scale;

            bool wantHead = targetSpeaking && session.UsesHeadTalkLayer;
            bool wantBody = targetSpeaking && session.UsesBodyTalkLayer;

            if (_headTalkBlender != null)
            {
                if (wantHead)
                    _headTalkBlender.SetTarget(headPeak, config.HeadTalkLayerFadeInSeconds);
                else
                    _headTalkBlender.SetTarget(0f, config.HeadTalkLayerFadeOutSeconds);
            }

            if (_bodyTalkBlender != null)
            {
                if (wantBody)
                    _bodyTalkBlender.SetTarget(bodyPeak, config.BodyTalkLayerFadeInSeconds);
                else
                    _bodyTalkBlender.SetTarget(0f, config.BodyTalkLayerFadeOutSeconds);
            }
        }

        public void FlushAndWrite(
            float deltaTime,
            DialogueConductorBinding writer,
            Object owner,
            int headTalkLayerIndex,
            int bodyTalkLayerIndex)
        {
            if (_headTalkBlender != null)
                writer.WriteWeight(owner, headTalkLayerIndex, _headTalkBlender.Tick(deltaTime));

            if (_bodyTalkBlender != null)
                writer.WriteWeight(owner, bodyTalkLayerIndex, _bodyTalkBlender.Tick(deltaTime));
        }

        public void WriteZeroedWeights(
            DialogueConductorBinding writer,
            Object owner,
            int headTalkLayerIndex,
            int bodyTalkLayerIndex)
        {
            writer.WriteWeight(owner, headTalkLayerIndex, 0f);
            writer.WriteWeight(owner, bodyTalkLayerIndex, 0f);
        }
    }
}
