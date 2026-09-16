using UnityEngine;

namespace Convai.Domain.Embodiment.Readings
{
    /// <summary>
    ///     Combined attention + cognition signal a gaze coordinator broadcasts to eye, head,
    ///     and spine actuators.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The intent decouples <em>what</em> the character looks at from <em>how</em>
    ///         the body articulates that look: <see cref="OverallWeight" /> controls how much
    ///         the gaze system steals authority from idle behavior, while
    ///         <see cref="EyeShare" /> splits that authority between eye-only micro-movement
    ///         and full head-turn engagement.
    ///     </para>
    /// </remarks>
    public readonly struct GazeIntent
    {
        /// <summary>World-space point the character should direct gaze toward.</summary>
        public Vector3 WorldTargetPoint { get; }

        /// <summary>
        ///     Combined authority weight in <c>[0, 1]</c>. <c>0</c> lets idle gaze run freely;
        ///     <c>1</c> clamps eyes and head fully onto the target.
        /// </summary>
        public float OverallWeight { get; }

        /// <summary>
        ///     Fraction of <see cref="OverallWeight" /> handled by eyes alone in <c>[0, 1]</c>.
        ///     <c>1</c> means eyes track while the head stays relaxed (quick glance);
        ///     <c>0</c> forces the full head-turn commit used for close engagement.
        /// </summary>
        public float EyeShare { get; }

        public GazeIntent(Vector3 worldTargetPoint, float overallWeight, float eyeShare)
        {
            WorldTargetPoint = worldTargetPoint;
            OverallWeight = overallWeight < 0f ? 0f : overallWeight > 1f ? 1f : overallWeight;
            EyeShare = eyeShare < 0f ? 0f : eyeShare > 1f ? 1f : eyeShare;
        }

        /// <summary>Disengaged intent — actuators should release back to idle behavior.</summary>
        public static GazeIntent Relaxed => new(Vector3.zero, 0f, 1f);
    }
}
