using System.Collections.Generic;
using Convai.Modules.DialogueAnimation.Core;
using UnityEngine;

namespace Convai.Modules.DialogueAnimation.Runtime.Driver
{
    /// <summary>
    ///     Encapsulates the deterministic build pipeline for a dialogue animation runtime:
    ///     animator+contract validation, override-controller cloning, slot wiring, ping-pong
    ///     instantiation, and foundation-clip resolution. Returns a
    ///     <see cref="DialogueRuntimeArtifacts" /> bundle on success, or an explanatory
    ///     warning string on failure.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The builder is pure: it never touches the controller or its fields, and it
    ///         destroys the cloned override controller on every failure path so the caller
    ///         does not need to track partial state.
    ///     </para>
    /// </remarks>
    internal static class DialogueRuntimeBuilder
    {
        public static bool TryBuild(
            Animator animator,
            in DialogueAnimatorContractView contract,
            in DialogueAnimatorLayerSet layers,
            AnimationClip foundationOverride,
            DialogueAnimationLibrary library,
            out DialogueRuntimeArtifacts artifacts,
            out string warning)
        {
            artifacts = default;
            warning = null;

            if (!DialogueAnimatorContractValidator.TryValidateAnimator(animator, in layers, out string contractError))
            {
                warning = contractError;
                return false;
            }

            AnimatorOverrideController overrideController =
                CloneOverrideController(animator.runtimeAnimatorController);
            AnimatorSlotOverrider slotOverrider = new(overrideController);

            if (!slotOverrider.HasSlot(contract.BasePlaceholderName))
            {
                warning = $"missing base placeholder '{contract.BasePlaceholderName}'.";
                DialogueOverrideControllerOwnership.DestroyOverrideController(overrideController);
                return false;
            }

            AnimatorStatePingPong idleOverlayPingPong = new(
                animator,
                slotOverrider,
                contract.IdleOverlayLayerIndex,
                new AnimatorStatePingPong.SlotDefinition(contract.IdleOverlayStateA, contract.IdleOverlayPlaceholderA),
                new AnimatorStatePingPong.SlotDefinition(contract.IdleOverlayStateB, contract.IdleOverlayPlaceholderB));

            AnimatorStatePingPong bodyTalkPingPong = new(
                animator,
                slotOverrider,
                contract.BodyTalkLayerIndex,
                new AnimatorStatePingPong.SlotDefinition(contract.BodyTalkStateA, contract.BodyTalkPlaceholderA),
                new AnimatorStatePingPong.SlotDefinition(contract.BodyTalkStateB, contract.BodyTalkPlaceholderB));

            AnimatorStatePingPong headTalkPingPong = new(
                animator,
                slotOverrider,
                contract.HeadTalkLayerIndex,
                new AnimatorStatePingPong.SlotDefinition(contract.HeadTalkStateA, contract.HeadTalkPlaceholderA),
                new AnimatorStatePingPong.SlotDefinition(contract.HeadTalkStateB, contract.HeadTalkPlaceholderB));

            if (!idleOverlayPingPong.IsWired
                || !bodyTalkPingPong.IsWired
                || !headTalkPingPong.IsWired)
            {
                warning = "could not wire animator slots. " +
                    $"Placeholders: '{contract.BasePlaceholderName}', '{contract.IdleOverlayPlaceholderA}', '{contract.IdleOverlayPlaceholderB}', " +
                    $"'{contract.BodyTalkPlaceholderA}', '{contract.BodyTalkPlaceholderB}', " +
                    $"'{contract.HeadTalkPlaceholderA}', '{contract.HeadTalkPlaceholderB}'.";
                DialogueOverrideControllerOwnership.DestroyOverrideController(overrideController);
                return false;
            }

            AnimationClip foundationClip = ResolveFoundationClip(foundationOverride, library);
            if (foundationClip == null)
            {
                warning = "has no foundation idle clip.";
                DialogueOverrideControllerOwnership.DestroyOverrideController(overrideController);
                return false;
            }

            artifacts = new DialogueRuntimeArtifacts(
                overrideController,
                slotOverrider,
                idleOverlayPingPong,
                bodyTalkPingPong,
                headTalkPingPong,
                foundationClip);
            return true;
        }

        private static AnimatorOverrideController CloneOverrideController(
            RuntimeAnimatorController sourceController)
        {
            const HideFlags runtimeHideFlags = HideFlags.HideAndDontSave | HideFlags.DontUnloadUnusedAsset;

            if (sourceController is AnimatorOverrideController sourceOverride)
            {
                var clone = new AnimatorOverrideController(sourceOverride.runtimeAnimatorController);
                clone.name = $"Convai Dialogue Runtime Override ({sourceOverride.name})";
                clone.hideFlags = runtimeHideFlags;
                var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                sourceOverride.GetOverrides(overrides);
                clone.ApplyOverrides(overrides);
                return clone;
            }

            var wrapper = new AnimatorOverrideController(sourceController);
            wrapper.name = sourceController != null
                ? $"Convai Dialogue Runtime Override ({sourceController.name})"
                : "Convai Dialogue Runtime Override";
            wrapper.hideFlags = runtimeHideFlags;
            return wrapper;
        }

        private static AnimationClip ResolveFoundationClip(
            AnimationClip foundationOverride,
            DialogueAnimationLibrary library)
        {
            if (foundationOverride != null)
                return foundationOverride;

            if (library?.IdleEntries == null)
                return null;

            IReadOnlyList<DialogueClipEntry> idles = library.IdleEntries;
            for (int i = 0; i < idles.Count; i++)
            {
                if (idles[i].IsValid)
                    return idles[i].Clip;
            }

            return null;
        }
    }
}
