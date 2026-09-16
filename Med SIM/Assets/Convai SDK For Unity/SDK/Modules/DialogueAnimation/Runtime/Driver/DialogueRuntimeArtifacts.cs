using UnityEngine;

namespace Convai.Modules.DialogueAnimation.Runtime.Driver
{
    /// <summary>
    ///     Bundle of runtime objects produced by <see cref="DialogueRuntimeBuilder" />:
    ///     the cloned <see cref="AnimatorOverrideController" />, the slot overrider, the three
    ///     animator state ping-pongs, and the resolved foundation idle clip.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The struct is value-typed so the builder can return everything in a single
    ///         allocation-free package. The controller takes ownership of the override
    ///         controller through <see cref="DialogueOverrideControllerOwnership" />.
    ///     </para>
    /// </remarks>
    internal readonly struct DialogueRuntimeArtifacts
    {
        public AnimatorOverrideController OverrideController { get; }
        public AnimatorSlotOverrider SlotOverrider { get; }
        public AnimatorStatePingPong IdleOverlayPingPong { get; }
        public AnimatorStatePingPong BodyTalkPingPong { get; }
        public AnimatorStatePingPong HeadTalkPingPong { get; }
        public AnimationClip FoundationClip { get; }

        public DialogueRuntimeArtifacts(
            AnimatorOverrideController overrideController,
            AnimatorSlotOverrider slotOverrider,
            AnimatorStatePingPong idleOverlayPingPong,
            AnimatorStatePingPong bodyTalkPingPong,
            AnimatorStatePingPong headTalkPingPong,
            AnimationClip foundationClip)
        {
            OverrideController = overrideController;
            SlotOverrider = slotOverrider;
            IdleOverlayPingPong = idleOverlayPingPong;
            BodyTalkPingPong = bodyTalkPingPong;
            HeadTalkPingPong = headTalkPingPong;
            FoundationClip = foundationClip;
        }
    }
}
