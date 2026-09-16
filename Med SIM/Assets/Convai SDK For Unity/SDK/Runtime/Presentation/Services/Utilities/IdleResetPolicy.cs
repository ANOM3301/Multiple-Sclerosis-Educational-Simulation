using System;
using Convai.Domain.DomainEvents.Session;

namespace Convai.Runtime.Presentation.Services.Utilities
{
    /// <summary>
    ///     Pure state machine that decides when UI activity should trigger reset-idle-timer.
    /// </summary>
    public sealed class IdleResetPolicy
    {
        private readonly TimeSpan _retryCooldown;

        public IdleResetPolicy(TimeSpan? retryCooldown = null)
        {
            _retryCooldown = retryCooldown ?? TimeSpan.FromSeconds(3);
        }

        public SessionState CurrentSessionState { get; private set; } = SessionState.Disconnected;

        public bool IsWarningActive { get; private set; }

        public bool IsVoiceActive { get; private set; }

        public DateTime LastResetAttemptUtc { get; private set; } = DateTime.MinValue;

        public bool ShouldMonitorUi =>
            CurrentSessionState == SessionState.Connected && IsWarningActive && !IsVoiceActive;

        public void HandleSessionStateChanged(SessionState newState)
        {
            if (newState != CurrentSessionState)
                ClearTransientState();

            CurrentSessionState = newState;
        }

        public void HandleIdleWarning()
        {
            if (CurrentSessionState != SessionState.Connected)
            {
                ClearTransientState();
                return;
            }

            IsWarningActive = true;
        }

        public void HandlePlayerSpeakingStateChanged(bool isSpeaking)
        {
            IsVoiceActive = isSpeaking;

            if (isSpeaking)
                IsWarningActive = false;
        }

        public void HandleOutboundRtviMessage(string messageType)
        {
            if (IsServerVisibleActivity(messageType))
                IsWarningActive = false;
        }

        public bool TryBeginResetAttempt(DateTime nowUtc)
        {
            if (!ShouldMonitorUi)
                return false;

            if (LastResetAttemptUtc != DateTime.MinValue &&
                nowUtc - LastResetAttemptUtc < _retryCooldown)
                return false;

            LastResetAttemptUtc = nowUtc;
            return true;
        }

        public void HandleResetResult(bool wasSent)
        {
            if (wasSent)
                IsWarningActive = false;
        }

        public void ClearTransientState()
        {
            IsWarningActive = false;
            IsVoiceActive = false;
            LastResetAttemptUtc = DateTime.MinValue;
        }

        public static bool IsServerVisibleActivity(string messageType)
        {
            switch (messageType ?? string.Empty)
            {
                case "user_text_message":
                case "trigger-message":
                case "context-update":
                    return true;
                default:
                    return false;
            }
        }
    }
}
