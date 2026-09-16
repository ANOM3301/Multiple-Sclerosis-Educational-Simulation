/// <summary>
///     ConvaiManager partial: SDK event subscription forwarding.
///     Events are managed by the ConvaiEvents facade (created by ConvaiRuntimeHost).
/// </summary>

using Convai.Domain.DomainEvents.Session;
using Convai.Runtime.Facades;

namespace Convai.Runtime.Components
{
    public partial class ConvaiManager
    {
        private bool _facadeEventsSubscribed;

        /// <summary>
        ///     Subscribes to the ConvaiEvents facade events to forward them to public events.
        /// </summary>
        internal void SubscribeToFacadeEvents()
        {
            if (_facadeEventsSubscribed) return;

            ConvaiEvents events = _host?.Events;
            if (events == null) return;

            events.OnConnected += HandleRoomConnected;
            events.OnDisconnected += HandleRoomDisconnected;
            events.OnSessionError += HandleSessionError;

            _facadeEventsSubscribed = true;
        }

        internal void UnsubscribeFromFacadeEvents()
        {
            if (!_facadeEventsSubscribed) return;

            ConvaiEvents events = _host?.Events;
            if (events == null) return;

            events.OnConnected -= HandleRoomConnected;
            events.OnDisconnected -= HandleRoomDisconnected;
            events.OnSessionError -= HandleSessionError;

            _facadeEventsSubscribed = false;
        }

        private void HandleRoomConnected()
        {
            RefreshOwnedAgentState();
            UpdateWebGLVoiceStartArmState();
            OnConnected?.Invoke();
        }

        private void HandleRoomDisconnected()
        {
            _webGLVoiceStartArmed = false;
            OnDisconnected?.Invoke();
        }

        private void HandleSessionError(SessionError error) => OnError?.Invoke(error);
    }
}
