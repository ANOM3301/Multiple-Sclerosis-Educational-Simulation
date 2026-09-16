using System;
using System.Collections.Generic;
using Convai.Runtime.Components;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Convai.Editor.AI
{
    public static partial class ConvaiMcpTools
    {
        private static object Success(string message, object data) => ConvaiMcpResponses.Success(message, data);

        private static object Failure(string code, string message, object data) =>
            ConvaiMcpResponses.Failure(code, message, data);

        private static object Result(bool success, string message, object data) =>
            ConvaiMcpResponses.Envelope(success, message, data);

        private static object StandardResponseSchema() => ConvaiMcpResponses.StandardResponseSchema();

        private static object EmptyInputSchema() => new
        {
            type = "object",
            properties = new { },
            additionalProperties = false
        };

        private static object BooleanInputSchema(string propertyName, string description, bool defaultValue) => new
        {
            type = "object",
            properties = new Dictionary<string, object>
            {
                [propertyName] = new
                {
                    type = "boolean",
                    description,
                    @default = defaultValue
                }
            },
            additionalProperties = false
        };

        private static object EnumInputSchema<T>(string propertyName, string description, T defaultValue)
            where T : struct, Enum => new
        {
            type = "object",
            properties = new Dictionary<string, object>
            {
                [propertyName] = new
                {
                    type = "string",
                    description,
                    @enum = Enum.GetNames(typeof(T)),
                    @default = defaultValue.ToString()
                }
            },
            additionalProperties = false
        };

        private static T Parse<T>(JObject parameters) where T : class, new() =>
            parameters?.ToObject<T>() ?? new T();

        private static T FindFirst<T>() where T : UnityEngine.Object
        {
            T[] values = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include);
            return values.Length > 0 ? values[0] : null;
        }

        private static object[] DescribeComponents<T>(T[] components) where T : Component
        {
            var descriptions = new object[components.Length];
            for (int i = 0; i < components.Length; i++)
            {
                T component = components[i];
                descriptions[i] = new
                {
                    instanceId = ConvaiMcpEntityRef.ToToolId(component.gameObject),
                    componentInstanceId = ConvaiMcpEntityRef.ToToolId(component),
                    name = component.gameObject.name,
                    activeInHierarchy = component.gameObject.activeInHierarchy,
                    scene = component.gameObject.scene.name
                };
            }

            return descriptions;
        }

        private static object[] DescribePlayers(ConvaiPlayer[] players)
        {
            var descriptions = new object[players.Length];
            for (int i = 0; i < players.Length; i++)
            {
                ConvaiPlayer player = players[i];
                descriptions[i] = new
                {
                    instanceId = ConvaiMcpEntityRef.ToToolId(player.gameObject),
                    componentInstanceId = ConvaiMcpEntityRef.ToToolId(player),
                    name = player.gameObject.name,
                    playerName = player.PlayerName,
                    activeInHierarchy = player.gameObject.activeInHierarchy,
                    scene = player.gameObject.scene.name
                };
            }

            return descriptions;
        }

        private static object[] DescribeCharacters(ConvaiCharacter[] characters)
        {
            var descriptions = new object[characters.Length];
            for (int i = 0; i < characters.Length; i++)
            {
                ConvaiCharacter character = characters[i];
                descriptions[i] = new
                {
                    instanceId = ConvaiMcpEntityRef.ToToolId(character.gameObject),
                    componentInstanceId = ConvaiMcpEntityRef.ToToolId(character),
                    name = character.gameObject.name,
                    characterId = character.CharacterId,
                    activeInHierarchy = character.gameObject.activeInHierarchy,
                    scene = character.gameObject.scene.name
                };
            }

            return descriptions;
        }

        private static void AddUnique(List<string> destination, IEnumerable<string> source)
        {
            foreach (string value in source)
            {
                if (!destination.Contains(value)) destination.Add(value);
            }
        }

        private static string TryGetPackageRoot() =>
            ConvaiSceneSetupApi.TryGetConvaiSdkPackageRoot(out string packageRoot)
                ? packageRoot
                : string.Empty;
    }
}
