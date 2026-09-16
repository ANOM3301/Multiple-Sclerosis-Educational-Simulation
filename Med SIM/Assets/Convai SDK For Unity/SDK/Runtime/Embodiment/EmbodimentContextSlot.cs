using System;
using Convai.Domain.Logging;
using Convai.Runtime.Logging;
using UnityEngine;
using ILogger = Convai.Domain.Logging.ILogger;

namespace Convai.Runtime.Embodiment
{
    /// <summary>
    ///     Single-writer slot for a source registration owned by
    ///     <see cref="EmbodimentContext" />. Encapsulates the duplicate-source
    ///     warning, change notification, and exception-safe event dispatch shared by
    ///     every per-character source the context exposes.
    /// </summary>
    /// <typeparam name="T">Source contract type (interface or class).</typeparam>
    internal sealed class EmbodimentContextSlot<T> where T : class
    {
        private readonly string _role;
        private EmbodimentContext _ownerContext;
        private T _current;

        public EmbodimentContextSlot(string role)
        {
            if (string.IsNullOrEmpty(role)) throw new ArgumentException("Role must be non-empty.", nameof(role));
            _role = role;
        }

        /// <summary>
        ///     Binds this slot to its owning context so duplicate-register warnings and subscriber
        ///     exceptions can use injected <see cref="ILogger" /> when available.
        /// </summary>
        internal void EnsureAttached(EmbodimentContext owner)
        {
            if (owner != null && _ownerContext == null)
                _ownerContext = owner;
        }

        /// <summary>The currently registered source, or <c>null</c> when none.</summary>
        public T Current => _current;

        /// <summary>Raised after a successful register or unregister with the new value (or <c>null</c>).</summary>
        public event Action<T> Changed;

        /// <summary>
        ///     Registers <paramref name="candidate" /> as the active source. Returns <c>true</c>
        ///     when the slot transitions; <c>false</c> when the candidate is null, identical to
        ///     the current source, or rejected because another source is already registered.
        /// </summary>
        public bool TryRegister(T candidate)
        {
            if (candidate == null) return false;
            if (ReferenceEquals(_current, candidate)) return false;

            if (_current != null)
            {
                string message =
                    $"[EmbodimentContext] Duplicate {_role} '{Describe(candidate)}' ignored. " +
                    $"'{Describe(_current)}' is already registered on this character.";
                LogWarning(message);
                return false;
            }

            _current = candidate;
            Raise(candidate);
            return true;
        }

        /// <summary>
        ///     Releases the slot when <paramref name="candidate" /> matches the current source.
        ///     Returns <c>true</c> on release; <c>false</c> when the slot was already empty or
        ///     held a different source.
        /// </summary>
        public bool Unregister(T candidate)
        {
            if (candidate == null) return false;
            if (!ReferenceEquals(_current, candidate)) return false;

            _current = null;
            Raise(null);
            return true;
        }

        private void Raise(T value)
        {
            Action<T> handler = Changed;
            if (handler == null) return;

            try
            {
                handler.Invoke(value);
            }
            catch (Exception ex)
            {
                LogSubscriberException(ex);
            }
        }

        private void LogWarning(string message)
        {
            ILogger logger = _ownerContext?.Logger;
            if (logger != null)
            {
                logger.Warning(message, LogCategory.Character);
                return;
            }

            ConvaiLogger.Warning(message, LogCategory.Character);
            if (ConvaiLogger.SinkCount == 0)
                Debug.LogWarning(message);
        }

        private void LogSubscriberException(Exception ex)
        {
            const string message = "[EmbodimentContext] A subscriber threw while handling a source change notification.";
            ILogger logger = _ownerContext?.Logger;
            if (logger != null)
            {
                logger.Error(ex, message, LogCategory.Character);
                return;
            }

            ConvaiLogger.Exception(ex, LogCategory.Character);
            if (ConvaiLogger.SinkCount == 0)
            {
                Debug.LogError($"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static string Describe(object value)
        {
            if (value is Component component) return component.name;
            if (value is UnityEngine.Object unityObject) return unityObject.name;
            return value != null ? value.GetType().Name : "<null>";
        }
    }
}
