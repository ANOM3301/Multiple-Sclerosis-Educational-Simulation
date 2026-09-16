using UnityEngine;

namespace Convai.Modules.DialogueAnimation.Runtime
{
    internal readonly struct DialogueAnimatorLayerSet
    {
        public DialogueAnimatorLayerSet(int baseIdle, int idleOverlay, int bodyTalk, int headTalk)
        {
            BaseIdle = baseIdle;
            IdleOverlay = idleOverlay;
            BodyTalk = bodyTalk;
            HeadTalk = headTalk;
        }

        public int BaseIdle { get; }
        public int IdleOverlay { get; }
        public int BodyTalk { get; }
        public int HeadTalk { get; }

        public override string ToString() =>
            $"baseIdle={BaseIdle}, idleOverlay={IdleOverlay}, bodyTalk={BodyTalk}, headTalk={HeadTalk}";
    }

    internal static class DialogueAnimatorContractValidator
    {
        public const int RequiredLayerCount = 4;

        public static bool TryValidateAnimator(
            Animator animator,
            in DialogueAnimatorLayerSet layers,
            out string message)
        {
            if (animator == null)
            {
                message = "could not resolve an Animator.";
                return false;
            }

            if (animator.runtimeAnimatorController == null)
            {
                message = "animator has no runtimeAnimatorController.";
                return false;
            }

            int layerCount = animator.layerCount;
            if (layerCount < RequiredLayerCount)
            {
                message =
                    $"requires at least {RequiredLayerCount} animator layers " +
                    $"(base idle, idle overlay, body talk, head talk). layerCount={layerCount}.";
                return false;
            }

            if (!AreLayersInRange(in layers, layerCount))
            {
                message = $"invalid layer indices ({layers}, layerCount={layerCount}).";
                return false;
            }

            if (!AreLayersDistinct(in layers))
            {
                message = "animator layer indices must be unique.";
                return false;
            }

            message = null;
            return true;
        }

        public static bool AreLayersDistinct(in DialogueAnimatorLayerSet layers)
        {
            int b = layers.BaseIdle;
            int i = layers.IdleOverlay;
            int u = layers.BodyTalk;
            int h = layers.HeadTalk;
            return b != i && b != u && b != h && i != u && i != h && u != h;
        }

        private static bool AreLayersInRange(in DialogueAnimatorLayerSet layers, int layerCount) =>
            IsLayerInRange(layers.BaseIdle, layerCount)
            && IsLayerInRange(layers.IdleOverlay, layerCount)
            && IsLayerInRange(layers.BodyTalk, layerCount)
            && IsLayerInRange(layers.HeadTalk, layerCount);

        private static bool IsLayerInRange(int layerIndex, int layerCount) =>
            layerIndex >= 0 && layerIndex < layerCount;
    }
}
