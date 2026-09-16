#nullable enable

using System.Collections.Generic;
using Convai.Shared.Types;
using Newtonsoft.Json;

namespace Convai.Infrastructure.Protocol.Messages
{
    /// <summary>Payload for ordered structured backend actions returned for the current turn.</summary>
    public sealed class ActionResponsePayload
    {
        /// <summary>Ordered action commands. No-action turns arrive as an empty array.</summary>
        [JsonProperty("actions")]
        public List<ConvaiActionCommand>? Actions { get; set; }
    }
}
