using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Convai.Modules.Attention.Components;
using Convai.Modules.Attention.Providers;
using Convai.Modules.Embodiment.Components;
using Convai.Modules.ConversationFlow.Components;
using Convai.Modules.DialogueAnimation.Components;
using Convai.Modules.DialogueAnimation.Runtime;
using Convai.Modules.Emotion.Components;
using Convai.Modules.FacialAnimation.Components;
using Convai.Modules.Gaze.Components;
using Convai.Runtime.Animation;
using Convai.Runtime.Embodiment;
using Convai.Runtime.Components;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;

namespace Convai.Tests.EditMode.Embodiment
{
    public sealed class EmbodimentPublicSurfaceGuardTests
    {
        private static string PackageRoot => Path.GetFullPath(Path.Combine(
            UnityEngine.Application.dataPath,
            "..",
            "Packages",
            "com.convai.convai-sdk-for-unity"));

        [Test]
        [Category("Architecture")]
        public void EmbodimentFeatureSurface_HasNoPublicReleaseCleanupTerms()
        {
            string[] roots =
            {
                "SDK/Domain/Embodiment",
                "SDK/Modules/Embodiment",
                "SDK/Modules/Attention",
                "SDK/Modules/Emotion",
                "SDK/Modules/Gaze",
                "SDK/Modules/DialogueAnimation",
                "SDK/Runtime/Embodiment",
                "SDK/Runtime/Animation",
                "SDK/Editor/Embodiment"
            };

            string[] forbidden =
            {
                "[Obsolete",
                "FormerlySerializedAs",
                "deprecated",
                "obsolete",
                "legacy",
                "Animation Rigging Gaze Bridge",
                "FacialBlendshapeLayerKind"
            };

            var violations = new List<string>();
            foreach (string root in roots)
            {
                string absoluteRoot = Path.Combine(PackageRoot, root.Replace('/', Path.DirectorySeparatorChar));
                Assert.IsTrue(Directory.Exists(absoluteRoot), $"Embodiment feature root not found: {root}");

                foreach (string file in Directory.EnumerateFiles(absoluteRoot, "*.cs", SearchOption.AllDirectories))
                {
                    string source = File.ReadAllText(file);
                    foreach (string token in forbidden)
                    {
                        if (source.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                            violations.Add($"{ToPackagePath(file)} contains '{token}'");
                    }
                }
            }

            Assert.IsEmpty(violations, string.Join(Environment.NewLine, violations.Take(30)));
        }

        [Test]
        public void EmotionController_RequiresCharacterScopedEvents()
        {
            GameObject root = new("EmotionFilterTestCharacter");
            ConvaiCharacter character = root.AddComponent<ConvaiCharacter>();
            ConvaiEmotionController controller = root.AddComponent<ConvaiEmotionController>();

            try
            {
                SetPrivateField(character, "_characterId", "char-a");
                SetPrivateField(controller, "_character", character);

                Assert.IsTrue((bool)InvokePrivateWithResult(controller, "MatchesCharacter", "char-a"));
                Assert.IsFalse((bool)InvokePrivateWithResult(controller, "MatchesCharacter", "char-b"));
                Assert.IsFalse((bool)InvokePrivateWithResult(controller, "MatchesCharacter", ""));
                Assert.IsFalse((bool)InvokePrivateWithResult(controller, "MatchesCharacter", (object)null));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LipSyncSampleCC4ExtendedPresetAsset_UsesProfileSlotsOnly()
        {
            string path = Path.GetFullPath(Path.Combine(
                PackageRoot,
                "Samples",
                "LipSyncSample",
                "Embodiment",
                "Presets",
                "LipSyncSample_CC4Extended_CharacterEmbodimentPreset.asset"));
            if (!File.Exists(path))
                Assert.Fail(
                    $"Required Lip Sync sample CC4 Extended embodiment preset asset is missing: {path}");

            string yaml = File.ReadAllText(path);

            StringAssert.Contains("profileSlots:", yaml);
            StringAssert.Contains("convai.dialogue-animation", yaml);
            Assert.IsFalse(yaml.Contains("conversationFlow:"));
            Assert.IsFalse(yaml.Contains("facialComposition:"));
            Assert.IsFalse(yaml.Contains("dialogueAnimationRuntimeConfig:"));
        }

        [Test]
        public void EmbodimentPresetAssembly_DoesNotReferenceConcreteEmbodimentModules()
        {
            string asmdefPath = Path.Combine(
                PackageRoot,
                "SDK",
                "Modules",
                "Embodiment",
                "Convai.Modules.Embodiment.asmdef");
            string json = File.ReadAllText(asmdefPath);

            Assert.IsFalse(json.Contains("Convai.Modules.ConversationFlow"));
            Assert.IsFalse(json.Contains("Convai.Modules.Attention"));
            Assert.IsFalse(json.Contains("Convai.Modules.Emotion"));
            Assert.IsFalse(json.Contains("Convai.Modules.Gaze"));
            Assert.IsFalse(json.Contains("Convai.Modules.FacialAnimation"));
        }

        [Test]
        public void DialogueAnimationAssembly_DoesNotReferenceConcreteRuntimeModules()
        {
            string asmdefPath = Path.Combine(
                PackageRoot,
                "SDK",
                "Modules",
                "DialogueAnimation",
                "Convai.Modules.DialogueAnimation.asmdef");
            string json = File.ReadAllText(asmdefPath);

            Assert.IsFalse(json.Contains("Convai.Modules.ConversationFlow"));
            Assert.IsFalse(json.Contains("Convai.Modules.LipSync"));
        }

        [Test]
        public void AddComponentMenus_ExposeOnlyPublicEmbodimentModules()
        {
            AssertAddComponentMenu<ConvaiEmotionController>("Convai/Embodiment/Emotion Controller");
            AssertAddComponentMenu<ConvaiAttentionController>("Convai/Embodiment/Attention Controller");
            AssertAddComponentMenu<ConvaiEyeGazeActuator>("Convai/Embodiment/Eye Gaze");
            AssertAddComponentMenu<ConvaiHeadLookActuator>("Convai/Embodiment/Head Look");
            AssertAddComponentMenu<ConvaiDialogueAnimationController>("Convai/Embodiment/Dialogue Animation");
            AssertAddComponentMenu<ConvaiCharacterEmbodimentBinding>("Convai/Embodiment/Character Embodiment Binding");
            AssertAddComponentMenu<ConvaiFacialClipPlayer>("Convai/Embodiment/Facial Clip Player");
            AssertAddComponentMenu<ConvaiFacialClipRuntimePlayer>("Convai/Embodiment/Facial Clip Runtime Player");
            AssertAddComponentMenu<ConvaiConversationFlowController>("Convai/Embodiment/Conversation Flow Controller");
            AssertAddComponentMenu<ConvaiGazeCoordinator>("Convai/Embodiment/Gaze Coordinator");
            AssertAddComponentMenu<DefaultFocusTargetProvider>("Convai/Embodiment/Focus Target Provider (Default)");

            AssertAddComponentMenu<EmbodimentContext>(string.Empty);
            AssertAddComponentMenu<StandardRigBinding>(string.Empty);
        }

        [Test]
        public void CoreAndLipSync_DoNotReferenceEmbodimentCompositionRoot()
        {
            AssertFileDoesNotContain(
                "SDK/Runtime/Components/ConvaiCharacter.cs",
                "EmbodimentContext",
                "ConvaiCharacter must not know the optional embodiment composition root.");
            AssertFileDoesNotContain(
                "SDK/Modules/LipSync/Components/ConvaiLipSyncComponent.cs",
                "EmbodimentContext",
                "ConvaiLipSyncComponent must not register embodiment providers directly.");
            AssertFileDoesNotContain(
                "SDK/Modules/LipSync/Components/ConvaiLipSyncComponent.cs",
                "Convai.Runtime.Embodiment",
                "ConvaiLipSyncComponent must stay embodiment-assembly agnostic.");
            AssertFileDoesNotContain(
                "SDK/Modules/LipSync/Components/LipSyncRuntimeController.cs",
                "EmbodimentContext",
                "LipSync runtime playback must not resolve embodiment context.");
            AssertFileDoesNotContain(
                "SDK/Modules/LipSync/Components/LipSyncRuntimeController.cs",
                "IEmotionMouthWeightProvider",
                "Emotion fade blending belongs to optional embodiment integration, not lipsync playback.");
            AssertFileDoesNotContain(
                "SDK/Modules/LipSync/Sinks/SkinnedMeshBlendshapeSink.cs",
                "GetOrCreate",
                "SkinnedMeshBlendshapeSink must not create FacialBlendshapeCompositorHost; use LipSyncBlendshapeOutputSinkFactory.");
        }

        [Test]
        public void ConversationOnlyContext_DoesNotEagerCreateRigCompositorOrConductor()
        {
            GameObject root = new("ConversationOnlyLazyContextTest");

            try
            {
                ConvaiConversationFlowController flow = root.AddComponent<ConvaiConversationFlowController>();
                Assert.IsTrue(EmbodimentContext.TryResolve(flow, out EmbodimentContext context));
                Assert.NotNull(context);

                Assert.IsNull(root.GetComponentInChildren<StandardRigBinding>(true));
                Assert.IsNull(root.GetComponentInChildren<FacialBlendshapeCompositorHost>(true));
                Assert.IsNull(root.GetComponentInChildren<AnimatorConductor>(true));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ConvaiGazeCoordinator_OnEnable_DoesNotDemandConversationFlowDriver()
        {
            GameObject root = new("GazeCoordinatorFlowDemandTest");

            try
            {
                root.AddComponent<ConvaiCharacter>();
                ConvaiGazeCoordinator coordinator = root.AddComponent<ConvaiGazeCoordinator>();

                Assert.IsTrue(coordinator.enabled, "Coordinator should stay enabled with a valid context.");
                Assert.IsTrue(EmbodimentContext.TryResolve(coordinator, out EmbodimentContext context));
                Assert.IsFalse(
                    context.IsConversationFlowDriverDemanded,
                    "Gaze-only stack must not auto-provision ConvaiConversationFlowController.");
                Assert.IsNull(
                    root.GetComponentInChildren<ConvaiConversationFlowController>(true),
                    "No hidden conversation flow driver before any module requests it.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DialogueAnimatorContractValidator_ReportsLayerContractDiagnostics()
        {
            GameObject root = new("DialogueAnimatorContractValidatorTest");
            Animator animator = root.AddComponent<Animator>();
            AnimatorController shortController = CreateController(3);
            AnimatorController validController = CreateController(4);

            try
            {
                animator.runtimeAnimatorController = shortController;
                Assert.IsFalse(DialogueAnimatorContractValidator.TryValidateAnimator(
                    animator,
                    new DialogueAnimatorLayerSet(0, 1, 2, 3),
                    out string shortMessage));
                StringAssert.Contains("at least 4", shortMessage);

                animator.runtimeAnimatorController = validController;
                Assert.IsFalse(DialogueAnimatorContractValidator.TryValidateAnimator(
                    animator,
                    new DialogueAnimatorLayerSet(0, 1, 1, 3),
                    out string duplicateMessage));
                StringAssert.Contains("unique", duplicateMessage);

                Assert.IsFalse(DialogueAnimatorContractValidator.TryValidateAnimator(
                    animator,
                    new DialogueAnimatorLayerSet(0, 1, 2, 4),
                    out string rangeMessage));
                StringAssert.Contains("invalid layer", rangeMessage);

                Assert.IsTrue(DialogueAnimatorContractValidator.TryValidateAnimator(
                    animator,
                    new DialogueAnimatorLayerSet(0, 1, 2, 3),
                    out string okMessage));
                Assert.IsNull(okMessage);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(validController);
                UnityEngine.Object.DestroyImmediate(shortController);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static string ToPackagePath(string path) =>
            Path.GetRelativePath(PackageRoot, path).Replace('\\', '/');

        private static object InvokePrivateWithResult(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method, $"Missing method {methodName}.");
            return method.Invoke(target, args);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, $"Missing field {fieldName}.");
            field.SetValue(target, value);
        }

        private static void AssertAddComponentMenu<T>(string expected)
        {
            AddComponentMenu attribute = typeof(T).GetCustomAttribute<AddComponentMenu>();
            Assert.NotNull(attribute, $"{typeof(T).Name} must declare AddComponentMenu.");
            Assert.AreEqual(expected, attribute.componentMenu);
        }

        private static void AssertFileDoesNotContain(string packageRelativePath, string token, string message)
        {
            string path = Path.Combine(PackageRoot, packageRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(File.Exists(path), $"Expected source file to exist: {packageRelativePath}");
            string source = File.ReadAllText(path);
            StringAssert.DoesNotContain(token, source, message);
        }

        private static AnimatorController CreateController(int layerCount)
        {
            AnimatorController controller = new();
            for (int i = 0; i < layerCount; i++)
                controller.AddLayer($"Layer {i}");
            return controller;
        }
    }
}
