using Convai.Modules.DialogueAnimation.Runtime.Driver;

namespace Convai.Modules.DialogueAnimation.Core
{
    /// <summary>
    ///     Per-frame command payload emitted by <see cref="DialogueAnimationOrchestrator" /> for
    ///     <see cref="DialogueAnimatorWriter" /> to apply. Encapsulates everything the writer
    ///     needs to drive the four-layer animator stack without inspecting upstream state.
    /// </summary>
    /// <remarks>
    ///     Public fields (not properties) so the embedded <see cref="DialogueTalkSession" />
    ///     can be passed through <c>in</c> parameters without copying.
    /// </remarks>
    internal readonly struct DialogueAnimationCommands
    {
        public readonly DialogueTalkSession Talk;
        public readonly bool Speaking;
        public readonly float SpeechLayerScale;
        public readonly bool HasValidIdleLibrary;

        public DialogueAnimationCommands(
            in DialogueTalkSession talk,
            bool speaking,
            float speechLayerScale,
            bool hasValidIdleLibrary)
        {
            Talk = talk;
            Speaking = speaking;
            SpeechLayerScale = speechLayerScale;
            HasValidIdleLibrary = hasValidIdleLibrary;
        }
    }
}
