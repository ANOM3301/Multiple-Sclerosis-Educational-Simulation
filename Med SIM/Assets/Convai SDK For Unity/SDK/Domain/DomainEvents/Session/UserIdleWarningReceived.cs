using System;

namespace Convai.Domain.DomainEvents.Session
{
    /// <summary>
    ///     Domain event raised when backend sends a user idle warning before inactivity disconnect.
    /// </summary>
    public readonly struct UserIdleWarningReceived
    {
        /// <summary>Seconds remaining before idle disconnection.</summary>
        public int RemainingSeconds { get; }

        /// <summary>Optional user-facing warning message.</summary>
        public string Message { get; }

        /// <summary>When the warning was received (UTC).</summary>
        public DateTime Timestamp { get; }

        /// <summary>Creates a new UserIdleWarningReceived event.</summary>
        public UserIdleWarningReceived(int remainingSeconds, string message, DateTime timestamp)
        {
            RemainingSeconds = Math.Max(0, remainingSeconds);
            Message = message ?? string.Empty;
            Timestamp = timestamp;
        }

        /// <summary>Creates a UserIdleWarningReceived event with the current UTC timestamp.</summary>
        public static UserIdleWarningReceived Create(int remainingSeconds, string message) =>
            new(remainingSeconds, message, DateTime.UtcNow);
    }
}
