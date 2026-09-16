using System;
using System.Collections.Generic;
using Convai.Shared.Actions;
using Convai.Shared.Types;
using UnityEngine;

namespace Convai.Runtime.Actions
{
    /// <summary>
    ///     An action target resolved against the authored <see cref="ConvaiActionConfig" />
    ///     objects and characters (exact name match, case-insensitive).
    /// </summary>
    [Serializable]
    public sealed class ConvaiResolvedActionTarget
    {
        /// <summary>Whether the target is an object or a character.</summary>
        public ConvaiActionTargetKind Kind { get; private set; }

        /// <summary>Authored name of the resolved target.</summary>
        public string Name { get; private set; }

        /// <summary>Authored object binding; null unless <see cref="Kind" /> is Object.</summary>
        public ConvaiActionObjectDefinition ObjectBinding { get; private set; }

        /// <summary>Authored character binding; null unless <see cref="Kind" /> is Character.</summary>
        public ConvaiActionCharacterDefinition CharacterBinding { get; private set; }

        /// <summary>Scene object of the resolved binding, when assigned by the author.</summary>
        public GameObject GameObjectReference => Kind switch
        {
            ConvaiActionTargetKind.Object => ObjectBinding?.GameObjectReference,
            ConvaiActionTargetKind.Character => CharacterBinding?.GameObjectReference,
            _ => null
        };

        internal static ConvaiResolvedActionTarget FromObject(ConvaiActionObjectDefinition actionObject) =>
            new()
            {
                Kind = ConvaiActionTargetKind.Object,
                Name = actionObject?.Name ?? string.Empty,
                ObjectBinding = actionObject
            };

        internal static ConvaiResolvedActionTarget FromCharacter(ConvaiActionCharacterDefinition character) =>
            new()
            {
                Kind = ConvaiActionTargetKind.Character,
                Name = character?.Name ?? string.Empty,
                CharacterBinding = character
            };

        internal static ConvaiResolvedActionTarget Resolve(
            string targetName,
            ConvaiActionConfig actionConfig,
            ConvaiActionTargetRequirement? targetRequirement)
        {
            if (string.IsNullOrWhiteSpace(targetName) || actionConfig == null)
                return null;

            ConvaiActionTargetLookup lookup = ConvaiActionTargetLookup.FromConfig(actionConfig);
            if (targetRequirement == ConvaiActionTargetRequirement.Character)
                return lookup.ResolveCharacter(targetName) ?? lookup.ResolveObject(targetName);

            if (targetRequirement == ConvaiActionTargetRequirement.Object)
                return lookup.ResolveObject(targetName) ?? lookup.ResolveCharacter(targetName);

            return lookup.ResolveObject(targetName) ?? lookup.ResolveCharacter(targetName);
        }

        internal static ConvaiResolvedActionTarget Resolve(
            string targetName,
            ConvaiActionConfig actionConfig,
            ConvaiActionTargetKind? targetKind)
        {
            if (string.IsNullOrWhiteSpace(targetName) || actionConfig == null)
                return null;

            return targetKind switch
            {
                ConvaiActionTargetKind.Object => ResolveObject(targetName, actionConfig),
                ConvaiActionTargetKind.Character => ResolveCharacter(targetName, actionConfig),
                _ => ResolveObject(targetName, actionConfig) ?? ResolveCharacter(targetName, actionConfig)
            };
        }

        private static ConvaiResolvedActionTarget ResolveObject(string targetName, ConvaiActionConfig actionConfig) =>
            ConvaiActionTargetLookup.FromConfig(actionConfig).ResolveObject(targetName);

        private static ConvaiResolvedActionTarget ResolveCharacter(string targetName, ConvaiActionConfig actionConfig) =>
            ConvaiActionTargetLookup.FromConfig(actionConfig).ResolveCharacter(targetName);

        private sealed class ConvaiActionTargetLookup
        {
            private readonly Dictionary<string, ConvaiActionObjectDefinition> _objects;
            private readonly Dictionary<string, ConvaiActionCharacterDefinition> _characters;

            private ConvaiActionTargetLookup(
                Dictionary<string, ConvaiActionObjectDefinition> objects,
                Dictionary<string, ConvaiActionCharacterDefinition> characters)
            {
                _objects = objects;
                _characters = characters;
            }

            public static ConvaiActionTargetLookup FromConfig(ConvaiActionConfig actionConfig)
            {
                var objects = new Dictionary<string, ConvaiActionObjectDefinition>(StringComparer.OrdinalIgnoreCase);
                var characters =
                    new Dictionary<string, ConvaiActionCharacterDefinition>(StringComparer.OrdinalIgnoreCase);

                IReadOnlyList<ConvaiActionObjectDefinition> objectDefinitions = actionConfig.Objects;
                if (objectDefinitions != null)
                {
                    for (int i = 0; i < objectDefinitions.Count; i++)
                    {
                        ConvaiActionObjectDefinition actionObject = objectDefinitions[i];
                        string name = NormalizeName(actionObject?.Name);
                        if (string.IsNullOrEmpty(name) || objects.ContainsKey(name))
                            continue;

                        objects[name] = actionObject;
                    }
                }

                IReadOnlyList<ConvaiActionCharacterDefinition> characterDefinitions = actionConfig.Characters;
                if (characterDefinitions != null)
                {
                    for (int i = 0; i < characterDefinitions.Count; i++)
                    {
                        ConvaiActionCharacterDefinition character = characterDefinitions[i];
                        string name = NormalizeName(character?.Name);
                        if (string.IsNullOrEmpty(name) || characters.ContainsKey(name))
                            continue;

                        characters[name] = character;
                    }
                }

                return new ConvaiActionTargetLookup(objects, characters);
            }

            public ConvaiResolvedActionTarget ResolveObject(string targetName)
            {
                string name = NormalizeName(targetName);
                if (name.Length == 0 || !_objects.TryGetValue(name, out ConvaiActionObjectDefinition actionObject))
                    return null;

                return FromObject(actionObject);
            }

            public ConvaiResolvedActionTarget ResolveCharacter(string targetName)
            {
                string name = NormalizeName(targetName);
                if (name.Length == 0 ||
                    !_characters.TryGetValue(name, out ConvaiActionCharacterDefinition character))
                    return null;

                return FromCharacter(character);
            }

            private static string NormalizeName(string name) =>
                string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();
        }

        internal static Dictionary<string, GameObject> BuildObjectEntityLookup(ConvaiActionConfig actionConfig)
        {
            var lookup = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<ConvaiActionObjectDefinition> objects = actionConfig?.Objects;
            if (objects == null)
                return lookup;

            for (int i = 0; i < objects.Count; i++)
            {
                ConvaiActionObjectDefinition actionObject = objects[i];
                string name = string.IsNullOrWhiteSpace(actionObject?.Name) ? string.Empty : actionObject.Name.Trim();
                if (name.Length == 0 || lookup.ContainsKey(name))
                    continue;

                GameObject entity = actionObject.GameObjectReference;
                if (entity == null)
                    continue;

                lookup[name] = entity;
            }

            return lookup;
        }

        internal static Dictionary<string, GameObject> BuildCharacterEntityLookup(ConvaiActionConfig actionConfig)
        {
            var lookup = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<ConvaiActionCharacterDefinition> characters = actionConfig?.Characters;
            if (characters == null)
                return lookup;

            for (int i = 0; i < characters.Count; i++)
            {
                ConvaiActionCharacterDefinition character = characters[i];
                string name = string.IsNullOrWhiteSpace(character?.Name) ? string.Empty : character.Name.Trim();
                if (name.Length == 0 || lookup.ContainsKey(name))
                    continue;

                GameObject entity = character.GameObjectReference;
                if (entity == null)
                    continue;

                lookup[name] = entity;
            }

            return lookup;
        }
    }
}
