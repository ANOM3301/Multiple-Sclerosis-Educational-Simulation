using System;
using System.Collections.Generic;

namespace Convai.Runtime.Actions
{
    /// <summary>
    ///     Privacy-safe summary of one action-response filter pass. Used by editor diagnostics;
    ///     intentionally excludes the raw backend action payload.
    /// </summary>
    internal readonly struct ConvaiActionResponseFilterDiagnostic
    {
        private ConvaiActionResponseFilterDiagnostic(
            string characterId,
            string participantId,
            int receivedCount,
            int acceptedCount,
            int rejectedCount,
            IReadOnlyDictionary<string, int> rejectedByReason,
            DateTime timestamp)
        {
            CharacterId = characterId ?? string.Empty;
            ParticipantId = participantId ?? string.Empty;
            ReceivedCount = receivedCount;
            AcceptedCount = acceptedCount;
            RejectedCount = rejectedCount;
            RejectedByReason = rejectedByReason ?? new Dictionary<string, int>();
            Timestamp = timestamp;
        }

        public string CharacterId { get; }
        public string ParticipantId { get; }
        public int ReceivedCount { get; }
        public int AcceptedCount { get; }
        public int RejectedCount { get; }
        public IReadOnlyDictionary<string, int> RejectedByReason { get; }
        public DateTime Timestamp { get; }

        public static ConvaiActionResponseFilterDiagnostic Create(
            string characterId,
            string participantId,
            int acceptedCount,
            int rejectedCount,
            IReadOnlyDictionary<string, int> rejectedByReason) =>
            new(
                characterId,
                participantId,
                acceptedCount + rejectedCount,
                acceptedCount,
                rejectedCount,
                rejectedByReason == null
                    ? new Dictionary<string, int>()
                    : new Dictionary<string, int>(rejectedByReason),
                DateTime.UtcNow);
    }

    /// <summary>Read-only editor diagnostic for one pending runtime action-state update.</summary>
    internal readonly struct ConvaiRuntimeActionUpdateDebugInfo
    {
        public ConvaiRuntimeActionUpdateDebugInfo(
            string updateId,
            DateTime sentAtUtc,
            bool mutatesActionConfig,
            bool mutatesTopLevelAttention,
            bool hasAcknowledgement,
            string acknowledgementStatus)
        {
            UpdateId = updateId ?? string.Empty;
            SentAtUtc = sentAtUtc;
            MutatesActionConfig = mutatesActionConfig;
            MutatesTopLevelAttention = mutatesTopLevelAttention;
            HasAcknowledgement = hasAcknowledgement;
            AcknowledgementStatus = acknowledgementStatus ?? string.Empty;
        }

        public string UpdateId { get; }
        public DateTime SentAtUtc { get; }
        public bool MutatesActionConfig { get; }
        public bool MutatesTopLevelAttention { get; }
        public bool HasAcknowledgement { get; }
        public string AcknowledgementStatus { get; }
    }
}
