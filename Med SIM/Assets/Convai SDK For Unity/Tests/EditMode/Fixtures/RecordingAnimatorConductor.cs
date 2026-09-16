using System.Collections.Generic;
using UnityEngine;

namespace Convai.Tests.EditMode.Fixtures
{
    /// <summary>
    ///     Captures calls made to animator-writing methods for assertion in unit tests.
    ///     Does NOT drive a real <see cref="Animator" />; attach to a plain
    ///     <see cref="GameObject" /> and use <see cref="Records" /> to assert behavior.
    /// </summary>
    public sealed class RecordingAnimatorConductor : MonoBehaviour
    {
        public readonly List<AnimatorRecord> Records = new();

        public void RecordLayerWeight(int layerIndex, float weight)
            => Records.Add(new AnimatorRecord(AnimatorRecordType.LayerWeight, layerIndex, weight));

        public void RecordParameter(string parameterName, float value)
            => Records.Add(new AnimatorRecord(AnimatorRecordType.FloatParameter, parameterName: parameterName, floatValue: value));

        public void RecordParameter(string parameterName, bool value)
            => Records.Add(new AnimatorRecord(AnimatorRecordType.BoolParameter, parameterName: parameterName, boolValue: value));

        public void RecordTrigger(string parameterName)
            => Records.Add(new AnimatorRecord(AnimatorRecordType.Trigger, parameterName: parameterName));

        public void RecordCrossFade(string stateName, float duration, int layerIndex)
            => Records.Add(new AnimatorRecord(AnimatorRecordType.CrossFade, layerIndex, floatValue: duration, stateName: stateName));

        public void Clear() => Records.Clear();

        public IEnumerable<AnimatorRecord> OfType(AnimatorRecordType type)
        {
            foreach (AnimatorRecord r in Records)
                if (r.RecordType == type)
                    yield return r;
        }
    }

    public enum AnimatorRecordType
    {
        LayerWeight,
        FloatParameter,
        BoolParameter,
        Trigger,
        CrossFade
    }

    public readonly struct AnimatorRecord
    {
        public AnimatorRecordType RecordType { get; }
        public int LayerIndex { get; }
        public float FloatValue { get; }
        public bool BoolValue { get; }
        public string ParameterName { get; }
        public string StateName { get; }

        public AnimatorRecord(
            AnimatorRecordType recordType,
            int layerIndex = -1,
            float floatValue = 0f,
            bool boolValue = false,
            string parameterName = null,
            string stateName = null)
        {
            RecordType = recordType;
            LayerIndex = layerIndex;
            FloatValue = floatValue;
            BoolValue = boolValue;
            ParameterName = parameterName;
            StateName = stateName;
        }
    }
}
