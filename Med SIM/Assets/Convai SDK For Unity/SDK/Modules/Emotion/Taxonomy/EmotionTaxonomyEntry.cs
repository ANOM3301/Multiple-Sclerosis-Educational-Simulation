using System;
using System.Collections.Generic;
using Convai.Domain.Embodiment.Taxonomy;
using UnityEngine;

namespace Convai.Modules.Emotion.Taxonomy
{
    /// <summary>
    ///     Serializable entry used by <see cref="EmotionTaxonomyAsset" /> to author the
    ///     emotion vocabulary.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Mirrors <see cref="EmotionDescriptor" /> in shape but stays mutable so Unity
    ///         can serialize it in the inspector. The asset converts entries into immutable
    ///         descriptors at load time.
    ///     </para>
    /// </remarks>
    [Serializable]
    public sealed class EmotionTaxonomyEntry
    {
        [SerializeField, Tooltip("Canonical lowercase label (e.g. 'joy', 'anger'). Must be unique within the taxonomy.")]
        private string label;

        [SerializeField, Tooltip("Alternative server labels that should resolve to this emotion.")]
        private List<string> aliases = new();

        [SerializeField, Tooltip("Emotions whose presence suppresses neutral synthesis on the mouth region.")]
        private List<string> complements = new();

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Default mouth-shape influence during non-speaking beats.")]
        private float defaultMouthInfluence = 0.5f;

        [SerializeField, Tooltip("Mark exactly one entry as the taxonomy's neutral baseline.")]
        private bool isNeutral;

        public string Label => label;
        public IReadOnlyList<string> Aliases => aliases;
        public IReadOnlyList<string> Complements => complements;
        public float DefaultMouthInfluence => defaultMouthInfluence;
        public bool IsNeutral => isNeutral;

        public EmotionTaxonomyEntry() { }

        public EmotionTaxonomyEntry(
            string label,
            IEnumerable<string> aliases,
            IEnumerable<string> complements,
            float defaultMouthInfluence,
            bool isNeutral)
        {
            this.label = label;
            this.aliases = aliases != null ? new List<string>(aliases) : new List<string>();
            this.complements = complements != null ? new List<string>(complements) : new List<string>();
            this.defaultMouthInfluence = defaultMouthInfluence;
            this.isNeutral = isNeutral;
        }

        /// <summary>Converts this serializable entry into an immutable <see cref="EmotionDescriptor" />.</summary>
        internal EmotionDescriptor ToDescriptor()
        {
            return new EmotionDescriptor(
                label: label,
                aliases: aliases?.ToArray() ?? Array.Empty<string>(),
                complements: complements?.ToArray() ?? Array.Empty<string>(),
                defaultMouthInfluence: defaultMouthInfluence,
                isNeutral: isNeutral);
        }
    }
}
