using UnityEngine;

namespace Convai.Modules.DialogueAnimation.Runtime.Driver
{
    /// <summary>
    ///     Captures the state of a single talk turn: which animator talk layers are active,
    ///     the chosen clip, and the originating library index. Replaces the four scattered
    ///     fields the controller previously used to describe in-flight talk motion.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <see cref="LibraryIndex" /> is sentinel <c>-1</c> when no talk has played yet so
    ///         consumers can distinguish "first turn" from "library entry 0".
    ///     </para>
    /// </remarks>
    internal struct DialogueTalkSession
    {
        public bool UsesHeadTalkLayer;
        public bool UsesBodyTalkLayer;
        public AnimationClip Clip;
        public int LibraryIndex;

        public static DialogueTalkSession Empty => new() { LibraryIndex = -1 };

        public bool IsActive => UsesHeadTalkLayer || UsesBodyTalkLayer;
    }
}
