using System;
using System.Collections.Generic;
using UnityEngine;

namespace Convai.Runtime.Animation
{
    /// <summary>
    ///     Configures per-region facial blendshape composition for the <see cref="FacialBlendshapeCompositorHost" />.
    ///     Each blendshape is classified into a <see cref="FacialBlendshapeRegion" /> via semicolon-separated
    ///     name patterns, and each region defines how Emotion, LipSync, FacialClip, and Custom
    ///     layers blend together during idle and speech states.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ConvaiFacialCompositionProfile",
        menuName = "Convai/Animation/Facial Composition Profile")]
    public sealed class ConvaiFacialCompositionProfile : ScriptableObject
    {
        private const char PatternSeparator = ';';

        [Header("Region Name Patterns (semicolon-separated, case-insensitive substring match)")]
        [Tooltip("Blendshapes whose name contains any of these substrings are classified as Mouth.")]
        [SerializeField] private string _mouthPatterns = "Mouth;Lip;Tongue;Jaw_Open";

        [Tooltip("Blendshapes classified as Brow/Forehead.")]
        [SerializeField] private string _browPatterns = "Brow;Forehead";

        [Tooltip("Blendshapes classified as Eye.")]
        [SerializeField] private string _eyePatterns = "Eye_Blink;Eye_Squint;Eye_Wide;Eye_Look";

        [Tooltip("Blendshapes classified as Cheek/Nose.")]
        [SerializeField] private string _cheekPatterns = "Cheek;Nose;Sneer";

        [Tooltip("Blendshapes classified as Jaw (separate from Mouth for directional jaw movement).")]
        [SerializeField] private string _jawPatterns = "Jaw_Forward;Jaw_Left;Jaw_Right";

        [Header("Mesh Discovery Priority Patterns (semicolon-separated)")]
        [Tooltip("Mesh names matching these substrings are prioritized first for facial blendshape discovery.")]
        [SerializeField] private string _headMeshPatterns = "head;face";

        [Tooltip("Secondary priority mesh name patterns (teeth, jaw meshes).")]
        [SerializeField] private string _secondaryMeshPatterns = "teeth;tooth";

        [Tooltip("Tertiary priority mesh name patterns (tongue meshes).")]
        [SerializeField] private string _tertiaryMeshPatterns = "tongue";

        [Header("Speech Blend Timing")]
        [Tooltip("Seconds to ramp speech blend factor from 0 to 1 when speech starts.")]
        [SerializeField] [Min(0.01f)] private float _speechRampUpDuration = 0.15f;

        [Tooltip("Seconds to ramp speech blend factor from 1 to 0 when speech ends.")]
        [SerializeField] [Min(0.01f)] private float _speechRampDownDuration = 0.4f;

        [Header("Global Normalization")]
        [Tooltip("When enabled, composed values exceeding 100 are clamped after all layers contribute.")]
        [SerializeField] private bool _enableGlobalNormalization;

        [Header("Per-Region Composition")]
        [SerializeField] private RegionBlendConfig _mouthConfig = RegionBlendConfig.Create(
            idleEmotion: 1f, idleFacialClip: 1f, idleLipSync: 0f,
            speakingEmotion: 0.2f, speakingFacialClip: 0f, speakingLipSync: 1f);

        [SerializeField] private RegionBlendConfig _browConfig = RegionBlendConfig.Create(
            idleEmotion: 1f, idleFacialClip: 1f, idleLipSync: 0f,
            speakingEmotion: 0.85f, speakingFacialClip: 0.5f, speakingLipSync: 0.15f);

        [SerializeField] private RegionBlendConfig _eyeConfig = RegionBlendConfig.Create(
            idleEmotion: 0.8f, idleFacialClip: 1f, idleLipSync: 0f,
            speakingEmotion: 0.7f, speakingFacialClip: 0.4f, speakingLipSync: 0.1f);

        [SerializeField] private RegionBlendConfig _cheekConfig = RegionBlendConfig.Create(
            idleEmotion: 1f, idleFacialClip: 0.8f, idleLipSync: 0f,
            speakingEmotion: 0.7f, speakingFacialClip: 0.3f, speakingLipSync: 0.25f);

        [SerializeField] private RegionBlendConfig _jawConfig = RegionBlendConfig.Create(
            idleEmotion: 0.5f, idleFacialClip: 0.5f, idleLipSync: 0f,
            speakingEmotion: 0.1f, speakingFacialClip: 0f, speakingLipSync: 1f);

        [SerializeField] private RegionBlendConfig _otherConfig = RegionBlendConfig.Create(
            idleEmotion: 1f, idleFacialClip: 1f, idleLipSync: 0f,
            speakingEmotion: 0.6f, speakingFacialClip: 0.4f, speakingLipSync: 0.3f);

        private List<string> _parsedMouth;
        private List<string> _parsedBrow;
        private List<string> _parsedEye;
        private List<string> _parsedCheek;
        private List<string> _parsedJaw;
        private List<string> _parsedHeadMesh;
        private List<string> _parsedSecondaryMesh;
        private List<string> _parsedTertiaryMesh;
        private int _cachedHash;

        public float SpeechRampUpDuration => _speechRampUpDuration;
        public float SpeechRampDownDuration => _speechRampDownDuration;
        public bool EnableGlobalNormalization => _enableGlobalNormalization;

        public ref readonly RegionBlendConfig MouthConfig => ref _mouthConfig;
        public ref readonly RegionBlendConfig BrowConfig => ref _browConfig;
        public ref readonly RegionBlendConfig EyeConfig => ref _eyeConfig;
        public ref readonly RegionBlendConfig CheekConfig => ref _cheekConfig;
        public ref readonly RegionBlendConfig JawConfig => ref _jawConfig;
        public ref readonly RegionBlendConfig OtherConfig => ref _otherConfig;

        public RegionBlendConfig GetRegionConfig(FacialBlendshapeRegion region)
        {
            return region switch
            {
                FacialBlendshapeRegion.Mouth => _mouthConfig,
                FacialBlendshapeRegion.Brow => _browConfig,
                FacialBlendshapeRegion.Eye => _eyeConfig,
                FacialBlendshapeRegion.Cheek => _cheekConfig,
                FacialBlendshapeRegion.Jaw => _jawConfig,
                FacialBlendshapeRegion.Other => _otherConfig,
                _ => _otherConfig
            };
        }

        public FacialBlendshapeRegion ClassifyBlendshape(string blendshapeName)
        {
            EnsureParsedPatterns();

            if (MatchesAny(blendshapeName, _parsedMouth)) return FacialBlendshapeRegion.Mouth;
            if (MatchesAny(blendshapeName, _parsedJaw)) return FacialBlendshapeRegion.Jaw;
            if (MatchesAny(blendshapeName, _parsedBrow)) return FacialBlendshapeRegion.Brow;
            if (MatchesAny(blendshapeName, _parsedEye)) return FacialBlendshapeRegion.Eye;
            if (MatchesAny(blendshapeName, _parsedCheek)) return FacialBlendshapeRegion.Cheek;

            return FacialBlendshapeRegion.Other;
        }

        /// <summary>
        ///     Returns the discovery priority for a mesh based on configured name patterns.
        ///     Lower values = higher priority. Returns <c>int.MaxValue</c> for unmatched meshes.
        /// </summary>
        public int GetMeshDiscoveryPriority(string meshName)
        {
            EnsureParsedPatterns();

            if (MatchesAny(meshName, _parsedHeadMesh)) return 0;
            if (MatchesAny(meshName, _parsedSecondaryMesh)) return 1;
            if (MatchesAny(meshName, _parsedTertiaryMesh)) return 2;
            return 3;
        }

        public int ComputeConfigurationHash()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (_mouthPatterns ?? string.Empty).GetHashCode();
                hash = hash * 31 + (_browPatterns ?? string.Empty).GetHashCode();
                hash = hash * 31 + (_eyePatterns ?? string.Empty).GetHashCode();
                hash = hash * 31 + (_cheekPatterns ?? string.Empty).GetHashCode();
                hash = hash * 31 + (_jawPatterns ?? string.Empty).GetHashCode();
                hash = hash * 31 + (_headMeshPatterns ?? string.Empty).GetHashCode();
                hash = hash * 31 + (_secondaryMeshPatterns ?? string.Empty).GetHashCode();
                hash = hash * 31 + (_tertiaryMeshPatterns ?? string.Empty).GetHashCode();
                hash = hash * 31 + _speechRampUpDuration.GetHashCode();
                hash = hash * 31 + _speechRampDownDuration.GetHashCode();
                hash = hash * 31 + _enableGlobalNormalization.GetHashCode();
                hash = hash * 31 + HashRegionConfig(_mouthConfig);
                hash = hash * 31 + HashRegionConfig(_browConfig);
                hash = hash * 31 + HashRegionConfig(_eyeConfig);
                hash = hash * 31 + HashRegionConfig(_cheekConfig);
                hash = hash * 31 + HashRegionConfig(_jawConfig);
                hash = hash * 31 + HashRegionConfig(_otherConfig);
                return hash;
            }
        }

        private void OnValidate()
        {
            InvalidatePatternCache();
        }

        private void OnEnable()
        {
            InvalidatePatternCache();
        }

        private void InvalidatePatternCache()
        {
            _parsedMouth = null;
            _parsedBrow = null;
            _parsedEye = null;
            _parsedCheek = null;
            _parsedJaw = null;
            _parsedHeadMesh = null;
            _parsedSecondaryMesh = null;
            _parsedTertiaryMesh = null;
            _cachedHash = 0;
        }

        private void EnsureParsedPatterns()
        {
            int currentHash = ComputeConfigurationHash();
            if (_parsedMouth != null && currentHash == _cachedHash)
                return;

            _parsedMouth = ParsePatterns(_mouthPatterns);
            _parsedBrow = ParsePatterns(_browPatterns);
            _parsedEye = ParsePatterns(_eyePatterns);
            _parsedCheek = ParsePatterns(_cheekPatterns);
            _parsedJaw = ParsePatterns(_jawPatterns);
            _parsedHeadMesh = ParsePatterns(_headMeshPatterns);
            _parsedSecondaryMesh = ParsePatterns(_secondaryMeshPatterns);
            _parsedTertiaryMesh = ParsePatterns(_tertiaryMeshPatterns);
            _cachedHash = currentHash;
        }

        private static List<string> ParsePatterns(string raw)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(raw))
                return result;

            string[] parts = raw.Split(PatternSeparator, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string pattern = parts[i].Trim();
                if (!string.IsNullOrEmpty(pattern))
                    result.Add(pattern);
            }

            return result;
        }

        private static bool MatchesAny(string name, List<string> patterns)
        {
            if (patterns == null || patterns.Count == 0 || string.IsNullOrEmpty(name))
                return false;

            for (int i = 0; i < patterns.Count; i++)
            {
                if (name.IndexOf(patterns[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static int HashRegionConfig(in RegionBlendConfig config)
        {
            unchecked
            {
                int h = config.IdleEmotionWeight.GetHashCode();
                h = h * 31 + config.IdleFacialClipWeight.GetHashCode();
                h = h * 31 + config.IdleLipSyncWeight.GetHashCode();
                h = h * 31 + config.IdleCustomWeight.GetHashCode();
                h = h * 31 + config.SpeakingEmotionWeight.GetHashCode();
                h = h * 31 + config.SpeakingFacialClipWeight.GetHashCode();
                h = h * 31 + config.SpeakingLipSyncWeight.GetHashCode();
                h = h * 31 + config.SpeakingCustomWeight.GetHashCode();
                h = h * 31 + ((int)config.Mode).GetHashCode();
                h = h * 31 + config.EnableNormalization.GetHashCode();
                return h;
            }
        }
    }
}
