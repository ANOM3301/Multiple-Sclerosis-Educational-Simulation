using System.Collections.Generic;

namespace Convai.Modules.DialogueAnimation.Core
{
    /// <summary>
    ///     Strategy contract for selecting the next idle or talk variant out of a
    ///     candidate pool.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Implementations are intended to be pure, allocation-free, and deterministic
    ///         for a given <see cref="VariantSelectionContext.Seed" />. That keeps the
    ///         selector trivially testable and makes AAA-style regression captures feasible.
    ///     </para>
    ///     <para>
    ///         Selectors are <em>not</em> responsible for scheduling (timing, cooldowns,
    ///         state transitions); the controller owns scheduling. Selectors only answer
    ///         "given these candidates and this context, which one?".
    ///     </para>
    /// </remarks>
    public interface IDialogueVariantSelector
    {
        /// <summary>
        ///     Picks one candidate from <paramref name="candidates" /> according to the
        ///     strategy.
        /// </summary>
        /// <param name="candidates">
        ///     Ordered list of eligible entries. The same indices are referenced by
        ///     <see cref="VariantSelectionContext.LastPlayedIndex" />.
        /// </param>
        /// <param name="context">Selection inputs (character gender, emotion, seed, last played).</param>
        /// <param name="selectedIndex">Index of the picked candidate, or <c>-1</c> on failure.</param>
        /// <param name="selected">The picked entry, or <c>default</c> on failure.</param>
        /// <returns><c>true</c> when a selection was made; <c>false</c> when no candidate is eligible.</returns>
        bool TrySelect(
            IReadOnlyList<DialogueClipEntry> candidates,
            in VariantSelectionContext context,
            out int selectedIndex,
            out DialogueClipEntry selected);
    }
}
