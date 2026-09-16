using System;
using System.Collections.Generic;
using Convai.Domain.Embodiment.Interfaces;
using Convai.Domain.Embodiment.Taxonomy;
using Convai.Runtime.Animation;
using Convai.Runtime.Embodiment;
using UnityEngine;

namespace Convai.Modules.Emotion.Outputs
{
    /// <summary>
    ///     Drives facial blendshapes from composed emotion scores via the single-writer
    ///     <see cref="FacialBlendshapeCompositorHost" />. Separates mouth-region output onto
    ///     its own layer so LipSync can cross-fade cleanly with the active expression.
    /// </summary>
    [Serializable]
    public sealed class BlendshapeEmotionBinding :
        IEmotionOutputBinding,
        IFacialBlendshapeSource,
        IEmotionMouthWeightProvider
    {
        [SerializeField, Tooltip("Emotion-to-blendshape mapping. Slots with empty BlendshapeNames are ignored.")]
        private List<EmotionSlotBinding> slots = new();

        private readonly List<ResolvedSlot> _generalSlots = new();
        private readonly List<ResolvedSlot> _mouthSlots = new();

        private readonly List<BlendshapeTargetKey> _generalTargets = new();
        private readonly List<float> _generalWeights = new();
        private readonly List<BlendshapeTargetKey> _mouthTargets = new();
        private readonly List<float> _mouthWeights = new();
        private readonly Dictionary<BlendshapeTargetKey, float> _lastMouthWeights = new();

        private FacialBlendshapeCompositorHost _compositor;
        private Component _sourceComponent;
        private string _sourceName;
        private bool _bound;

        /// <inheritdoc />
        public Component SourceComponent => _sourceComponent;

        /// <inheritdoc />
        public string SourceName => _sourceName;

        public IReadOnlyList<EmotionSlotBinding> Slots => slots;

        /// <summary>
        ///     Replaces the authored slot list. Intended for preset factories and editor
        ///     tooling; prefer the inspector for one-off authoring.
        /// </summary>
        public void SetSlots(IReadOnlyList<EmotionSlotBinding> newSlots)
        {
            slots.Clear();
            if (newSlots == null) return;
            for (int i = 0; i < newSlots.Count; i++)
            {
                EmotionSlotBinding slot = newSlots[i];
                if (slot != null) slots.Add(slot);
            }
        }

        /// <summary>
        ///     Creates an ephemeral runtime copy that reuses authored slot data but owns its
        ///     own bound-state caches. Shared profile assets must never bind themselves
        ///     directly because multiple characters may use the same authoring asset.
        /// </summary>
        public BlendshapeEmotionBinding CreateRuntimeCopy()
        {
            var copy = new BlendshapeEmotionBinding();
            if (slots == null) return copy;

            var clonedSlots = new List<EmotionSlotBinding>(slots.Count);
            for (int i = 0; i < slots.Count; i++)
            {
                EmotionSlotBinding slot = slots[i];
                if (slot != null) clonedSlots.Add(slot.Clone());
            }

            copy.SetSlots(clonedSlots);
            return copy;
        }

        /// <inheritdoc />
        public void Bind(
            UnityEngine.Object owner,
            IEmotionTaxonomy taxonomy,
            IStandardRigBinding rig,
            AnimatorConductor conductor,
            FacialBlendshapeCompositorHost compositor)
        {
            Unbind(owner);

            _sourceComponent = owner as Component;
            _sourceName = owner != null ? owner.name : "BlendshapeEmotionBinding";
            _compositor = compositor;

            if (compositor == null || taxonomy == null || slots == null) return;

            IReadOnlyList<SkinnedMeshRenderer> facialMeshes = ResolveFacialMeshes(rig, owner as Component);
            if (facialMeshes.Count == 0) return;

            var scratchNames = new List<string>(4);
            for (int i = 0; i < slots.Count; i++)
            {
                EmotionSlotBinding slot = slots[i];
                if (slot == null) continue;
                if (string.IsNullOrWhiteSpace(slot.BlendshapeNames)) continue;
                if (string.IsNullOrWhiteSpace(slot.EmotionLabel)) continue;
                if (!taxonomy.TryResolve(slot.EmotionLabel, out EmotionDescriptor descriptor)) continue;

                slot.FillBlendshapeNames(scratchNames);
                if (scratchNames.Count == 0) continue;

                List<BlendshapeTargetKey> resolved = ResolveTargets(facialMeshes, scratchNames);
                if (resolved.Count == 0) continue;

                var resolvedSlot = new ResolvedSlot(
                    descriptor.Label,
                    slot.WeightMultiplier,
                    slot.FullBlendshapeWeight,
                    resolved);

                (slot.IsMouthShape ? _mouthSlots : _generalSlots).Add(resolvedSlot);
            }

            _bound = _generalSlots.Count > 0 || _mouthSlots.Count > 0;
        }

        /// <inheritdoc />
        public void Apply(IReadOnlyDictionary<string, float> scores, float neutralAlternationFactor)
        {
            if (!_bound || _compositor == null || scores == null) return;

            float intensityAttenuation = 1f - Mathf.Clamp01(neutralAlternationFactor);

            SubmitSlotGroup(_generalSlots, scores, intensityAttenuation,
                _generalTargets, _generalWeights, FacialBlendshapeLayers.EmotionGeneral, tracksMouth: false);

            SubmitSlotGroup(_mouthSlots, scores, intensityAttenuation,
                _mouthTargets, _mouthWeights, FacialBlendshapeLayers.EmotionMouth, tracksMouth: true);
        }

        /// <inheritdoc />
        public void Unbind(UnityEngine.Object owner)
        {
            _generalSlots.Clear();
            _mouthSlots.Clear();
            _generalTargets.Clear();
            _generalWeights.Clear();
            _mouthTargets.Clear();
            _mouthWeights.Clear();
            _lastMouthWeights.Clear();
            _compositor = null;
            _sourceComponent = null;
            _sourceName = null;
            _bound = false;
        }

        /// <inheritdoc />
        public bool TryGetMouthWeight(BlendshapeTargetKey key, out float weight)
        {
            return _lastMouthWeights.TryGetValue(key, out weight);
        }

        private void SubmitSlotGroup(
            List<ResolvedSlot> slotsGroup,
            IReadOnlyDictionary<string, float> scores,
            float intensityAttenuation,
            List<BlendshapeTargetKey> targetBuffer,
            List<float> weightBuffer,
            int layerId,
            bool tracksMouth)
        {
            targetBuffer.Clear();
            weightBuffer.Clear();
            if (tracksMouth) _lastMouthWeights.Clear();
            if (slotsGroup.Count == 0) return;

            for (int i = 0; i < slotsGroup.Count; i++)
            {
                ResolvedSlot slot = slotsGroup[i];
                scores.TryGetValue(slot.Label, out float score);
                if (score <= 0f) continue;

                float weight = Mathf.Clamp(
                    score * slot.WeightMultiplier * slot.FullWeight * intensityAttenuation,
                    0f, 100f);

                if (weight <= 0.001f) continue;

                for (int t = 0; t < slot.Targets.Count; t++)
                {
                    BlendshapeTargetKey key = slot.Targets[t];
                    targetBuffer.Add(key);
                    weightBuffer.Add(weight);

                    if (tracksMouth)
                    {
                        _lastMouthWeights.TryGetValue(key, out float previous);
                        if (weight > previous) _lastMouthWeights[key] = weight;
                    }
                }
            }

            if (targetBuffer.Count == 0) return;

            _compositor.SubmitLayer(this, layerId, targetBuffer, weightBuffer, targetBuffer.Count);
        }

        private static IReadOnlyList<SkinnedMeshRenderer> ResolveFacialMeshes(
            IStandardRigBinding rig,
            Component fallbackContext)
        {
            if (rig != null && rig.FacialMeshes != null && rig.FacialMeshes.Count > 0)
                return rig.FacialMeshes;

            if (fallbackContext == null) return Array.Empty<SkinnedMeshRenderer>();

            Transform root = rig?.Root;
            if (root == null)
            {
                EmbodimentContext context =
                    fallbackContext.GetComponentInParent<EmbodimentContext>(true);
                root = context != null ? context.CharacterRoot : fallbackContext.transform;
            }

            SkinnedMeshRenderer[] discovered = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            var filtered = new List<SkinnedMeshRenderer>(discovered.Length);
            for (int i = 0; i < discovered.Length; i++)
            {
                SkinnedMeshRenderer smr = discovered[i];
                if (smr == null || smr.sharedMesh == null) continue;
                if (smr.sharedMesh.blendShapeCount == 0) continue;
                filtered.Add(smr);
            }
            return filtered;
        }

        private static List<BlendshapeTargetKey> ResolveTargets(
            IReadOnlyList<SkinnedMeshRenderer> meshes,
            IReadOnlyList<string> blendshapeNames)
        {
            var result = new List<BlendshapeTargetKey>(blendshapeNames.Count);
            for (int n = 0; n < blendshapeNames.Count; n++)
            {
                string name = blendshapeNames[n];
                for (int m = 0; m < meshes.Count; m++)
                {
                    SkinnedMeshRenderer smr = meshes[m];
                    if (smr == null || smr.sharedMesh == null) continue;

                    int index = smr.sharedMesh.GetBlendShapeIndex(name);
                    if (index < 0) continue;

                    result.Add(new BlendshapeTargetKey(smr, index));
                }
            }
            return result;
        }

        private readonly struct ResolvedSlot
        {
            public ResolvedSlot(string label, float weightMultiplier, float fullWeight, List<BlendshapeTargetKey> targets)
            {
                Label = label;
                WeightMultiplier = weightMultiplier;
                FullWeight = fullWeight;
                Targets = targets;
            }

            public string Label { get; }
            public float WeightMultiplier { get; }
            public float FullWeight { get; }
            public List<BlendshapeTargetKey> Targets { get; }
        }
    }
}
