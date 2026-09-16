#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Convai.Modules.FacialAnimation.Profiles;
using UnityEditor;
using UnityEngine;

namespace Convai.Editor.Embodiment.FacialAnimation
{
    internal static class FacialAnimationClipBakeUtility
    {
        private const string BlendshapePrefix = "blendShape.";

        public static List<DiscoveredFacialCurveBinding> Analyze(
            AnimationClip clip,
            string mouthPatterns)
        {
            var discovered = new List<DiscoveredFacialCurveBinding>();
            if (clip == null)
                return discovered;

            string[] mouthParts = ParsePatterns(mouthPatterns);
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            for (int i = 0; i < bindings.Length; i++)
            {
                EditorCurveBinding binding = bindings[i];
                if (binding.type != typeof(SkinnedMeshRenderer))
                    continue;

                if (string.IsNullOrEmpty(binding.propertyName) ||
                    !binding.propertyName.StartsWith(BlendshapePrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                AnimationCurve sourceCurve = AnimationUtility.GetEditorCurve(clip, binding);
                if (sourceCurve == null || sourceCurve.length == 0)
                    continue;

                string blendshapeName = binding.propertyName.Substring(BlendshapePrefix.Length);
                discovered.Add(new DiscoveredFacialCurveBinding(
                    binding.path,
                    blendshapeName,
                    MatchesAnyPattern(blendshapeName, mouthParts),
                    true,
                    sourceCurve));
            }

            discovered.Sort(CompareDiscoveredBindings);
            return discovered;
        }

        public static List<ConvaiFacialAnimationProfile.CurveBinding> CreateProfileBindings(
            IReadOnlyList<DiscoveredFacialCurveBinding> discovered,
            float durationSeconds)
        {
            var profileBindings = new List<ConvaiFacialAnimationProfile.CurveBinding>();
            if (discovered == null)
                return profileBindings;

            _ = durationSeconds;
            for (int i = 0; i < discovered.Count; i++)
            {
                DiscoveredFacialCurveBinding binding = discovered[i];
                if (!binding.Include || string.IsNullOrWhiteSpace(binding.BlendshapeName) || binding.Curve == null)
                    continue;

                profileBindings.Add(new ConvaiFacialAnimationProfile.CurveBinding(
                    binding.BlendshapeName,
                    binding.IsMouth,
                    CloneSourceBlendshapeCurve(binding.Curve)));
            }

            return profileBindings;
        }

        public static AnimationCurve CloneSourceBlendshapeCurve(AnimationCurve source)
        {
            if (source == null || source.length == 0)
                return AnimationCurve.Linear(0f, 0f, 0f, 0f);

            var clone = new AnimationCurve(source.keys)
            {
                preWrapMode = source.preWrapMode,
                postWrapMode = source.postWrapMode
            };

            return clone;
        }

        public static string[] ParsePatterns(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return Array.Empty<string>();

            string[] parts = raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            var result = new List<string>(parts.Length);
            for (int i = 0; i < parts.Length; i++)
            {
                string pattern = parts[i].Trim();
                if (!string.IsNullOrEmpty(pattern))
                    result.Add(pattern);
            }

            return result.ToArray();
        }

        private static bool MatchesAnyPattern(string name, string[] patterns)
        {
            if (string.IsNullOrEmpty(name) || patterns == null)
                return false;

            for (int i = 0; i < patterns.Length; i++)
            {
                if (name.IndexOf(patterns[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static int CompareDiscoveredBindings(
            DiscoveredFacialCurveBinding left,
            DiscoveredFacialCurveBinding right)
        {
            int path = string.Compare(left.RelativePath, right.RelativePath, StringComparison.Ordinal);
            return path != 0
                ? path
                : string.Compare(left.BlendshapeName, right.BlendshapeName, StringComparison.Ordinal);
        }
    }

    internal struct DiscoveredFacialCurveBinding
    {
        public DiscoveredFacialCurveBinding(
            string relativePath,
            string blendshapeName,
            bool isMouth,
            bool include,
            AnimationCurve curve)
        {
            RelativePath = relativePath ?? string.Empty;
            BlendshapeName = blendshapeName ?? string.Empty;
            IsMouth = isMouth;
            Include = include;
            Curve = curve;
        }

        public string RelativePath;
        public string BlendshapeName;
        public bool IsMouth;
        public bool Include;
        public AnimationCurve Curve;
    }
}
#endif
