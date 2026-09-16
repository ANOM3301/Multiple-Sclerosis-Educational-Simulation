using System;
using UnityEngine;

namespace Convai.Runtime.Animation
{
    /// <summary>
    ///     Per-region blend weights that control how Emotion, FacialClip, LipSync,
    ///     and Custom layers contribute to a facial region. The compositor interpolates between
    ///     <c>Idle*</c> and <c>Speaking*</c> weights using the smoothed speech blend factor.
    /// </summary>
    [Serializable]
    public struct RegionBlendConfig
    {
        [Header("Idle (not speaking)")]
        [Range(0f, 1f)] public float IdleEmotionWeight;
        [Range(0f, 1f)] public float IdleFacialClipWeight;
        [Range(0f, 1f)] public float IdleLipSyncWeight;
        [Range(0f, 1f)] public float IdleCustomWeight;

        [Header("Speaking")]
        [Range(0f, 1f)] public float SpeakingEmotionWeight;
        [Range(0f, 1f)] public float SpeakingFacialClipWeight;
        [Range(0f, 1f)] public float SpeakingLipSyncWeight;
        [Range(0f, 1f)] public float SpeakingCustomWeight;

        [Header("Blend Mode")]
        public FacialBlendMode Mode;

        [Header("Normalization")]
        [Tooltip("When enabled, the composed result is clamped to 100 and excess is proportionally reduced.")]
        public bool EnableNormalization;

        public static RegionBlendConfig Create(
            float idleEmotion, float idleFacialClip, float idleLipSync,
            float speakingEmotion, float speakingFacialClip, float speakingLipSync,
            FacialBlendMode mode = FacialBlendMode.WeightedAdditive,
            float idleCustom = 0f, float speakingCustom = 0f,
            bool enableNormalization = false)
        {
            return new RegionBlendConfig
            {
                IdleEmotionWeight = Mathf.Clamp01(idleEmotion),
                IdleFacialClipWeight = Mathf.Clamp01(idleFacialClip),
                IdleLipSyncWeight = Mathf.Clamp01(idleLipSync),
                IdleCustomWeight = Mathf.Clamp01(idleCustom),
                SpeakingEmotionWeight = Mathf.Clamp01(speakingEmotion),
                SpeakingFacialClipWeight = Mathf.Clamp01(speakingFacialClip),
                SpeakingLipSyncWeight = Mathf.Clamp01(speakingLipSync),
                SpeakingCustomWeight = Mathf.Clamp01(speakingCustom),
                Mode = mode,
                EnableNormalization = enableNormalization
            };
        }

        /// <summary>
        ///     Returns the effective weight for each content layer at the given speech blend factor.
        /// </summary>
        public void GetInterpolatedWeights(
            float speechFactor,
            out float emotionWeight,
            out float facialClipWeight,
            out float lipSyncWeight,
            out float customWeight)
        {
            emotionWeight = Mathf.Lerp(IdleEmotionWeight, SpeakingEmotionWeight, speechFactor);
            facialClipWeight = Mathf.Lerp(IdleFacialClipWeight, SpeakingFacialClipWeight, speechFactor);
            lipSyncWeight = Mathf.Lerp(IdleLipSyncWeight, SpeakingLipSyncWeight, speechFactor);
            customWeight = Mathf.Lerp(IdleCustomWeight, SpeakingCustomWeight, speechFactor);
        }

        /// <summary>
        ///     Composes the final blendshape value from all weighted layer contributions.
        /// </summary>
        public float Compose(
            float emotionVal, float emotionWeight,
            float facialClipVal, float facialClipWeight,
            float lipSyncVal, float lipSyncWeight,
            float customVal, float customWeight)
        {
            float result;
            switch (Mode)
            {
                case FacialBlendMode.WeightedAdditive:
                    result = (emotionVal * emotionWeight) +
                             (facialClipVal * facialClipWeight) +
                             (lipSyncVal * lipSyncWeight) +
                             (customVal * customWeight);
                    break;

                case FacialBlendMode.Max:
                    result = Mathf.Max(
                        emotionVal * emotionWeight,
                        Mathf.Max(
                            facialClipVal * facialClipWeight,
                            Mathf.Max(
                                lipSyncVal * lipSyncWeight,
                                customVal * customWeight)));
                    break;

                case FacialBlendMode.Override:
                {
                    float ls = lipSyncVal * lipSyncWeight;
                    if (ls > 0.0001f) { result = ls; break; }
                    float em = emotionVal * emotionWeight;
                    if (em > 0.0001f) { result = em; break; }
                    float fc = facialClipVal * facialClipWeight;
                    if (fc > 0.0001f) { result = fc; break; }
                    result = customVal * customWeight;
                    break;
                }

                default:
                    result = emotionVal * emotionWeight;
                    break;
            }

            if (EnableNormalization && result > 100f)
                result = 100f;

            return result;
        }
    }
}
