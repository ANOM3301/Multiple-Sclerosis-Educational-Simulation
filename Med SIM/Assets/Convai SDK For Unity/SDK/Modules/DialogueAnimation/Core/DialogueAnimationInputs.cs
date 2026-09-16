using Convai.Domain.Embodiment.Readings;
using Convai.Domain.Embodiment.Semantics;

namespace Convai.Modules.DialogueAnimation.Core
{
    /// <summary>
    ///     Per-frame inputs consumed by <see cref="DialogueAnimationOrchestrator" />. Captures
    ///     the upstream dialogue state and emotion reading along with the frame delta so the
    ///     orchestrator stays free of <see cref="UnityEngine"/> dependencies.
    /// </summary>
    /// <remarks>
    ///     Public fields (not properties) so callers can pass the embedded
    ///     <see cref="EmotionReading" /> through <c>in</c> parameters without copying.
    /// </remarks>
    internal readonly struct DialogueAnimationInputs
    {
        public readonly DialogueState Primary;
        public readonly EmotionReading Emotion;
        public readonly float DeltaTime;

        public DialogueAnimationInputs(DialogueState primary, in EmotionReading emotion, float deltaTime)
        {
            Primary = primary;
            Emotion = emotion;
            DeltaTime = deltaTime;
        }
    }
}
