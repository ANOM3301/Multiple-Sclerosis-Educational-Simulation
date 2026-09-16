using System.Collections.Generic;
using Convai.Runtime.Embodiment;
using Convai.Runtime.Components;
using UnityEngine;

namespace Convai.Runtime.Animation
{
    /// <summary>
    ///     Single authoritative writer for runtime facial blendshape output on a character.
    ///     Sources submit logical layer weights during the frame and the host writes the final
    ///     composed result once in <see cref="LateUpdate" />.
    /// </summary>
    /// <remarks>
    ///     Supports both built-in layers (see <see cref="FacialBlendshapeLayers" />) and
    ///     user-registered custom layers via <see cref="RegisterCustomLayer" />.
    /// </remarks>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(EmbodimentExecutionOrders.FacialCompositor)]
    [AddComponentMenu("")]
    public sealed class FacialBlendshapeCompositorHost : MonoBehaviour
    {
        /// <summary>
        ///     Primary built-in <c>Resources</c> relative path for the default composition profile.
        ///     Full resolution order (custom hook + candidates) is defined by
        ///     <see cref="FacialCompositionProfileResourceResolution" />.
        /// </summary>
        public static string DefaultProfileResourcePath =>
            FacialCompositionProfileResourceResolution.PrimaryBuiltInResourceRelativePath;

        /// <summary>
        ///     Minimum weight delta before writing to <see cref="SkinnedMeshRenderer" /> blendshapes.
        ///     Shared with direct lip-sync output so behavior matches compositor writes.
        /// </summary>
        internal const float BlendshapeWriteEpsilon = 0.0001f;

        private readonly Dictionary<int, Dictionary<BlendshapeTargetKey, float>> _layerFrames = new();
        private readonly HashSet<int> _activeLayersThisFrame = new();
        private readonly HashSet<int> _registeredCustomLayers = new();
        private readonly Dictionary<int, string> _customLayerDebugNames = new();

        private readonly Dictionary<BlendshapeTargetKey, float> _lastApplied = new();
        private readonly List<BlendshapeTargetKey> _composeKeys = new();
        private readonly HashSet<BlendshapeTargetKey> _composeKeySet = new();
        private readonly HashSet<BlendshapeTargetKey> _headOwnedTargets = new();
        private readonly Dictionary<BlendshapeTargetKey, FacialBlendshapeRegion> _regionCache = new();

        private ConvaiFacialCompositionProfile _compositionProfile;
        private int _lastProfileConfigHash;
        private float _speechBlendFactor;
        private bool _lipSyncSpeechReportedThisFrame;
        private bool _lipSyncSpeechActiveThisFrame;
        private int _nextCustomLayerId = FacialBlendshapeLayers.CustomLayerStart;
        private bool _defaultProfileLoadAttempted;
        private bool _loggedComposePassThroughFallback;

        /// <summary>Current smoothed speech factor. 0 = idle, 1 = fully talking.</summary>
        public float SpeechBlendFactor => _speechBlendFactor;

        /// <summary>Whether multi-region composition is active.</summary>
        public bool IsRegionCompositionActive => _compositionProfile != null;

        public ConvaiFacialCompositionProfile CompositionProfile => _compositionProfile;

        /// <summary>
        ///     Finds an existing compositor on <paramref name="context" /> or under the preferred
        ///     character owner. Never adds a component.
        /// </summary>
        public static FacialBlendshapeCompositorHost TryResolve(Component context)
        {
            if (context == null) return null;

            FacialBlendshapeCompositorHost host = context.GetComponent<FacialBlendshapeCompositorHost>();
            if (host != null)
                return host;

            GameObject owner = ResolvePreferredOwner(context) ?? context.gameObject;

            host = owner.GetComponent<FacialBlendshapeCompositorHost>();
            if (host != null)
                return host;

            FacialBlendshapeCompositorHost[] existingHosts =
                owner.GetComponentsInChildren<FacialBlendshapeCompositorHost>(true);
            return existingHosts.Length > 0 ? existingHosts[0] : null;
        }

        public static FacialBlendshapeCompositorHost GetOrCreate(Component context)
        {
            FacialBlendshapeCompositorHost host = TryResolve(context);
            if (host != null)
                return host;

            if (context == null || !UnityEngine.Application.isPlaying)
                return null;

            GameObject owner = ResolvePreferredOwner(context) ?? context.gameObject;
            FacialBlendshapeCompositorHost created = owner.AddComponent<FacialBlendshapeCompositorHost>();
            created.hideFlags = EmbodimentContext.RuntimeInfrastructureHideFlags();
            return created;
        }

        private void Awake()
        {
            hideFlags = EmbodimentContext.RuntimeInfrastructureHideFlags();
            EnsureDefaultProfileLoaded();
        }

        public void SetCompositionProfile(ConvaiFacialCompositionProfile profile)
        {
            if (ReferenceEquals(_compositionProfile, profile))
                return;

            _compositionProfile = profile;
            _lastProfileConfigHash = 0;
            if (profile != null)
                _defaultProfileLoadAttempted = true;
            InvalidateRegionCache();
        }

        /// <summary>
        ///     Attempts to load the SDK default composition profile from <c>Resources</c> (see
        ///     <see cref="FacialCompositionProfileResourceResolution" />) when no profile has been
        ///     explicitly assigned. Safe to call repeatedly: the load is performed at most once per instance.
        /// </summary>
        /// <returns>
        ///     <c>true</c> if a profile is assigned after the call (either previously set or newly
        ///     loaded); <c>false</c> otherwise (e.g. the default asset is missing from the build).
        /// </returns>
        public bool EnsureDefaultProfileLoaded()
        {
            if (_compositionProfile != null)
                return true;
            if (_defaultProfileLoadAttempted)
                return false;

            _defaultProfileLoadAttempted = true;
            ConvaiFacialCompositionProfile loaded =
                FacialCompositionProfileResourceResolution.TryLoadDefaultProfileFromResources();
            if (loaded == null)
                return false;

            _compositionProfile = loaded;
            _lastProfileConfigHash = 0;
            InvalidateRegionCache();
            return true;
        }

        // ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //  Custom Layer Registration
        // ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        ///     Registers a custom layer and returns its unique layer ID.
        ///     Custom layer values are composed using the Custom weight in <see cref="RegionBlendConfig" />.
        /// </summary>
        public int RegisterCustomLayer(string debugName = null)
        {
            int layerId = _nextCustomLayerId++;
            _registeredCustomLayers.Add(layerId);
            if (!string.IsNullOrEmpty(debugName))
                _customLayerDebugNames[layerId] = debugName;
            return layerId;
        }

        /// <summary>
        ///     Unregisters a previously registered custom layer.
        /// </summary>
        public void UnregisterCustomLayer(int layerId)
        {
            if (!FacialBlendshapeLayers.IsCustom(layerId)) return;
            _registeredCustomLayers.Remove(layerId);
            _customLayerDebugNames.Remove(layerId);
            _layerFrames.Remove(layerId);
        }

        // ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //  Layer Submission
        // ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        ///     Submits a layer contribution for the current frame.
        /// </summary>
        public void SubmitLayer(
            IFacialBlendshapeSource source,
            int layerId,
            IReadOnlyList<BlendshapeTargetKey> targets,
            IReadOnlyList<float> weights,
            int count)
        {
            if (targets == null || weights == null || count <= 0) return;

            Dictionary<BlendshapeTargetKey, float> destination = GetOrCreateFrameLayer(layerId);
            _activeLayersThisFrame.Add(layerId);

            int appliedCount = Mathf.Min(count, Mathf.Min(targets.Count, weights.Count));
            for (int i = 0; i < appliedCount; i++)
            {
                BlendshapeTargetKey key = targets[i];
                if (!key.IsValid) continue;

                float weight = Mathf.Clamp(weights[i], 0f, 100f);
                if (weight <= BlendshapeWriteEpsilon) continue;

                if (destination.TryGetValue(key, out float current) && current >= weight)
                    continue;

                destination[key] = weight;
            }
        }

        /// <summary>
        ///     Reports whether LipSync is actively speaking this frame.
        /// </summary>
        public void SetLipSyncSpeechActive(IFacialBlendshapeSource source, bool isActive)
        {
            _lipSyncSpeechReportedThisFrame = true;
            if (isActive) _lipSyncSpeechActiveThisFrame = true;
        }

        /// <summary>
        ///     Registers blendshape targets owned exclusively by the head-look system.
        /// </summary>
        public void RegisterHeadLookOwnedTargets(
            IFacialBlendshapeSource source,
            IReadOnlyList<BlendshapeTargetKey> targets,
            int count)
        {
            _headOwnedTargets.Clear();
            if (targets == null || count <= 0) return;

            int appliedCount = Mathf.Min(count, targets.Count);
            for (int i = 0; i < appliedCount; i++)
            {
                BlendshapeTargetKey key = targets[i];
                if (!key.IsValid) continue;
                _headOwnedTargets.Add(key);
            }
        }

        public void ClearHeadLookOwnedTargets(IFacialBlendshapeSource source)
        {
            _headOwnedTargets.Clear();
        }

        // ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //  Composition Pipeline
        // ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        private static GameObject ResolvePreferredOwner(Component context)
        {
            ConvaiCharacter characterRoot = context.GetComponentInParent<ConvaiCharacter>(true);
            return characterRoot != null ? characterRoot.gameObject : context.gameObject;
        }

        private void LateUpdate()
        {
            float targetSpeechFactor = _lipSyncSpeechReportedThisFrame && _lipSyncSpeechActiveThisFrame ? 1f : 0f;
            float deltaTime = UnityEngine.Application.isPlaying ? Time.deltaTime : (1f / 60f);
            if (deltaTime <= 0f) deltaTime = 1f / 60f;

            if (_compositionProfile == null)
                EnsureDefaultProfileLoaded();

            RefreshProfileState();
            UpdateSpeechBlendFactor(targetSpeechFactor, deltaTime);

            bool hasAnyActivity = _activeLayersThisFrame.Count > 0 || _lastApplied.Count > 0;
            if (!hasAnyActivity)
            {
                ClearFrameState();
                return;
            }

            BuildComposeKeySet();

            if (_compositionProfile != null)
                ComposeAndWriteRegion();
            else
                ComposePassThrough();

            ClearFrameState();
        }

        private void UpdateSpeechBlendFactor(float targetSpeechFactor, float deltaTime)
        {
            if (_compositionProfile == null)
            {
                _speechBlendFactor = targetSpeechFactor;
                return;
            }

            bool rampingUp = targetSpeechFactor > _speechBlendFactor;
            float blendWindow = rampingUp
                ? _compositionProfile.SpeechRampUpDuration
                : _compositionProfile.SpeechRampDownDuration;

            float step = deltaTime / Mathf.Max(0.01f, blendWindow);
            _speechBlendFactor = Mathf.MoveTowards(_speechBlendFactor, targetSpeechFactor, step);
        }

        private void RefreshProfileState()
        {
            if (_compositionProfile == null)
            {
                if (_lastProfileConfigHash != 0)
                {
                    _lastProfileConfigHash = 0;
                    InvalidateRegionCache();
                }
                return;
            }

            int configHash = _compositionProfile.ComputeConfigurationHash();
            if (configHash != _lastProfileConfigHash)
            {
                _lastProfileConfigHash = configHash;
                InvalidateRegionCache();
            }
        }

        private FacialBlendshapeRegion ClassifyRegion(BlendshapeTargetKey key)
        {
            if (_regionCache.TryGetValue(key, out FacialBlendshapeRegion cached))
                return cached;

            string blendshapeName = key.Mesh.sharedMesh.GetBlendShapeName(key.BlendshapeIndex);
            FacialBlendshapeRegion region = _compositionProfile.ClassifyBlendshape(blendshapeName);
            _regionCache[key] = region;
            return region;
        }

        private void InvalidateRegionCache()
        {
            _regionCache.Clear();
        }

        private void ComposeAndWriteRegion()
        {
            float speechFactor = _speechBlendFactor;
            bool globalNorm = _compositionProfile.EnableGlobalNormalization;

            for (int i = 0; i < _composeKeys.Count; i++)
            {
                BlendshapeTargetKey key = _composeKeys[i];
                if (!key.IsValid) continue;

                if (_headOwnedTargets.Contains(key))
                {
                    float headLookVal = GetLayerValue(FacialBlendshapeLayers.HeadLook, key);
                    WriteIfChanged(key, headLookVal);
                    continue;
                }

                FacialBlendshapeRegion region = ClassifyRegion(key);
                RegionBlendConfig config = _compositionProfile.GetRegionConfig(region);

                config.GetInterpolatedWeights(speechFactor,
                    out float emotionW, out float facialClipW, out float lipSyncW,
                    out float customW);

                float emotionGeneral = GetLayerValue(FacialBlendshapeLayers.EmotionGeneral, key);
                float emotionMouth = GetLayerValue(FacialBlendshapeLayers.EmotionMouth, key);
                float lipSync = GetLayerValue(FacialBlendshapeLayers.LipSync, key);
                float eyes = GetLayerValue(FacialBlendshapeLayers.Eyes, key);
                float facialClipGeneral = GetLayerValue(FacialBlendshapeLayers.FacialClipGeneral, key);
                float facialClipMouth = GetLayerValue(FacialBlendshapeLayers.FacialClipMouth, key);
                float customVal = GetMaxCustomLayerValue(key);

                float emotionVal = Mathf.Max(emotionGeneral, emotionMouth);
                float facialClipVal = Mathf.Max(facialClipGeneral, facialClipMouth);

                float composed = config.Compose(
                    emotionVal, emotionW,
                    facialClipVal, facialClipW,
                    lipSync, lipSyncW,
                    customVal, customW);

                float finalWeight = Mathf.Max(eyes, composed);

                if (globalNorm && finalWeight > 100f)
                    finalWeight = 100f;

                finalWeight = Mathf.Clamp(finalWeight, 0f, 100f);
                WriteIfChanged(key, finalWeight);
            }
        }

        private float GetLayerValue(int layerId, BlendshapeTargetKey key)
        {
            if (!_layerFrames.TryGetValue(layerId, out Dictionary<BlendshapeTargetKey, float> frame))
                return 0f;
            frame.TryGetValue(key, out float value);
            return value;
        }

        private float GetMaxCustomLayerValue(BlendshapeTargetKey key)
        {
            float max = 0f;
            foreach (int customId in _registeredCustomLayers)
            {
                if (!_layerFrames.TryGetValue(customId, out Dictionary<BlendshapeTargetKey, float> frame))
                    continue;
                if (frame.TryGetValue(key, out float val) && val > max)
                    max = val;
            }
            return max;
        }

        /// <summary>
        ///     Fallback composition path used when no <see cref="ConvaiFacialCompositionProfile" />
        ///     is available (e.g. the default asset was stripped from the build). Combines all
        ///     active layer contributions per target using a max-blend so basic features like
        ///     LipSync and Emotion still produce visible output instead of being zeroed out.
        /// </summary>
        private void ComposePassThrough()
        {
            if (!_loggedComposePassThroughFallback)
            {
                _loggedComposePassThroughFallback = true;
                Debug.LogWarning(
                    "[FacialBlendshapeCompositorHost] No facial composition profile is assigned; " +
                    "using max-blend fallback (higher CPU cost per target). " +
                    "Assign ConvaiFacialCompositionProfile on the context or compositor, or ensure a default exists " +
                    "under Resources (tried: " +
                    string.Join(", ", FacialCompositionProfileResourceResolution.BuiltInDefaultProfileResourceCandidates) +
                    ").",
                    this);
            }

            for (int i = 0; i < _composeKeys.Count; i++)
            {
                BlendshapeTargetKey key = _composeKeys[i];
                if (!key.IsValid) continue;

                if (_headOwnedTargets.Contains(key))
                {
                    float headLookVal = GetLayerValue(FacialBlendshapeLayers.HeadLook, key);
                    WriteIfChanged(key, headLookVal);
                    continue;
                }

                float maxWeight = 0f;
                foreach (KeyValuePair<int, Dictionary<BlendshapeTargetKey, float>> kvp in _layerFrames)
                {
                    if (kvp.Value.Count == 0) continue;
                    if (kvp.Value.TryGetValue(key, out float val) && val > maxWeight)
                        maxWeight = val;
                }

                WriteIfChanged(key, Mathf.Clamp(maxWeight, 0f, 100f));
            }
        }

        private void ClearComposedOutput()
        {
            if (_lastApplied.Count == 0)
                return;

            BuildComposeKeySet();
            for (int i = 0; i < _composeKeys.Count; i++)
            {
                BlendshapeTargetKey key = _composeKeys[i];
                if (!key.IsValid) continue;
                WriteIfChanged(key, 0f);
            }

            _lastApplied.Clear();
        }

        private void WriteIfChanged(BlendshapeTargetKey key, float finalWeight)
        {
            finalWeight = Mathf.Clamp(finalWeight, 0f, 100f);
            _lastApplied.TryGetValue(key, out float previousWeight);

            if (Mathf.Abs(finalWeight - previousWeight) < BlendshapeWriteEpsilon)
                return;

            key.Mesh.SetBlendShapeWeight(key.BlendshapeIndex, finalWeight);

            if (finalWeight <= BlendshapeWriteEpsilon)
                _lastApplied.Remove(key);
            else
                _lastApplied[key] = finalWeight;
        }

        // ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //  Frame Layer Management
        // ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        private Dictionary<BlendshapeTargetKey, float> GetOrCreateFrameLayer(int layerId)
        {
            if (_layerFrames.TryGetValue(layerId, out Dictionary<BlendshapeTargetKey, float> existing))
                return existing;

            var frame = new Dictionary<BlendshapeTargetKey, float>();
            _layerFrames[layerId] = frame;
            return frame;
        }

        private void BuildComposeKeySet()
        {
            _composeKeys.Clear();
            _composeKeySet.Clear();

            foreach (KeyValuePair<int, Dictionary<BlendshapeTargetKey, float>> kvp in _layerFrames)
            {
                if (kvp.Value.Count == 0) continue;
                AddComposeKeys(kvp.Value);
            }

            AddComposeKeys(_headOwnedTargets);
            AddComposeKeys(_lastApplied);
        }

        private void AddComposeKeys(Dictionary<BlendshapeTargetKey, float> source)
        {
            foreach (BlendshapeTargetKey key in source.Keys)
            {
                if (!_composeKeySet.Add(key)) continue;
                _composeKeys.Add(key);
            }
        }

        private void AddComposeKeys(IEnumerable<BlendshapeTargetKey> keys)
        {
            foreach (BlendshapeTargetKey key in keys)
            {
                if (!_composeKeySet.Add(key)) continue;
                _composeKeys.Add(key);
            }
        }

        private void ClearFrameState()
        {
            foreach (KeyValuePair<int, Dictionary<BlendshapeTargetKey, float>> kvp in _layerFrames)
                kvp.Value.Clear();

            _activeLayersThisFrame.Clear();
            _lipSyncSpeechReportedThisFrame = false;
            _lipSyncSpeechActiveThisFrame = false;
        }
    }
}
