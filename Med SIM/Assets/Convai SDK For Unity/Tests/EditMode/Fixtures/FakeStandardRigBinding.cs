using System.Collections.Generic;
using Convai.Domain.Embodiment.Interfaces;
using Convai.Domain.Embodiment.Semantics;
using UnityEngine;

namespace Convai.Tests.EditMode.Fixtures
{
    /// <summary>
    ///     Minimal <see cref="IStandardRigBinding" /> for unit tests.
    ///     Provides a fake skeleton of <see cref="Transform" /> nodes without a real rig.
    /// </summary>
    public sealed class FakeStandardRigBinding : IStandardRigBinding
    {
        private readonly Dictionary<StandardBone, Transform> _bones = new();
        private readonly Dictionary<StandardBlendshape, (SkinnedMeshRenderer mesh, int index)> _blendshapes = new();

        public Transform Root { get; }
        public IReadOnlyList<SkinnedMeshRenderer> FacialMeshes { get; } = new List<SkinnedMeshRenderer>();
        public RigConvention DetectedConvention => RigConvention.Unknown;

        public FakeStandardRigBinding(Transform root) => Root = root;

        public void RegisterBone(StandardBone bone, Transform transform) => _bones[bone] = transform;

        public bool TryGetBone(StandardBone semantic, out Transform bone)
            => _bones.TryGetValue(semantic, out bone);

        public bool TryGetBlendshape(StandardBlendshape semantic, out SkinnedMeshRenderer mesh, out int blendshapeIndex)
        {
            if (_blendshapes.TryGetValue(semantic, out var entry))
            {
                mesh = entry.mesh;
                blendshapeIndex = entry.index;
                return true;
            }

            mesh = null;
            blendshapeIndex = -1;
            return false;
        }

        public void RegisterBlendshape(StandardBlendshape shape, SkinnedMeshRenderer mesh, int index)
            => _blendshapes[shape] = (mesh, index);
    }
}
