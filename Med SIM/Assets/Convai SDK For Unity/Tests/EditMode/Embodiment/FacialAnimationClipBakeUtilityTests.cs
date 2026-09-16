#if UNITY_EDITOR
using System.Linq;
using Convai.Editor.Embodiment.FacialAnimation;
using Convai.Modules.FacialAnimation.Profiles;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Convai.Tests.EditMode.Embodiment
{
    public sealed class FacialAnimationClipBakeUtilityTests
    {
        [Test]
        public void Analyze_ReturnsOnlySkinnedMeshBlendshapeCurves_WithMouthClassification()
        {
            AnimationClip clip = CreateClip();
            AnimationUtility.SetEditorCurve(
                clip,
                EditorCurveBinding.FloatCurve("Face", typeof(SkinnedMeshRenderer), "blendShape.Jaw_Open"),
                AnimationCurve.Linear(0f, 0f, 2f, 100f));
            AnimationUtility.SetEditorCurve(
                clip,
                EditorCurveBinding.FloatCurve("Face", typeof(SkinnedMeshRenderer), "m_LocalPosition.x"),
                AnimationCurve.Linear(0f, 0f, 2f, 1f));
            AnimationUtility.SetEditorCurve(
                clip,
                EditorCurveBinding.FloatCurve("Face", typeof(Transform), "blendShape.NotARealBlendshapeBinding"),
                AnimationCurve.Linear(0f, 0f, 2f, 1f));

            var discovered = FacialAnimationClipBakeUtility.Analyze(clip, "Jaw;Mouth");

            Assert.That(discovered, Has.Count.EqualTo(1));
            Assert.That(discovered[0].RelativePath, Is.EqualTo("Face"));
            Assert.That(discovered[0].BlendshapeName, Is.EqualTo("Jaw_Open"));
            Assert.That(discovered[0].IsMouth, Is.True);
            Assert.That(discovered[0].Include, Is.True);
        }

        [Test]
        public void CreateProfileBindings_PreservesSourceSecondsAndWeights_ForConvaiFacialAnimationProfile()
        {
            var sourceCurve = new AnimationCurve(
                new Keyframe(0f, 0f, 12.5f, 12.5f, 0.2f, 0.3f)
                {
                    weightedMode = WeightedMode.Both
                },
                new Keyframe(2f, 100f, -20f, -20f, 0.4f, 0.5f)
                {
                    weightedMode = WeightedMode.Both
                })
            {
                preWrapMode = WrapMode.ClampForever,
                postWrapMode = WrapMode.Loop
            };

            var discovered = new[]
            {
                new DiscoveredFacialCurveBinding(
                    "Face",
                    "Mouth_Smile",
                    true,
                    true,
                    sourceCurve)
            };

            var bindings = FacialAnimationClipBakeUtility.CreateProfileBindings(discovered, 2f);

            Assert.That(bindings, Has.Count.EqualTo(1));
            ConvaiFacialAnimationProfile.CurveBinding binding = bindings[0];
            Assert.That(binding.BlendshapeName, Is.EqualTo("Mouth_Smile"));
            Assert.That(binding.IsMouth, Is.True);
            Assert.That(binding.Curve.preWrapMode, Is.EqualTo(sourceCurve.preWrapMode));
            Assert.That(binding.Curve.postWrapMode, Is.EqualTo(sourceCurve.postWrapMode));
            Assert.That(binding.Curve.Evaluate(0f), Is.EqualTo(0f).Within(0.0001f));
            // PostWrapMode.Loop: Evaluate at t == last key time wraps to the cycle start; sample just before end.
            Assert.That(binding.Curve.Evaluate(2f - 1e-4f), Is.EqualTo(100f).Within(0.05f));
            Assert.That(binding.Curve.keys[1].time, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(binding.Curve.keys[1].value, Is.EqualTo(100f).Within(0.0001f));
            Assert.That(binding.Curve.keys[1].inTangent, Is.EqualTo(-20f).Within(0.0001f));
            Assert.That(binding.Curve.keys[1].outTangent, Is.EqualTo(-20f).Within(0.0001f));
            Assert.That(binding.Curve.keys[1].inWeight, Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(binding.Curve.keys[1].outWeight, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void SetBindings_ClonesCurveData_ForImportedBindings()
        {
            var profile = ScriptableObject.CreateInstance<ConvaiFacialAnimationProfile>();
            var sourceCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            var bindings = new[]
            {
                new ConvaiFacialAnimationProfile.CurveBinding("Brow_Raise", false, sourceCurve)
            };

            profile.SetBindings(bindings, 1f, true);
            sourceCurve.MoveKey(1, new Keyframe(1f, 0f));

            Assert.That(profile.Bindings.Single().Curve.Evaluate(1f), Is.EqualTo(1f).Within(0.0001f));
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void ResolveWeightSettings_BypassesGlobalWeight_ForDefaultBlinkRules()
        {
            var profile = ScriptableObject.CreateInstance<ConvaiFacialAnimationProfile>();

            ConvaiFacialAnimationProfile.BindingWeightSettings blink =
                profile.ResolveWeightSettings("CC_Base_Body.Eye_Blink_L");
            ConvaiFacialAnimationProfile.BindingWeightSettings smile =
                profile.ResolveWeightSettings("Mouth_Smile_L");

            Assert.That(blink.Mode, Is.EqualTo(ConvaiFacialAnimationProfile.GlobalWeightMode.BypassGlobal));
            Assert.That(blink.ResolveScale(profileScale: 1f, sourceGlobalScale: 0.25f), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(smile.Mode, Is.EqualTo(ConvaiFacialAnimationProfile.GlobalWeightMode.MultiplyGlobal));
            Assert.That(smile.ResolveScale(profileScale: 1f, sourceGlobalScale: 0.25f), Is.EqualTo(0.25f).Within(0.0001f));

            Object.DestroyImmediate(profile);
        }

        private static AnimationClip CreateClip()
        {
            var clip = new AnimationClip();
            clip.EnsureQuaternionContinuity();
            return clip;
        }
    }
}
#endif
