using Convai.Modules.Attention.Profiles;
using Convai.Modules.ConversationFlow.Profiles;
using Convai.Modules.DialogueAnimation.Profiles;
using Convai.Modules.Emotion.Profiles;
using Convai.Modules.Gaze.Profiles;

namespace Convai.Tests.EditMode.Fixtures
{
    /// <summary>
    ///     Fluent test-data builders for <c>Convai*Profile</c> ScriptableObjects.
    ///     All instances are created via <c>ScriptableObject.CreateInstance</c> using the
    ///     module's <c>CreateDefault()</c> factory so tests are free of project-asset coupling.
    ///     Remember to call <c>Object.DestroyImmediate</c> on the returned instance in [TearDown].
    /// </summary>
    public static class ProfileBuilders
    {
        public static AttentionProfileBuilder Attention() => new();
        public static GazeCoordinationProfileBuilder Gaze() => new();
        public static EmotionProfileBuilder Emotion() => new();
        public static DialogueAnimationProfileBuilder DialogueAnimation() => new();
        public static ConversationFlowProfileBuilder ConversationFlow() => new();
    }

    public sealed class AttentionProfileBuilder
    {
        private readonly ConvaiAttentionProfile _profile = ConvaiAttentionProfile.CreateDefault();

        public ConvaiAttentionProfile Build() => _profile;
    }

    public sealed class GazeCoordinationProfileBuilder
    {
        private readonly ConvaiGazeCoordinationProfile _profile = ConvaiGazeCoordinationProfile.CreateDefault();

        public ConvaiGazeCoordinationProfile Build() => _profile;
    }

    public sealed class EmotionProfileBuilder
    {
        private readonly ConvaiEmotionProfile _profile = ConvaiEmotionProfile.CreateDefault();

        public ConvaiEmotionProfile Build() => _profile;
    }

    public sealed class DialogueAnimationProfileBuilder
    {
        private readonly ConvaiDialogueAnimationProfile _profile = ConvaiDialogueAnimationProfile.CreateDefault();

        public ConvaiDialogueAnimationProfile Build() => _profile;
    }

    public sealed class ConversationFlowProfileBuilder
    {
        private readonly ConvaiConversationFlowProfile _profile = ConvaiConversationFlowProfile.CreateDefault();

        public ConvaiConversationFlowProfile Build() => _profile;
    }
}
