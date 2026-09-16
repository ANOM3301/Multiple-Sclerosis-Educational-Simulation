using System.Collections.Generic;
using Convai.Domain.Embodiment.Readings;
using Convai.Modules.DialogueAnimation.Core;
using Convai.Runtime.Embodiment;
using UnityEngine;

namespace Convai.Modules.DialogueAnimation.Runtime.Driver
{
    /// <summary>
    ///     Orchestrates the head- and body-talk pass when a turn starts: rebuilds the
    ///     scratch pools on <see cref="DialogueAnimationClipPicker" />, draws an emotion-biased
    ///     pick from each pool, and crossfades the resulting clips on the matching animator
    ///     ping-pongs.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The starter is pure logic: it owns no fields. The caller supplies the picker,
    ///         the two ping-pongs, the runtime config, the variant selector, and the RNG, and
    ///         receives a <see cref="DialogueTalkSession" /> describing which talk layers were
    ///         armed. When neither layer succeeded, the result's <see cref="DialogueTalkSession.IsActive" />
    ///         is <c>false</c> so the caller can log a content-coverage warning.
    ///     </para>
    /// </remarks>
    internal static class DialogueTalkVariantStarter
    {
        private const float DefaultTalkFadeSeconds = 0.25f;

        public static DialogueTalkSession Start(
            DialogueAnimationLibrary library,
            DialogueAnimationClipPicker picker,
            AnimatorStatePingPong headTalkPingPong,
            AnimatorStatePingPong bodyTalkPingPong,
            DialogueAnimationRuntimeConfig config,
            IDialogueVariantSelector selector,
            CharacterGender characterGender,
            ref DeterministicEmbodimentRandom rng,
            in EmotionReading emotion)
        {
            DialogueTalkSession session = DialogueTalkSession.Empty;

            if (library == null || !library.HasAnyValidTalk()) return session;

            IReadOnlyList<DialogueClipEntry> talk = library.TalkEntries;
            picker.RebuildHeadTalkEligibleScratch(talk);
            picker.RebuildBodyOnlyTalkScratch(talk);

            float talkFadeDefault = config?.TalkCrossFadeDuration ?? DefaultTalkFadeSeconds;

            TryStartFromHeadEligiblePool(
                picker,
                headTalkPingPong,
                bodyTalkPingPong,
                config,
                selector,
                characterGender,
                ref rng,
                in emotion,
                talkFadeDefault,
                ref session);

            if (!session.UsesBodyTalkLayer)
            {
                TryStartFromBodyOnlyPool(
                    picker,
                    bodyTalkPingPong,
                    config,
                    selector,
                    characterGender,
                    ref rng,
                    in emotion,
                    talkFadeDefault,
                    ref session);
            }

            return session;
        }

        private static void TryStartFromHeadEligiblePool(
            DialogueAnimationClipPicker picker,
            AnimatorStatePingPong headTalkPingPong,
            AnimatorStatePingPong bodyTalkPingPong,
            DialogueAnimationRuntimeConfig config,
            IDialogueVariantSelector selector,
            CharacterGender characterGender,
            ref DeterministicEmbodimentRandom rng,
            in EmotionReading emotion,
            float talkFadeDefault,
            ref DialogueTalkSession session)
        {
            if (picker.HeadTalkEligibleCount <= 0) return;

            if (!picker.TryPickFromPool(
                    picker.HeadTalkEligible,
                    picker.LastHeadTalkEligiblePickIndex,
                    in emotion,
                    selector,
                    config,
                    characterGender,
                    ref rng,
                    out DialogueClipEntry headPick,
                    out int headScratchIndex))
            {
                return;
            }

            AnimationClip clip = headPick.Clip;
            if (clip == null) return;

            float fade = ResolveFade(headPick, talkFadeDefault);

            switch (headPick.TalkBodyCoverage)
            {
                case DialogueTalkBodyCoverage.HeadOnly:
                    if (!headTalkPingPong.CrossFadeTo(clip, fade)) return;
                    picker.LastHeadTalkEligiblePickIndex = headScratchIndex;
                    session.UsesHeadTalkLayer = true;
                    session.Clip = clip;
                    session.LibraryIndex = picker.ResolveHeadTalkLibraryIndex(headScratchIndex);
                    break;

                case DialogueTalkBodyCoverage.BodyAndHead:
                    if (!headTalkPingPong.CrossFadeTo(clip, fade)) return;
                    if (!bodyTalkPingPong.CrossFadeTo(clip, fade)) return;
                    picker.LastHeadTalkEligiblePickIndex = headScratchIndex;
                    session.UsesHeadTalkLayer = true;
                    session.UsesBodyTalkLayer = true;
                    session.Clip = clip;
                    session.LibraryIndex = picker.ResolveHeadTalkLibraryIndex(headScratchIndex);
                    break;
            }
        }

        private static void TryStartFromBodyOnlyPool(
            DialogueAnimationClipPicker picker,
            AnimatorStatePingPong bodyTalkPingPong,
            DialogueAnimationRuntimeConfig config,
            IDialogueVariantSelector selector,
            CharacterGender characterGender,
            ref DeterministicEmbodimentRandom rng,
            in EmotionReading emotion,
            float talkFadeDefault,
            ref DialogueTalkSession session)
        {
            if (picker.BodyOnlyTalkCount <= 0) return;

            if (!picker.TryPickFromPool(
                    picker.BodyOnlyTalk,
                    picker.LastBodyOnlyPickIndex,
                    in emotion,
                    selector,
                    config,
                    characterGender,
                    ref rng,
                    out DialogueClipEntry upperPick,
                    out int upperScratchIndex))
            {
                return;
            }

            AnimationClip clip = upperPick.Clip;
            if (clip == null) return;

            float fade = ResolveFade(upperPick, talkFadeDefault);
            if (!bodyTalkPingPong.CrossFadeTo(clip, fade)) return;

            picker.LastBodyOnlyPickIndex = upperScratchIndex;
            session.UsesBodyTalkLayer = true;
            if (session.Clip == null)
            {
                session.Clip = clip;
                session.LibraryIndex = picker.ResolveBodyOnlyLibraryIndex(upperScratchIndex);
            }
        }

        private static float ResolveFade(in DialogueClipEntry entry, float fallback) =>
            entry.CrossFadeDurationOverride > 0f
                ? entry.CrossFadeDurationOverride
                : Mathf.Max(0f, fallback);
    }
}
