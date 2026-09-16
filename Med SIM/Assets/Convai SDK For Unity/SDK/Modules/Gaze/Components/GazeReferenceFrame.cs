using Convai.Runtime.Embodiment;
using UnityEngine;

namespace Convai.Modules.Gaze.Components
{
    internal static class GazeReferenceFrame
    {
        public static Transform Resolve(EmbodimentContext context, Transform fallback)
        {
            Transform reference = context?.RigBinding?.Root;
            return reference != null ? reference : fallback;
        }

        public static Vector3 ResolveForward(EmbodimentContext context, Transform fallback)
        {
            Transform reference = Resolve(context, fallback);
            return reference != null ? reference.forward : Vector3.forward;
        }
    }
}
