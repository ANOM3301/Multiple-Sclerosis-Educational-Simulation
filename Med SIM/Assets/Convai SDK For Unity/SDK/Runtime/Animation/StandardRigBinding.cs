using System.Collections.Generic;
using Convai.Domain.Embodiment.Interfaces;
using Convai.Domain.Embodiment.Semantics;
using Convai.Runtime.Embodiment;
using UnityEngine;

namespace Convai.Runtime.Animation
{
    /// <summary>
    ///     Default <see cref="IStandardRigBinding" /> implementation that inspects the
    ///     character hierarchy at setup time and caches bone / blendshape resolution tables.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The binding resolves bones via Unity's <see cref="HumanBodyBones" /> system
    ///         when the character has a humanoid <see cref="Animator" />. For generic rigs it
    ///         falls back to name-based lookup using the convention tables in
    ///         <see cref="RigConventionMaps" />.
    ///     </para>
    ///     <para>
    ///         Blendshape resolution is convention-driven: the binding asks
    ///         <see cref="RigConventionResolver" /> which rig this is, consults the convention
    ///         table, and resolves the named blendshape on the most appropriate mesh
    ///         (preferring the mesh that owns the most matches ; i.e. the face mesh, not
    ///         costumes).
    ///     </para>
    ///     <para>
    ///         Call <see cref="Rebuild" /> whenever the character is re-skinned or outfits
    ///         are hot-swapped at runtime; otherwise the binding is safe to keep for the full
    ///         character lifetime.
    ///     </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class StandardRigBinding : MonoBehaviour, IStandardRigBinding
    {
        [Tooltip("Leave empty to auto-detect every SkinnedMeshRenderer in this hierarchy.")]
        [SerializeField] private List<SkinnedMeshRenderer> facialMeshes = new();

        [Tooltip("Optional convention override. When set to a non-Unknown value, auto detection is skipped.")]
        [SerializeField] private RigConvention conventionOverride = RigConvention.Unknown;

        [Tooltip("Required when Convention Override is Custom. Maps Convai semantic blendshapes to this rig's names.")]
        [SerializeField] private CustomRigConventionMap customConventionMap;

        private readonly Dictionary<StandardBone, Transform> _boneCache = new();
        private readonly Dictionary<StandardBlendshape, BlendshapeResolution> _blendshapeCache = new();
        private RigConvention _detectedConvention = RigConvention.Unknown;
        private Animator _animator;
        private Transform[] _hierarchyTransformCache;

        /// <inheritdoc />
        public Transform Root
        {
            get
            {
                _animator ??= GetComponentInChildren<Animator>(true);
                return _animator != null ? _animator.transform : transform;
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<SkinnedMeshRenderer> FacialMeshes => facialMeshes;

        /// <inheritdoc />
        public RigConvention DetectedConvention => _detectedConvention;

        /// <summary>Custom semantic-to-blendshape map used when DetectedConvention is Custom.</summary>
        public CustomRigConventionMap CustomConventionMap => customConventionMap;

        /// <summary>Confidence score of the last detection pass in <c>[0, 1]</c>.</summary>
        public float DetectionConfidence { get; private set; }

        private void Awake()
        {
            Rebuild();
        }

        /// <summary>
        ///     Re-scans the hierarchy and rebuilds resolution tables. Safe to call when
        ///     outfits change, a mesh is replaced, or the user applies a convention override.
        /// </summary>
        public void Rebuild()
        {
            _boneCache.Clear();
            _blendshapeCache.Clear();

            if (facialMeshes == null || facialMeshes.Count == 0)
                AutoDetectFacialMeshes();
            else
            {
                PruneNullMeshes();
                if (facialMeshes.Count == 0)
                    AutoDetectFacialMeshes();
            }

            _animator = GetComponentInChildren<Animator>(true);
            _hierarchyTransformCache = GetComponentsInChildren<Transform>(true);

            if (conventionOverride != RigConvention.Unknown)
            {
                _detectedConvention = conventionOverride;
                DetectionConfidence = 1f;
            }
            else
            {
                _detectedConvention = RigConventionResolver.Detect(facialMeshes, out float confidence);
                DetectionConfidence = confidence;
            }

            SortFacialMeshesByResolutionPriority();

            NotifyContextRigBindingChanged();
        }

        /// <inheritdoc />
        public bool TryGetBone(StandardBone semantic, out Transform bone)
        {
            if (_boneCache.TryGetValue(semantic, out bone))
                return bone != null;

            bone = ResolveBone(semantic);
            if (bone != null)
                _boneCache[semantic] = bone;
            return bone != null;
        }

        /// <inheritdoc />
        public bool TryGetBlendshape(
            StandardBlendshape semantic,
            out SkinnedMeshRenderer mesh,
            out int blendshapeIndex)
        {
            if (_blendshapeCache.TryGetValue(semantic, out BlendshapeResolution cached))
            {
                mesh = cached.Mesh;
                blendshapeIndex = cached.Index;
                return cached.Mesh != null && cached.Index >= 0;
            }

            if (!ResolveBlendshape(semantic, out mesh, out blendshapeIndex))
            {
                _blendshapeCache[semantic] = new BlendshapeResolution(null, -1);
                return false;
            }

            _blendshapeCache[semantic] = new BlendshapeResolution(mesh, blendshapeIndex);
            return true;
        }

        private Transform ResolveBone(StandardBone semantic)
        {
            if (_animator != null && _animator.isHuman)
            {
                HumanBodyBones? human = MapToHumanBodyBones(semantic);
                if (human.HasValue)
                {
                    Transform bone = _animator.GetBoneTransform(human.Value);
                    if (bone != null) return bone;
                }
            }

            // Generic fallback ; bone name lookup.
            string[] candidates = MapToFallbackNames(semantic);
            if (candidates == null) return null;

            Transform[] all = _hierarchyTransformCache ?? GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < candidates.Length; i++)
            {
                string target = candidates[i];
                for (int j = 0; j < all.Length; j++)
                {
                    Transform t = all[j];
                    if (t != null && string.Equals(t.name, target, System.StringComparison.OrdinalIgnoreCase))
                        return t;
                }
            }

            return null;
        }

        private bool ResolveBlendshape(
            StandardBlendshape semantic,
            out SkinnedMeshRenderer mesh,
            out int blendshapeIndex)
        {
            mesh = null;
            blendshapeIndex = -1;

            if (!TryResolveBlendshapeName(semantic, out string blendshapeName))
                return false;

            for (int i = 0; i < facialMeshes.Count; i++)
            {
                SkinnedMeshRenderer smr = facialMeshes[i];
                if (smr == null || smr.sharedMesh == null) continue;

                int index = smr.sharedMesh.GetBlendShapeIndex(blendshapeName);
                if (index < 0) continue;

                mesh = smr;
                blendshapeIndex = index;
                return true;
            }

            return false;
        }

        private bool TryResolveBlendshapeName(StandardBlendshape semantic, out string blendshapeName)
        {
            blendshapeName = null;

            if (_detectedConvention == RigConvention.Custom)
                return customConventionMap != null &&
                       customConventionMap.TryGetBlendshapeName(semantic, out blendshapeName);

            IReadOnlyDictionary<StandardBlendshape, string> map =
                RigConventionMaps.ForConvention(_detectedConvention);
            return map.TryGetValue(semantic, out blendshapeName);
        }

        private void AutoDetectFacialMeshes()
        {
            facialMeshes.Clear();
            SkinnedMeshRenderer[] found = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < found.Length; i++)
            {
                SkinnedMeshRenderer smr = found[i];
                if (smr != null && smr.sharedMesh != null && smr.sharedMesh.blendShapeCount > 0)
                    facialMeshes.Add(smr);
            }
        }

        private void SortFacialMeshesByResolutionPriority()
        {
            if (facialMeshes == null || facialMeshes.Count < 2) return;

            var scores = new Dictionary<SkinnedMeshRenderer, MeshResolutionScore>();
            for (int i = 0; i < facialMeshes.Count; i++)
            {
                SkinnedMeshRenderer mesh = facialMeshes[i];
                if (mesh == null || mesh.sharedMesh == null) continue;

                scores[mesh] = new MeshResolutionScore(
                    CountSemanticBlendshapeMatches(mesh),
                    ResolveMeshNamePriority(mesh),
                    i);
            }

            facialMeshes.Sort((a, b) =>
            {
                if (ReferenceEquals(a, b)) return 0;
                if (a == null) return 1;
                if (b == null) return -1;

                MeshResolutionScore scoreA = scores.TryGetValue(a, out MeshResolutionScore aScore)
                    ? aScore
                    : new MeshResolutionScore(0, int.MaxValue, int.MaxValue);
                MeshResolutionScore scoreB = scores.TryGetValue(b, out MeshResolutionScore bScore)
                    ? bScore
                    : new MeshResolutionScore(0, int.MaxValue, int.MaxValue);

                int coverageCompare = scoreB.SemanticMatchCount.CompareTo(scoreA.SemanticMatchCount);
                if (coverageCompare != 0) return coverageCompare;

                int priorityCompare = scoreA.NamePriority.CompareTo(scoreB.NamePriority);
                if (priorityCompare != 0) return priorityCompare;

                return scoreA.OriginalIndex.CompareTo(scoreB.OriginalIndex);
            });
        }

        private int CountSemanticBlendshapeMatches(SkinnedMeshRenderer renderer)
        {
            if (renderer == null || renderer.sharedMesh == null) return 0;

            int count = 0;
            if (_detectedConvention == RigConvention.Custom)
            {
                IReadOnlyList<CustomRigConventionMap.BlendshapeMapping> mappings =
                    customConventionMap != null ? customConventionMap.Blendshapes : null;
                if (mappings == null) return 0;

                for (int i = 0; i < mappings.Count; i++)
                {
                    string blendshapeName = mappings[i].BlendshapeName;
                    if (!string.IsNullOrWhiteSpace(blendshapeName) &&
                        renderer.sharedMesh.GetBlendShapeIndex(blendshapeName) >= 0)
                    {
                        count++;
                    }
                }

                return count;
            }

            IReadOnlyDictionary<StandardBlendshape, string> map =
                RigConventionMaps.ForConvention(_detectedConvention);
            foreach (KeyValuePair<StandardBlendshape, string> kvp in map)
            {
                if (!string.IsNullOrWhiteSpace(kvp.Value) &&
                    renderer.sharedMesh.GetBlendShapeIndex(kvp.Value) >= 0)
                {
                    count++;
                }
            }

            return count;
        }

        private static int ResolveMeshNamePriority(SkinnedMeshRenderer renderer)
        {
            string meshName = renderer != null ? renderer.name : string.Empty;
            if (ContainsMeshPattern(meshName, "head") || ContainsMeshPattern(meshName, "face")) return 0;
            if (ContainsMeshPattern(meshName, "teeth") || ContainsMeshPattern(meshName, "tooth")) return 1;
            if (ContainsMeshPattern(meshName, "tongue")) return 2;
            return 3;
        }

        private static bool ContainsMeshPattern(string value, string pattern) =>
            !string.IsNullOrEmpty(value) &&
            value.IndexOf(pattern, System.StringComparison.OrdinalIgnoreCase) >= 0;

        private void PruneNullMeshes()
        {
            for (int i = facialMeshes.Count - 1; i >= 0; i--)
                if (facialMeshes[i] == null) facialMeshes.RemoveAt(i);
        }

        private void NotifyContextRigBindingChanged()
        {
            EmbodimentContext context = GetComponentInParent<EmbodimentContext>(true);
            if (context == null) return;

            context.NotifyRigBindingChanged(this);
        }

        private static HumanBodyBones? MapToHumanBodyBones(StandardBone semantic)
        {
            return semantic switch
            {
                StandardBone.Hips => HumanBodyBones.Hips,
                StandardBone.Spine => HumanBodyBones.Spine,
                StandardBone.Chest => HumanBodyBones.Chest,
                StandardBone.UpperChest => HumanBodyBones.UpperChest,
                StandardBone.Neck => HumanBodyBones.Neck,
                StandardBone.Head => HumanBodyBones.Head,
                StandardBone.LeftEye => HumanBodyBones.LeftEye,
                StandardBone.RightEye => HumanBodyBones.RightEye,
                StandardBone.LeftShoulder => HumanBodyBones.LeftShoulder,
                StandardBone.RightShoulder => HumanBodyBones.RightShoulder,
                StandardBone.LeftUpperArm => HumanBodyBones.LeftUpperArm,
                StandardBone.RightUpperArm => HumanBodyBones.RightUpperArm,
                _ => null
            };
        }

        private static readonly string[] FallbackHips = { "Hips", "CC_Base_Hip", "pelvis" };
        private static readonly string[] FallbackSpine = { "Spine", "CC_Base_Spine01", "spine_01" };
        private static readonly string[] FallbackChest = { "Chest", "Spine1", "CC_Base_Spine02", "spine_02" };
        private static readonly string[] FallbackUpperChest = { "UpperChest", "Spine2", "spine_03" };
        private static readonly string[] FallbackNeck = { "Neck", "CC_Base_NeckTwist01", "neck_01" };
        private static readonly string[] FallbackHead = { "Head", "CC_Base_Head", "head" };
        private static readonly string[] FallbackLeftEye = { "LeftEye", "Eye_L", "CC_Base_L_Eye", "eye_l" };
        private static readonly string[] FallbackRightEye = { "RightEye", "Eye_R", "CC_Base_R_Eye", "eye_r" };
        private static readonly string[] FallbackLeftShoulder = { "LeftShoulder", "CC_Base_L_Clavicle", "clavicle_l" };
        private static readonly string[] FallbackRightShoulder = { "RightShoulder", "CC_Base_R_Clavicle", "clavicle_r" };
        private static readonly string[] FallbackLeftUpperArm = { "LeftArm", "CC_Base_L_Upperarm", "upperarm_l" };
        private static readonly string[] FallbackRightUpperArm = { "RightArm", "CC_Base_R_Upperarm", "upperarm_r" };

        private static string[] MapToFallbackNames(StandardBone semantic)
        {
            return semantic switch
            {
                StandardBone.Hips => FallbackHips,
                StandardBone.Spine => FallbackSpine,
                StandardBone.Chest => FallbackChest,
                StandardBone.UpperChest => FallbackUpperChest,
                StandardBone.Neck => FallbackNeck,
                StandardBone.Head => FallbackHead,
                StandardBone.LeftEye => FallbackLeftEye,
                StandardBone.RightEye => FallbackRightEye,
                StandardBone.LeftShoulder => FallbackLeftShoulder,
                StandardBone.RightShoulder => FallbackRightShoulder,
                StandardBone.LeftUpperArm => FallbackLeftUpperArm,
                StandardBone.RightUpperArm => FallbackRightUpperArm,
                _ => null
            };
        }

        private readonly struct BlendshapeResolution
        {
            public BlendshapeResolution(SkinnedMeshRenderer mesh, int index)
            {
                Mesh = mesh;
                Index = index;
            }

            public SkinnedMeshRenderer Mesh { get; }
            public int Index { get; }
        }

        private readonly struct MeshResolutionScore
        {
            public MeshResolutionScore(int semanticMatchCount, int namePriority, int originalIndex)
            {
                SemanticMatchCount = semanticMatchCount;
                NamePriority = namePriority;
                OriginalIndex = originalIndex;
            }

            public int SemanticMatchCount { get; }
            public int NamePriority { get; }
            public int OriginalIndex { get; }
        }
    }
}
