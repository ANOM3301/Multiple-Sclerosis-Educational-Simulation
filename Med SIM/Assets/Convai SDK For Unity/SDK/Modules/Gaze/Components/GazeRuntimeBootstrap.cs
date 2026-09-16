using Convai.Runtime.Embodiment;
using UnityEngine;

namespace Convai.Modules.Gaze.Components
{
    internal static class GazeRuntimeBootstrap
    {
        public static void EnsureCoordinator(EmbodimentContext context, Component requester)
        {
            if (context == null || context.GazeIntentProvider != null) return;
            if (!UnityEngine.Application.isPlaying) return;

            GameObject owner = context.CharacterRoot != null
                ? context.CharacterRoot.gameObject
                : requester.gameObject;

            if (owner.GetComponentInChildren<ConvaiGazeCoordinator>(true) != null)
                return;

            ConvaiGazeCoordinator coordinator = owner.AddComponent<ConvaiGazeCoordinator>();
            coordinator.hideFlags = HideFlags.None;
        }
    }
}
