using System.Reflection;
using Convai.Modules.DialogueAnimation.Components;
using Convai.Modules.DialogueAnimation.Runtime;
using Convai.Modules.DialogueAnimation.Runtime.Driver;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Convai.Tests.EditMode.DialogueAnimation
{
    /// <summary>
    ///     Guards that dialogue animation never assigns the authored AnimatorOverrideController
    ///     instance as the runtime animator controller (shared-asset mutation risk).
    /// </summary>
    public sealed class DialogueAnimationOverrideControllerIsolationTests
    {
        [Test]
        public void RuntimeConfig_Default_IgnoresFacialDialoguePhaseForTalkLayerScale()
        {
            DialogueAnimationRuntimeConfig config = ScriptableObject.CreateInstance<DialogueAnimationRuntimeConfig>();
            try
            {
                Assert.IsTrue(
                    config.IgnoreFacialDialoguePhaseForTalkLayerScale,
                    "Talk-layer speech scaling must not depend on facial compositor by default.");
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void CreateRuntimeOverrideController_FromAnimatorOverrideController_ReturnsDistinctInstance()
        {
            AnimatorController baseController = new();
            baseController.AddLayer("Base");
            var sourceOverride = new AnimatorOverrideController(baseController);

            try
            {
                MethodInfo method = typeof(DialogueRuntimeBuilder).GetMethod(
                    "CloneOverrideController",
                    BindingFlags.NonPublic | BindingFlags.Static);
                Assert.NotNull(method);

                var clone = (AnimatorOverrideController)method.Invoke(null, new object[] { sourceOverride });

                Assert.NotNull(clone);
                Assert.AreNotSame(sourceOverride, clone);
                Assert.IsTrue(
                    ReferenceEquals(clone.runtimeAnimatorController, baseController),
                    "Clone should wrap the same underlying AnimatorController.");
                Assert.That(clone.name, Does.StartWith("Convai Dialogue Runtime Override"));
                Assert.That(clone.hideFlags & HideFlags.DontSave, Is.EqualTo(HideFlags.DontSave));
                Object.DestroyImmediate(clone);
            }
            finally
            {
                Object.DestroyImmediate(sourceOverride);
                Object.DestroyImmediate(baseController);
            }
        }

        [Test]
        public void CreateRuntimeOverrideController_FromAnimatorController_ReturnsNewOverrideWrapper()
        {
            AnimatorController baseController = new();
            baseController.AddLayer("Base");

            try
            {
                MethodInfo method = typeof(DialogueRuntimeBuilder).GetMethod(
                    "CloneOverrideController",
                    BindingFlags.NonPublic | BindingFlags.Static);
                Assert.NotNull(method);

                var wrapper = (AnimatorOverrideController)method.Invoke(null, new object[] { baseController });
                Assert.NotNull(wrapper);
                Assert.AreNotSame(baseController, wrapper);
                Assert.AreSame(baseController, wrapper.runtimeAnimatorController);
                Assert.That(wrapper.name, Does.StartWith("Convai Dialogue Runtime Override"));
                Assert.That(wrapper.hideFlags & HideFlags.DontSave, Is.EqualTo(HideFlags.DontSave));
                Object.DestroyImmediate(wrapper);
            }
            finally
            {
                Object.DestroyImmediate(baseController);
            }
        }

        [Test]
        public void OverrideControllerOwnership_RestoresAuthoredController()
        {
            var host = new GameObject("dialogue-animation-ownership-test");
            Animator animator = host.AddComponent<Animator>();
            AnimatorController original = new();
            original.AddLayer("Base");
            var runtimeOverride = new AnimatorOverrideController(original);
            DialogueOverrideControllerOwnership ownership = new();

            try
            {
                animator.runtimeAnimatorController = original;
                ownership.Install(animator, runtimeOverride);

                Assert.AreSame(runtimeOverride, animator.runtimeAnimatorController);

                ownership.RestoreAndDestroyIfOwned();

                Assert.AreSame(original, animator.runtimeAnimatorController);
            }
            finally
            {
                if (runtimeOverride != null)
                    Object.DestroyImmediate(runtimeOverride);
                Object.DestroyImmediate(original);
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void OnValidate_EditMode_UnwrapsTransientRuntimeOverride()
        {
            var host = new GameObject("dialogue-animation-editor-sanitize-test");
            Animator animator = host.AddComponent<Animator>();
            host.AddComponent<ConvaiDialogueAnimationController>();
            AnimatorController baseController = new();
            baseController.AddLayer("Base");
            var transientOverride = new AnimatorOverrideController(baseController)
            {
                name = "Convai Dialogue Runtime Override (Base)",
                hideFlags = HideFlags.HideAndDontSave
            };

            try
            {
                animator.runtimeAnimatorController = transientOverride;
                MethodInfo onValidate = typeof(ConvaiDialogueAnimationController).GetMethod(
                    "OnValidate",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.NotNull(onValidate);

                onValidate.Invoke(host.GetComponent<ConvaiDialogueAnimationController>(), null);

                Assert.AreSame(baseController, animator.runtimeAnimatorController);
            }
            finally
            {
                if (transientOverride != null)
                    Object.DestroyImmediate(transientOverride);
                Object.DestroyImmediate(baseController);
                Object.DestroyImmediate(host);
            }
        }
    }
}
