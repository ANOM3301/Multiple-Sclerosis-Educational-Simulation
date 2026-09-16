using System.Collections.Generic;
using System.Reflection;
using Convai.Domain.Embodiment.Semantics;
using Convai.Runtime.Animation;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Convai.Tests.EditMode.Runtime
{
    public sealed class StandardRigBindingTests
    {
        private GameObject _host;
        private GameObject _rigRoot;
        private Object _ownedAsset;
        private Object _ownedMesh;
        private Object _ownedMesh2;

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            if (_rigRoot != null) Object.DestroyImmediate(_rigRoot);
            if (_ownedAsset != null) Object.DestroyImmediate(_ownedAsset);
            if (_ownedMesh != null) Object.DestroyImmediate(_ownedMesh);
            if (_ownedMesh2 != null) Object.DestroyImmediate(_ownedMesh2);
        }

        [Test]
        public void Root_ReturnsAnimatorTransform_WhenRigLivesUnderWrapper()
        {
            _host = new GameObject("CharacterWrapper");
            _rigRoot = new GameObject("RigRoot");
            _rigRoot.transform.SetParent(_host.transform, false);
            _rigRoot.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            _rigRoot.AddComponent<Animator>();

            StandardRigBinding binding = _host.AddComponent<StandardRigBinding>();
            binding.Rebuild();

            Assert.AreSame(_rigRoot.transform, binding.Root,
                "Animation systems should resolve the actual rig root, not the wrapper transform.");
        }

        [Test]
        public void Root_FallsBackToHostTransform_WhenAnimatorIsMissing()
        {
            _host = new GameObject("CharacterWrapper");

            StandardRigBinding binding = _host.AddComponent<StandardRigBinding>();
            binding.Rebuild();

            Assert.AreSame(_host.transform, binding.Root);
        }

        [Test]
        public void CustomConventionMap_ResolvesExplicitBlendshapeName()
        {
            _host = new GameObject("CustomRig");
            SkinnedMeshRenderer renderer = _host.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = CreateMeshWithBlendshape("CustomBlinkLeft");
            _ownedMesh = renderer.sharedMesh;

            var map = ScriptableObject.CreateInstance<CustomRigConventionMap>();
            _ownedAsset = map;
            SetPrivateField(map, "blendshapes", new List<CustomRigConventionMap.BlendshapeMapping>
            {
                new(StandardBlendshape.EyeBlinkLeft, "CustomBlinkLeft")
            });

            StandardRigBinding binding = _host.AddComponent<StandardRigBinding>();
            SetPrivateField(binding, "facialMeshes", new List<SkinnedMeshRenderer> { renderer });
            SetPrivateField(binding, "conventionOverride", RigConvention.Custom);
            SetPrivateField(binding, "customConventionMap", map);

            binding.Rebuild();

            Assert.IsTrue(binding.TryGetBlendshape(
                StandardBlendshape.EyeBlinkLeft,
                out SkinnedMeshRenderer resolvedMesh,
                out int resolvedIndex));
            Assert.AreSame(renderer, resolvedMesh);
            Assert.AreEqual(0, resolvedIndex);
        }

        [Test]
        public void Rebuild_AutoDetectsMeshes_WhenSerializedListContainsOnlyNulls()
        {
            _host = new GameObject("RigWithStaleMeshList");
            SkinnedMeshRenderer renderer = _host.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = CreateMeshWithBlendshape("JawOpen");
            _ownedMesh = renderer.sharedMesh;

            StandardRigBinding binding = _host.AddComponent<StandardRigBinding>();
            SetPrivateField(binding, "facialMeshes", new List<SkinnedMeshRenderer> { null, null });

            binding.Rebuild();

            Assert.AreEqual(1, binding.FacialMeshes.Count);
            Assert.AreSame(renderer, binding.FacialMeshes[0]);
        }

        [Test]
        public void Rebuild_PrioritizesFaceMesh_WhenAccessorySharesBlendshapeName()
        {
            _host = new GameObject("RigWithAccessoryBlendshapes");

            GameObject accessoryObject = new("AccessoryMesh");
            accessoryObject.transform.SetParent(_host.transform, false);
            SkinnedMeshRenderer accessory = accessoryObject.AddComponent<SkinnedMeshRenderer>();
            accessory.sharedMesh = CreateMeshWithBlendshapes("eyeBlinkLeft");
            _ownedMesh = accessory.sharedMesh;

            GameObject faceObject = new("CC_Base_Head");
            faceObject.transform.SetParent(_host.transform, false);
            SkinnedMeshRenderer face = faceObject.AddComponent<SkinnedMeshRenderer>();
            face.sharedMesh = CreateMeshWithBlendshapes("eyeBlinkLeft", "eyeBlinkRight", "jawOpen");
            _ownedMesh2 = face.sharedMesh;

            StandardRigBinding binding = _host.AddComponent<StandardRigBinding>();
            SetPrivateField(binding, "facialMeshes", new List<SkinnedMeshRenderer> { accessory, face });
            SetPrivateField(binding, "conventionOverride", RigConvention.ARKit);

            binding.Rebuild();

            Assert.AreSame(face, binding.FacialMeshes[0]);
            Assert.IsTrue(binding.TryGetBlendshape(
                StandardBlendshape.EyeBlinkLeft,
                out SkinnedMeshRenderer resolvedMesh,
                out _));
            Assert.AreSame(face, resolvedMesh);
        }

        [Test]
        public void ReallusionCC4Extended_ResolvesDedicatedEyelidFollowBlendshapes()
        {
            _host = new GameObject("CC4ExtendedRig");
            SkinnedMeshRenderer renderer = _host.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = CreateMeshWithBlendshapes(
                "Eye_Blink_L",
                "Eye_Blink_R",
                "Eyelash_Upper_Down_L",
                "Eyelash_Upper_Down_R",
                "Eyelash_Lower_Up_L",
                "Eyelash_Lower_Up_R");
            _ownedMesh = renderer.sharedMesh;

            StandardRigBinding binding = _host.AddComponent<StandardRigBinding>();
            SetPrivateField(binding, "facialMeshes", new List<SkinnedMeshRenderer> { renderer });
            SetPrivateField(binding, "conventionOverride", RigConvention.ReallusionCC4Extended);

            binding.Rebuild();

            Assert.IsTrue(binding.TryGetBlendshape(
                StandardBlendshape.EyeUpperLidDownLeft,
                out SkinnedMeshRenderer upperLidMesh,
                out int upperLidIndex));
            Assert.AreSame(renderer, upperLidMesh);
            Assert.AreEqual(2, upperLidIndex);

            Assert.IsTrue(binding.TryGetBlendshape(
                StandardBlendshape.EyeLowerLidUpRight,
                out SkinnedMeshRenderer lowerLidMesh,
                out int lowerLidIndex));
            Assert.AreSame(renderer, lowerLidMesh);
            Assert.AreEqual(5, lowerLidIndex);
        }

        private static Mesh CreateMeshWithBlendshape(string blendshapeName)
        {
            return CreateMeshWithBlendshapes(blendshapeName);
        }

        private static Mesh CreateMeshWithBlendshapes(params string[] blendshapeNames)
        {
            var mesh = new Mesh();
            mesh.vertices = new[] { Vector3.zero };
            for (int i = 0; i < blendshapeNames.Length; i++)
            {
                mesh.AddBlendShapeFrame(
                    blendshapeNames[i],
                    100f,
                    new[] { Vector3.zero },
                    new[] { Vector3.zero },
                    new[] { Vector3.zero });
            }
            return mesh;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, $"Missing field {fieldName}.");
            field.SetValue(target, value);
        }
    }
}
