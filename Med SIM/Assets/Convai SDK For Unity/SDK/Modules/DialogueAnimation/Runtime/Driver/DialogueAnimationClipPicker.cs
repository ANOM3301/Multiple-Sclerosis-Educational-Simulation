using System.Collections.Generic;
using Convai.Domain.Embodiment.Readings;
using Convai.Modules.DialogueAnimation.Core;
using Convai.Runtime.Embodiment;

namespace Convai.Modules.DialogueAnimation.Runtime.Driver
{
    /// <summary>
    ///     Filters a talk clip pool by <see cref="DialogueTalkBodyCoverage" /> for the head and
    ///     body talk passes, tracks the last library index picked from each scratch pool,
    ///     and delegates the actual variant draw to an <see cref="IDialogueVariantSelector" />.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The picker keeps the controller as a thin orchestration shell: pool partitioning,
    ///         scratch-list reuse, and library-index resolution all live here. The selector
    ///         (provided by the caller) implements the actual gender/emotion/last-played weighting.
    ///     </para>
    ///     <para>
    ///         Promoted to public so SDK consumers and tests can construct the picker directly
    ///         when integrating custom variant selectors or stress-testing pool partitioning.
    ///     </para>
    /// </remarks>
    public sealed class DialogueAnimationClipPicker
    {
        private readonly List<DialogueClipEntry> _scratchHeadTalkEligible = new(16);
        private readonly List<DialogueClipEntry> _scratchBodyOnlyTalk = new(16);
        private readonly List<int> _scratchHeadTalkEligibleLibraryIndices = new(16);
        private readonly List<int> _scratchBodyOnlyLibraryIndices = new(16);

        public int HeadTalkEligibleCount => _scratchHeadTalkEligible.Count;

        public int BodyOnlyTalkCount => _scratchBodyOnlyTalk.Count;

        public IReadOnlyList<DialogueClipEntry> HeadTalkEligible => _scratchHeadTalkEligible;

        public IReadOnlyList<DialogueClipEntry> BodyOnlyTalk => _scratchBodyOnlyTalk;

        public int LastHeadTalkEligiblePickIndex { get; set; } = -1;

        public int LastBodyOnlyPickIndex { get; set; } = -1;

        public void ResetPickIndices()
        {
            LastHeadTalkEligiblePickIndex = -1;
            LastBodyOnlyPickIndex = -1;
        }

        public void RebuildHeadTalkEligibleScratch(IReadOnlyList<DialogueClipEntry> talk)
        {
            _scratchHeadTalkEligible.Clear();
            _scratchHeadTalkEligibleLibraryIndices.Clear();
            if (talk == null) return;

            for (int i = 0; i < talk.Count; i++)
            {
                DialogueClipEntry e = talk[i];
                if (!e.IsValid) continue;

                if (e.TalkBodyCoverage is DialogueTalkBodyCoverage.HeadOnly
                    or DialogueTalkBodyCoverage.BodyAndHead)
                {
                    _scratchHeadTalkEligible.Add(e);
                    _scratchHeadTalkEligibleLibraryIndices.Add(i);
                }
            }
        }

        public void RebuildBodyOnlyTalkScratch(IReadOnlyList<DialogueClipEntry> talk)
        {
            _scratchBodyOnlyTalk.Clear();
            _scratchBodyOnlyLibraryIndices.Clear();
            if (talk == null) return;

            for (int i = 0; i < talk.Count; i++)
            {
                DialogueClipEntry e = talk[i];
                if (!e.IsValid) continue;

                if (e.TalkBodyCoverage == DialogueTalkBodyCoverage.BodyOnly)
                {
                    _scratchBodyOnlyTalk.Add(e);
                    _scratchBodyOnlyLibraryIndices.Add(i);
                }
            }
        }

        public int ResolveHeadTalkLibraryIndex(int scratchIndex) =>
            scratchIndex >= 0 && scratchIndex < _scratchHeadTalkEligibleLibraryIndices.Count
                ? _scratchHeadTalkEligibleLibraryIndices[scratchIndex]
                : -1;

        public int ResolveBodyOnlyLibraryIndex(int scratchIndex) =>
            scratchIndex >= 0 && scratchIndex < _scratchBodyOnlyLibraryIndices.Count
                ? _scratchBodyOnlyLibraryIndices[scratchIndex]
                : -1;

        public bool TryPickIdle(
            IReadOnlyList<DialogueClipEntry> idleEntries,
            int lastIdleIndex,
            in EmotionReading emotion,
            IDialogueVariantSelector selector,
            DialogueAnimationRuntimeConfig config,
            CharacterGender characterGender,
            ref DeterministicEmbodimentRandom rng,
            out DialogueClipEntry entry,
            out int index) =>
            TryPickFromPool(
                idleEntries,
                lastIdleIndex,
                in emotion,
                selector,
                config,
                characterGender,
                ref rng,
                out entry,
                out index);

        public bool TryPickFromPool(
            IReadOnlyList<DialogueClipEntry> pool,
            int lastPlayedIndex,
            in EmotionReading emotion,
            IDialogueVariantSelector selector,
            DialogueAnimationRuntimeConfig config,
            CharacterGender characterGender,
            ref DeterministicEmbodimentRandom rng,
            out DialogueClipEntry entry,
            out int index)
        {
            entry = default;
            index = -1;

            if (pool == null || pool.Count == 0 || selector == null) return false;

            float bias = config?.EmotionBiasStrength ?? 1.5f;
            var ctx = new VariantSelectionContext(
                characterGender,
                in emotion,
                lastPlayedIndex,
                bias,
                rng.NextUInt());

            return selector.TrySelect(pool, in ctx, out index, out entry);
        }
    }
}
