#nullable enable

using Newtonsoft.Json;

namespace Convai.Infrastructure.Protocol.Messages
{
    /// <summary>Inbound bot emotion payload deserialized from server-message with type "bot-emotion".</summary>
    public sealed class RTVIBotEmotionMessage
    {
        /// <summary>Gets or sets the emotion label.</summary>
        [JsonProperty("emotion")]
        public string? Emotion { get; set; }

        /// <summary>Gets or sets the emotion intensity scale (1-3).</summary>
        [JsonProperty("scale")]
        public int Scale { get; set; }
    }
}
