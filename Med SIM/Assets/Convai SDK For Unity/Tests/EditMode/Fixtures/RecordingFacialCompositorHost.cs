using System.Collections.Generic;
using UnityEngine;

namespace Convai.Tests.EditMode.Fixtures
{
    /// <summary>
    ///     Captures blendshape layer submissions for assertion in unit tests.
    ///     Does NOT write to any <see cref="SkinnedMeshRenderer" />.
    /// </summary>
    public sealed class RecordingFacialCompositorHost : MonoBehaviour
    {
        public readonly List<BlendshapeLayerPost> Posts = new();

        public void RecordPost(int layerId, string debugKey, float weight)
            => Posts.Add(new BlendshapeLayerPost(layerId, debugKey, weight));

        public void Clear() => Posts.Clear();

        public IEnumerable<BlendshapeLayerPost> ForLayer(int layerId)
        {
            foreach (BlendshapeLayerPost p in Posts)
                if (p.LayerId == layerId)
                    yield return p;
        }
    }

    public readonly struct BlendshapeLayerPost
    {
        public int LayerId { get; }
        public string Key { get; }
        public float Weight { get; }

        public BlendshapeLayerPost(int layerId, string key, float weight)
        {
            LayerId = layerId;
            Key = key;
            Weight = weight;
        }
    }
}
