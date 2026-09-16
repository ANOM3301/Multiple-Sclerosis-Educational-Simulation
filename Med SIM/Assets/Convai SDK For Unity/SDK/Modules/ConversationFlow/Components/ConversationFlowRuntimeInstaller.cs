using Convai.Domain.Embodiment.Interfaces;
using Convai.Runtime.Embodiment;
using UnityEngine;

namespace Convai.Modules.ConversationFlow.Components
{
    internal static class ConversationFlowRuntimeInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterDefaultFactory()
        {
            EmbodimentContext.RegisterDefaultConversationFlowSourceFactory(EnsureSource);
        }

        private static IConversationFlowSource EnsureSource(EmbodimentContext context)
        {
            if (context == null) return null;
            if (context.ConversationFlowSource != null)
                return context.ConversationFlowSource;

            ConvaiConversationFlowController existing =
                context.GetComponentInChildren<ConvaiConversationFlowController>(true);
            if (existing != null)
                return existing.isActiveAndEnabled ? existing : null;

            if (!context.IsConversationFlowDriverDemanded)
                return null;

            ConvaiConversationFlowController created =
                context.gameObject.AddComponent<ConvaiConversationFlowController>();
            created.hideFlags = HideFlags.None;
            return context.ConversationFlowSource ?? created;
        }
    }
}
