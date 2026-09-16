using Convai.Modules.Gaze.Profiles;
using UnityEngine;

namespace Convai.Modules.Gaze.Core
{
    /// <summary>
    ///     Stateless sine oscillator that produces the resting micro-tremor offset added to
    ///     each axis of the eye yaw/pitch delta. Two axis samples are decorrelated by a fixed
    ///     phase shift so left/right and yaw/pitch tremors do not visually align.
    /// </summary>
    internal static class EyeTremorOscillator
    {
        private const float AxisPhaseShift = 1.314f;

        /// <summary>
        ///     Returns the tremor offset (degrees) for <paramref name="axis" /> at
        ///     <paramref name="time" />. Returns zero when tremor is disabled or amplitude
        ///     is non-positive.
        /// </summary>
        public static float Sample(ConvaiGazeEyeProfile profile, int axis, float time)
        {
            if (profile == null || !profile.EnableMicroTremor || profile.MicroTremorAmplitude <= 0f)
                return 0f;

            float t = time * profile.MicroTremorFrequency + axis * AxisPhaseShift;
            return Mathf.Sin(t) * profile.MicroTremorAmplitude;
        }
    }
}
