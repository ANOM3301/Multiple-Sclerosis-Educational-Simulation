using System;
using System.Collections.Generic;
using Convai.Domain.Embodiment.Taxonomy;
using UnityEngine;

namespace Convai.Modules.Emotion.Taxonomy
{
    /// <summary>
    ///     Data-driven emotion vocabulary asset. Authors ship an instance alongside the
    ///     emotion profile so the pipeline can evolve independently of the runtime protocol.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Exactly one entry must be marked as neutral. Labels are compared
    ///         case-insensitively and canonicalized to lowercase at load time. Aliases allow
    ///         server labels like <c>"happy"</c> to resolve to <c>"joy"</c> without polluting
    ///         the taxonomy.
    ///     </para>
    ///     <para>
    ///         The asset is immutable at runtime once <see cref="EnsureBuilt" /> has executed;
    ///         changes in the inspector while in play-mode invalidate the cached tables so
    ///         the next access rebuilds them.
    ///     </para>
    /// </remarks>
    [CreateAssetMenu(
        menuName = "Convai/Embodiment/Emotion Taxonomy",
        fileName = "EmotionTaxonomy")]
    public sealed class EmotionTaxonomyAsset : ScriptableObject, IEmotionTaxonomy
    {
        [SerializeField, Tooltip("All emotions recognized by this taxonomy. Exactly one must be neutral.")]
        private List<EmotionTaxonomyEntry> entries = new();

        private readonly List<EmotionDescriptor> _descriptors = new();
        private readonly Dictionary<string, EmotionDescriptor> _byLabel =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, EmotionDescriptor> _byAlias =
            new(StringComparer.OrdinalIgnoreCase);
        private EmotionDescriptor _neutral;
        private bool _built;

        /// <inheritdoc />
        public IReadOnlyList<EmotionDescriptor> Emotions
        {
            get
            {
                EnsureBuilt();
                return _descriptors;
            }
        }

        /// <inheritdoc />
        public EmotionDescriptor Neutral
        {
            get
            {
                EnsureBuilt();
                return _neutral;
            }
        }

        /// <inheritdoc />
        public bool TryResolve(string serverLabel, out EmotionDescriptor descriptor)
        {
            EnsureBuilt();
            descriptor = null;
            if (string.IsNullOrWhiteSpace(serverLabel)) return false;

            string trimmed = serverLabel.Trim();
            if (_byLabel.TryGetValue(trimmed, out descriptor)) return true;
            if (_byAlias.TryGetValue(trimmed, out descriptor)) return true;
            return false;
        }

        private void OnEnable() => _built = false;

        /// <summary>Rebuilds resolution tables from <see cref="entries" />. Idempotent.</summary>
        public void EnsureBuilt()
        {
            if (_built) return;

            _descriptors.Clear();
            _byLabel.Clear();
            _byAlias.Clear();
            _neutral = null;

            if (entries == null) { _built = true; return; }

            for (int i = 0; i < entries.Count; i++)
            {
                EmotionTaxonomyEntry entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.Label)) continue;

                EmotionDescriptor descriptor;
                try { descriptor = entry.ToDescriptor(); }
                catch (ArgumentException) { continue; }

                if (_byLabel.ContainsKey(descriptor.Label))
                    continue; // first-win on duplicates

                _descriptors.Add(descriptor);
                _byLabel[descriptor.Label] = descriptor;

                for (int a = 0; a < descriptor.Aliases.Count; a++)
                {
                    string alias = descriptor.Aliases[a];
                    if (string.IsNullOrWhiteSpace(alias)) continue;
                    alias = alias.Trim();
                    if (_byAlias.ContainsKey(alias)) continue;
                    _byAlias[alias] = descriptor;
                }

                if (descriptor.IsNeutral && _neutral == null)
                    _neutral = descriptor;
            }

            if (_neutral == null)
            {
                Debug.LogWarning(
                    "[EmotionTaxonomyAsset] No entry marked neutral; synthesized 'neutral' fallback. " +
                    "Mark exactly one entry as the neutral baseline to suppress this warning.",
                    this);

                // Synthesize a neutral so the module still works when the taxonomy is malformed.
                _neutral = new EmotionDescriptor(
                    label: "neutral",
                    aliases: Array.Empty<string>(),
                    complements: Array.Empty<string>(),
                    defaultMouthInfluence: 0f,
                    isNeutral: true);
                _descriptors.Insert(0, _neutral);
                _byLabel[_neutral.Label] = _neutral;
            }

            _built = true;
        }

        private void OnValidate()
        {
            _built = false;

            if (entries == null || entries.Count == 0) return;

            int neutralCount = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                EmotionTaxonomyEntry entry = entries[i];
                if (entry != null && entry.IsNeutral) neutralCount++;
            }

            if (neutralCount == 0)
                Debug.LogWarning(
                    "[EmotionTaxonomyAsset] No entry has IsNeutral set. A synthetic 'neutral' will be used at runtime; mark one entry as the neutral baseline.",
                    this);
            else if (neutralCount > 1)
                Debug.LogWarning(
                    $"[EmotionTaxonomyAsset] {neutralCount} entries are marked IsNeutral; only the first will be used. Mark exactly one neutral baseline.",
                    this);
        }

        /// <summary>Creates the default Plutchik-style taxonomy used when no asset is wired up.</summary>
        public static EmotionTaxonomyAsset CreateDefault()
        {
            EmotionTaxonomyAsset instance = CreateInstance<EmotionTaxonomyAsset>();
            instance.hideFlags = HideFlags.HideAndDontSave;
            instance.entries = new List<EmotionTaxonomyEntry>
            {
                new("neutral",  new[] { "calm", "idle" },                new string[] { }, 0f,    true),
                new("joy",      new[] { "happy", "happiness", "ecstasy", "serenity", "excited", "enthusiastic" },      new[] { "trust" }, 0.6f, false),
                new("trust",    new[] { "acceptance", "admiration", "confident", "reassured" },     new[] { "joy" },  0.3f, false),
                new("fear",     new[] { "afraid", "apprehension", "terror", "fearful", "worried", "anxious", "nervous" },     new string[] { }, 0.4f, false),
                new("surprise", new[] { "amazement", "distraction", "surprised" },           new string[] { }, 0.5f, false),
                new("sadness",  new[] { "sad", "pensiveness", "grief" },                    new string[] { }, 0.3f, false),
                new("disgust",  new[] { "disgusted", "loathing", "boredom", "bored" },      new string[] { }, 0.4f, false),
                new("anger",    new[] { "angry", "annoyance", "rage" },                     new string[] { }, 0.55f, false),
                new("anticipation", new[] { "interest", "vigilance", "curious", "curiosity", "eager", "hopeful" },                      new string[] { }, 0.45f, false),
            };
            instance._built = false;
            return instance;
        }
    }
}
