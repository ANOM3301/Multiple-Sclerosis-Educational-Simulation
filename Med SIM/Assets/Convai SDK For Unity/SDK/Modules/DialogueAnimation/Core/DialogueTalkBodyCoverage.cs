namespace Convai.Modules.DialogueAnimation.Core
{
    /// <summary>
    ///     Declares which animator layers receive a talk <see cref="UnityEngine.AnimationClip" />
    ///     when using the four-layer stack (base idle, idle overlay, body talk, head talk).
    /// </summary>
    public enum DialogueTalkBodyCoverage
    {
        /// <summary>
        ///     Head talk layer only. Idle overlay and body talk layers stay at baseline weights.
        /// </summary>
        HeadOnly = 0,

        /// <summary>
        ///     Body talk layer and head talk layer play the same clip in lockstep.
        /// </summary>
        BodyAndHead = 1,

        /// <summary>
        ///     Body talk layer only. Head talk layer stays at baseline.
        /// </summary>
        BodyOnly = 2
    }
}
