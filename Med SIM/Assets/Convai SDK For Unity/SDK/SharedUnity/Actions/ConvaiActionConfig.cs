using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Convai.Shared.Actions
{
    /// <summary>
    ///     Connect-time action affordances for the current session.
    /// </summary>
    [Serializable]
    public sealed class ConvaiActionConfig
    {
        [field: SerializeField]
        [JsonProperty("actions", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> Actions { get; set; } = new();

        [field: SerializeField]
        [JsonProperty("characters", NullValueHandling = NullValueHandling.Ignore)]
        public List<ConvaiActionCharacterDefinition> Characters { get; set; } = new();

        [field: SerializeField]
        [JsonProperty("objects", NullValueHandling = NullValueHandling.Ignore)]
        public List<ConvaiActionObjectDefinition> Objects { get; set; } = new();

        [field: SerializeField]
        [JsonProperty("current_attention_object", NullValueHandling = NullValueHandling.Ignore)]
        public string CurrentAttentionObject { get; set; }

        public ConvaiActionConfig Clone() =>
            new()
            {
                Actions = Actions == null ? new List<string>() : new List<string>(Actions),
                Characters = CloneCharacters(Characters),
                Objects = CloneObjects(Objects),
                CurrentAttentionObject = CurrentAttentionObject
            };

        private static List<ConvaiActionCharacterDefinition> CloneCharacters(
            IReadOnlyList<ConvaiActionCharacterDefinition> characters)
        {
            var clone = new List<ConvaiActionCharacterDefinition>();
            if (characters == null)
                return clone;

            foreach (ConvaiActionCharacterDefinition character in characters)
                clone.Add(character?.Clone() ?? new ConvaiActionCharacterDefinition());

            return clone;
        }

        private static List<ConvaiActionObjectDefinition> CloneObjects(
            IReadOnlyList<ConvaiActionObjectDefinition> objects)
        {
            var clone = new List<ConvaiActionObjectDefinition>();
            if (objects == null)
                return clone;

            foreach (ConvaiActionObjectDefinition actionObject in objects)
                clone.Add(actionObject?.Clone() ?? new ConvaiActionObjectDefinition());

            return clone;
        }
    }

    /// <summary>
    ///     Runtime patch for the active session's action affordances. A null list is omitted and
    ///     preserves the current value; an empty list explicitly clears that request-level list.
    /// </summary>
    [Serializable]
    public sealed class ConvaiActionConfigPatch
    {
        /// <summary>Replacement action list. Null preserves; empty clears.</summary>
        [JsonProperty("actions", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> Actions { get; set; }

        /// <summary>Replacement character-target list. Null preserves; empty clears.</summary>
        [JsonProperty("characters", NullValueHandling = NullValueHandling.Ignore)]
        public List<ConvaiActionCharacterDefinition> Characters { get; set; }

        /// <summary>Replacement object-target list. Null preserves; empty clears.</summary>
        [JsonProperty("objects", NullValueHandling = NullValueHandling.Ignore)]
        public List<ConvaiActionObjectDefinition> Objects { get; set; }

        /// <summary>
        ///     Optional attention update resolved after list replacement. Null preserves; empty clears.
        /// </summary>
        [JsonProperty("current_attention_object", NullValueHandling = NullValueHandling.Ignore)]
        public string CurrentAttentionObject { get; set; }

        /// <summary>Creates a deep copy while preserving omitted-versus-empty list semantics.</summary>
        public ConvaiActionConfigPatch Clone() =>
            new()
            {
                Actions = Actions == null ? null : new List<string>(Actions),
                Characters = CloneCharacters(Characters),
                Objects = CloneObjects(Objects),
                CurrentAttentionObject = CurrentAttentionObject
            };

        private static List<ConvaiActionCharacterDefinition> CloneCharacters(
            IReadOnlyList<ConvaiActionCharacterDefinition> characters)
        {
            if (characters == null)
                return null;

            var clone = new List<ConvaiActionCharacterDefinition>(characters.Count);
            for (int i = 0; i < characters.Count; i++)
                clone.Add(characters[i]?.Clone());

            return clone;
        }

        private static List<ConvaiActionObjectDefinition> CloneObjects(
            IReadOnlyList<ConvaiActionObjectDefinition> objects)
        {
            if (objects == null)
                return null;

            var clone = new List<ConvaiActionObjectDefinition>(objects.Count);
            for (int i = 0; i < objects.Count; i++)
                clone.Add(objects[i]?.Clone());

            return clone;
        }
    }

    /// <summary>
    ///     Explicit action target object available to the backend for grounding.
    /// </summary>
    [Serializable]
    public sealed class ConvaiActionObjectDefinition
    {
        [field: SerializeField] [JsonProperty("name")] public string Name { get; set; }
        [field: SerializeField] [JsonProperty("description")] public string Description { get; set; }

        [field: SerializeField]
        [JsonIgnore]
        public GameObject GameObjectReference { get; set; }

        public ConvaiActionObjectDefinition Clone() =>
            new()
            {
                Name = Name,
                Description = Description,
                GameObjectReference = GameObjectReference
            };
    }

    /// <summary>
    ///     Explicit action target character available to the backend for grounding.
    /// </summary>
    [Serializable]
    public sealed class ConvaiActionCharacterDefinition
    {
        [field: SerializeField] [JsonProperty("name")] public string Name { get; set; }
        [field: SerializeField] [JsonProperty("bio")] public string Bio { get; set; }

        [field: SerializeField]
        [JsonIgnore]
        public GameObject GameObjectReference { get; set; }

        public ConvaiActionCharacterDefinition Clone() =>
            new()
            {
                Name = Name,
                Bio = Bio,
                GameObjectReference = GameObjectReference
            };
    }
}
