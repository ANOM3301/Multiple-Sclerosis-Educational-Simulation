using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Convai.Domain.EventSystem;
using Convai.Domain.Identity;
using Convai.Domain.Logging;
using Convai.Infrastructure.Networking.Transport;
using Convai.Runtime.Adapters.Networking;
using Convai.Runtime.Behaviors;
using Convai.Runtime.Core;
using Convai.Runtime.Core.Async;
using Convai.Runtime.Core.Configuration;
using Convai.Runtime.Core.Coordinators;
using Convai.Runtime.Core.Modules;
using Convai.Runtime.Core.Providers;
using Convai.Runtime.Core.Registry;
using Convai.Runtime.Logging;
using UnityEngine;
using ILogger = Convai.Domain.Logging.ILogger;

namespace Convai.Runtime.Components
{
    /// <summary>
    ///     Deferred room runtime that delegates to the actual room runtime from <see cref="ConvaiRoomManager" />.
    /// </summary>
    internal sealed class DeferredRoomRuntime : IRoomRuntime
    {
        private readonly ConvaiManager _manager;

        public DeferredRoomRuntime(ConvaiManager manager)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        }

        private IRoomRuntime Underlying
        {
            get
            {
                if (!_manager.TryGetRoomManager(out ConvaiRoomManager roomManager))
                    throw new InvalidOperationException("Room runtime accessed before ConvaiRoomManager is available.");

                IRoomRuntime runtime = roomManager.GetRoomRuntime();
                if (runtime == null)
                    throw new InvalidOperationException("Room runtime not initialized.");

                return runtime;
            }
        }

        public bool IsActive => Underlying.IsActive;
        public RoomSession Session => Underlying.Session;
        public IRoomConnectionCoordinator Connection => Underlying.Connection;
        public IRoomAudioCoordinator Audio => Underlying.Audio;
        public IRoomOwnershipCoordinator Ownership => Underlying.Ownership;
        public IRoomDiagnostics Diagnostics => Underlying.Diagnostics;

        public IConvaiOperation<RoomSession> ConnectAsync(CancellationToken ct = default) =>
            Underlying.ConnectAsync(ct);

        public IConvaiOperation<Unit> DisconnectAsync(CancellationToken ct = default) =>
            Underlying.DisconnectAsync(ct);

        public void Initialize(RoomSession session) => Underlying.Initialize(session);
        public void Shutdown() => Underlying.Shutdown();
    }

    /// <summary>
    ///     ConvaiManager partial: ConvaiRuntimeBuilder integration.
    /// </summary>
    public partial class ConvaiManager
    {
        private IEndUserIdentityProvider _endUserIdentityProvider;
        private IEndUserMetadataProvider _endUserMetadataProvider;
        private UnityConvaiAdapter _unityAdapter;

        public ConvaiRuntime ConvaiRuntime { get; private set; }

        public bool IsUsingRuntimeBuilder => ConvaiRuntime != null;

        protected virtual ConvaiRuntimeBuilder CreateRuntimeBuilder()
        {
            var builder = new ConvaiRuntimeBuilder();

            ConvaiLogger.Initialize();
            ILogger logger = new ConvaiLogger();
            builder.UseLogger(logger);

            IEventHub eventHub = new EventHub(UnityScheduler.Instance, logger);
            builder.UseEventHub(eventHub);

            IAgentRegistry agentRegistry = new AgentRegistry();
            builder.UseAgentRegistry(agentRegistry);

            builder.UseRoomRuntime(() => new DeferredRoomRuntime(this));

            ITransportProvider transportProvider = GetPlatformTransportProvider();
            if (transportProvider != null)
                builder.UseTransport(transportProvider);

            IConversationProvider conversationProvider = GetConversationProvider();
            if (conversationProvider != null)
                builder.UseConversation(conversationProvider);

            if (_endUserIdentityProvider != null)
                builder.WithEndUserIdentityProvider(_endUserIdentityProvider);

            if (_endUserMetadataProvider != null)
                builder.WithEndUserMetadataProvider(_endUserMetadataProvider);

            ConvaiSettings settings = ConvaiSettings.Instance;
            if (settings != null)
            {
                builder.WithRuntimePreferences(new RuntimePreferences
                {
                    TranscriptEnabled = settings.TranscriptSystemEnabled,
                    NotificationsEnabled = settings.NotificationSystemEnabled,
                    CharacterAudioVolume = settings.CharacterAudioVolume,
                    AudioFeedbackEnabled = settings.AudioFeedbackEnabled,
                    PreferredMicrophoneDeviceId = settings.DefaultMicrophoneDeviceId
                });
            }

            return builder;
        }

        protected virtual ConvaiRuntime BuildRuntime()
        {
            if (ConvaiRuntime != null)
            {
                if (_debugLogging)
                    ConvaiLogger.Warning("Runtime already built.", LogCategory.Bootstrap);
                return ConvaiRuntime;
            }

            ConvaiRuntimeBuilder builder = CreateRuntimeBuilder();
            ConvaiRuntime = builder.Build();

            if (_debugLogging)
                ConvaiLogger.Debug("ConvaiRuntime built via builder.", LogCategory.Bootstrap);

            return ConvaiRuntime;
        }

        protected virtual void InitializeUnityAdapter()
        {
            if (ConvaiRuntime == null)
            {
                ConvaiLogger.Warning("Cannot initialize adapter: runtime not built.",
                    LogCategory.Bootstrap);
                return;
            }

            _unityAdapter = GetComponent<UnityConvaiAdapter>() ?? gameObject.AddComponent<UnityConvaiAdapter>();
            _unityAdapter.Initialize(ConvaiRuntime);

            if (_debugLogging)
                ConvaiLogger.Debug("UnityConvaiAdapter initialized.", LogCategory.Bootstrap);
        }

        public async void StartRuntimeAsync()
        {
            if (_unityAdapter == null)
            {
                ConvaiLogger.Warning("Cannot start: adapter not initialized.", LogCategory.Bootstrap);
                return;
            }

            await _unityAdapter.StartRuntimeAsync();

            // Subscribe to facade events after runtime starts (events now flowing)
            _host?.EnsureFacades(_roomManager);
            SubscribeToFacadeEvents();

            if (_debugLogging)
                ConvaiLogger.Debug("ConvaiRuntime started.", LogCategory.Bootstrap);
        }

        private void DiscoverAndAddModules()
        {
            if (ConvaiRuntime == null || _host == null) return;

            IReadOnlyList<IConvaiModule> modules = _host.RegisteredModules;
            if (modules.Count == 0) return;

            ConvaiRuntime.AddModules(modules);

            if (_debugLogging)
                ConvaiLogger.Debug($"Added {modules.Count} module(s) to runtime.",
                    LogCategory.Bootstrap);
        }

        private void DisposeBuilderRuntime()
        {
            if (ConvaiRuntime != null)
            {
                try
                {
                    ConvaiRuntime.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(5));
                }
                catch (Exception ex)
                {
                    ConvaiLogger.Error($"Error disposing runtime: {ex.Message}", LogCategory.Bootstrap);
                }

                ConvaiRuntime = null;
            }

            _unityAdapter = null;
        }

        protected virtual ITransportProvider GetPlatformTransportProvider()
        {
            string typeName = ShouldUseWebGLTransportProvider()
                ? "Convai.Infrastructure.Networking.WebGL.WebGLTransportProvider, Convai.Transport.WebGL"
                : "Convai.Infrastructure.Networking.Native.NativeTransportProvider, Convai.Transport.Native";
            var providerType = Type.GetType(typeName);
            if (providerType == null)
            {
                if (_debugLogging)
                    ConvaiLogger.Debug($"Transport provider type not found: {typeName}",
                        LogCategory.Bootstrap);
                return null;
            }

            PropertyInfo instanceProp = providerType.GetProperty("Instance",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

            if (instanceProp != null)
                return instanceProp.GetValue(null) as ITransportProvider;

            return Activator.CreateInstance(providerType) as ITransportProvider;
        }

        private static bool ShouldUseWebGLTransportProvider() =>
            !UnityEngine.Application.isEditor && UnityEngine.Application.platform == RuntimePlatform.WebGLPlayer;

        protected virtual IConversationProvider GetConversationProvider() =>
            ConvaiConversationProvider.Instance;

        /// <summary>
        ///     Overrides the runtime end-user identity provider used for room connections.
        /// </summary>
        public void SetEndUserIdentityProvider(IEndUserIdentityProvider provider)
        {
            _endUserIdentityProvider = provider;
            _host?.SetEndUserIdentityProvider(provider);
        }

        /// <summary>
        ///     Overrides the runtime end-user metadata provider used for room connections.
        /// </summary>
        public void SetEndUserMetadataProvider(IEndUserMetadataProvider provider)
        {
            _endUserMetadataProvider = provider;
            _host?.SetEndUserMetadataProvider(provider);
        }
    }
}
