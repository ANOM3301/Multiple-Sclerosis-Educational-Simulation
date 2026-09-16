using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Convai.Domain.Abstractions;
using Convai.Domain.DomainEvents.Session;
using Convai.Domain.Errors;
using Convai.Domain.Logging;
using Convai.Infrastructure.Networking;
using Convai.Infrastructure.Networking.Models;
using Convai.RestAPI;
using Convai.RestAPI.Services;
using Convai.Runtime.Actions;
using Convai.Runtime.Behaviors;
using Convai.Runtime.Components;
using Convai.Runtime.Core.Async;
using Convai.Runtime.Core.Coordinators;
using Convai.Runtime.Core.Policies;
using Convai.Runtime.Emotion;
using Convai.Runtime.Networking.Media;
using Convai.Runtime.Room;
using Convai.Shared.Actions;
using Convai.Shared.Types;
using UnityEngine;
using ILogger = Convai.Domain.Logging.ILogger;

namespace Convai.Runtime.Adapters.Networking
{
    internal sealed class RoomConnectionRuntimeAdapter : IRoomConnectionRuntimeAdapter
    {
        private readonly Func<IConvaiCharacterAgent> _activeCharacterProvider;
        private readonly Func<bool> _canConnectProvider;
        private readonly Func<TurnTakingOptions> _configuredTurnTakingOptionsProvider;
        private readonly Func<ConnectionContext> _connectionContextProvider;
        private readonly object _connectionStateLock = new();
        private readonly Func<ConvaiConnectionType> _connectionTypeProvider;
        private readonly Func<RoomSessionConnectOptions> _consumePendingConnectOptions;
        private readonly Func<IConvaiRoomController> _controllerProvider;
        private readonly Func<string> _coreServerUrlProvider;
        private readonly Func<UserVadSettings> _configuredUserVadSettingsProvider;
        private readonly Func<RoomRespondModesConfig> _respondModesProvider;
        private readonly Func<RoomVisionInputConfig> _visionInputConfigProvider;
        private readonly Func<SessionState> _currentStateProvider;
        private readonly RoomDisconnectRuntimeAdapter _disconnectRuntimeAdapter;
        private readonly Func<bool> _isConnectedProvider;
        private readonly ILogger _logger;
        private readonly Action<bool> _onConnectAttemptStarted;
        private readonly Func<bool> _prepareConnectionFunc;
        private readonly Action<ConnectionFailure> _recordConnectionFailure;
        private readonly Action<string, string, string, string> _recordConnectionSuccess;
        private readonly Func<ReconnectPolicy> _resolveReconnectPolicy;
        private readonly Func<ISessionPersistence> _sessionPersistenceProvider;
        private readonly Action<ConnectionContext> _setConnectionContext;
        private readonly Action<TurnTakingOptions, ResolvedTurnTakingOptions> _prepareSessionTurnTakingState;
        private readonly Action<ResolvedTurnTakingOptions> _setCurrentResolvedTurnTakingOptions;
        private readonly Action<ReconnectPolicy> _setReconnectPolicy;
        private readonly TaskCompletionSource<bool> _startCompletedTcs = new();
        private readonly Func<int> _startWaitTimeoutProvider;
        private readonly Action<SessionState, SessionError?> _updateSessionState;
        private TaskCompletionSource<bool> _connectionStateTcs;

        public RoomConnectionRuntimeAdapter(
            Func<SessionState> currentStateProvider,
            Func<bool> isConnectedProvider,
            Func<bool> canConnectProvider,
            Func<int> startWaitTimeoutProvider,
            Func<bool> prepareConnectionFunc,
            Func<IConvaiCharacterAgent> activeCharacterProvider,
            Func<ConnectionContext> connectionContextProvider,
            Action<ConnectionContext> setConnectionContext,
            Func<ReconnectPolicy> resolveReconnectPolicy,
            Action<ReconnectPolicy> setReconnectPolicy,
            Action<bool> onConnectAttemptStarted,
            Func<IConvaiRoomController> controllerProvider,
            Func<ConvaiConnectionType> connectionTypeProvider,
            Func<string> coreServerUrlProvider,
            Func<TurnTakingOptions> configuredTurnTakingOptionsProvider,
            Func<UserVadSettings> configuredUserVadSettingsProvider,
            Func<RoomVisionInputConfig> visionInputConfigProvider,
            Func<RoomRespondModesConfig> respondModesProvider,
            Func<RoomSessionConnectOptions> consumePendingConnectOptions,
            Action<TurnTakingOptions, ResolvedTurnTakingOptions> prepareSessionTurnTakingState,
            Action<ResolvedTurnTakingOptions> setCurrentResolvedTurnTakingOptions,
            Func<ISessionPersistence> sessionPersistenceProvider,
            RoomDisconnectRuntimeAdapter disconnectRuntimeAdapter,
            Action<SessionState, SessionError?> updateSessionState,
            Action<string, string, string, string> recordConnectionSuccess,
            Action<ConnectionFailure> recordConnectionFailure,
            ILogger logger = null)
        {
            _currentStateProvider =
                currentStateProvider ?? throw new ArgumentNullException(nameof(currentStateProvider));
            _isConnectedProvider = isConnectedProvider ?? throw new ArgumentNullException(nameof(isConnectedProvider));
            _canConnectProvider = canConnectProvider ?? throw new ArgumentNullException(nameof(canConnectProvider));
            _startWaitTimeoutProvider = startWaitTimeoutProvider ??
                                        throw new ArgumentNullException(nameof(startWaitTimeoutProvider));
            _prepareConnectionFunc =
                prepareConnectionFunc ?? throw new ArgumentNullException(nameof(prepareConnectionFunc));
            _activeCharacterProvider = activeCharacterProvider ??
                                       throw new ArgumentNullException(nameof(activeCharacterProvider));
            _connectionContextProvider = connectionContextProvider ??
                                         throw new ArgumentNullException(nameof(connectionContextProvider));
            _setConnectionContext =
                setConnectionContext ?? throw new ArgumentNullException(nameof(setConnectionContext));
            _resolveReconnectPolicy =
                resolveReconnectPolicy ?? throw new ArgumentNullException(nameof(resolveReconnectPolicy));
            _setReconnectPolicy = setReconnectPolicy ?? throw new ArgumentNullException(nameof(setReconnectPolicy));
            _onConnectAttemptStarted = onConnectAttemptStarted ??
                                       throw new ArgumentNullException(nameof(onConnectAttemptStarted));
            _controllerProvider = controllerProvider ?? throw new ArgumentNullException(nameof(controllerProvider));
            _connectionTypeProvider =
                connectionTypeProvider ?? throw new ArgumentNullException(nameof(connectionTypeProvider));
            _coreServerUrlProvider =
                coreServerUrlProvider ?? throw new ArgumentNullException(nameof(coreServerUrlProvider));
            _configuredTurnTakingOptionsProvider = configuredTurnTakingOptionsProvider ??
                                                   throw new ArgumentNullException(
                                                       nameof(configuredTurnTakingOptionsProvider));
            _configuredUserVadSettingsProvider = configuredUserVadSettingsProvider ??
                                                 throw new ArgumentNullException(
                                                     nameof(configuredUserVadSettingsProvider));
            _visionInputConfigProvider = visionInputConfigProvider ?? (() => null);
            _respondModesProvider = respondModesProvider ?? (() => null);
            _consumePendingConnectOptions = consumePendingConnectOptions ??
                                            throw new ArgumentNullException(nameof(consumePendingConnectOptions));
            _prepareSessionTurnTakingState = prepareSessionTurnTakingState ??
                                             throw new ArgumentNullException(nameof(prepareSessionTurnTakingState));
            _setCurrentResolvedTurnTakingOptions = setCurrentResolvedTurnTakingOptions ??
                                                   throw new ArgumentNullException(
                                                       nameof(setCurrentResolvedTurnTakingOptions));
            _sessionPersistenceProvider = sessionPersistenceProvider ??
                                          throw new ArgumentNullException(nameof(sessionPersistenceProvider));
            _disconnectRuntimeAdapter = disconnectRuntimeAdapter ??
                                        throw new ArgumentNullException(nameof(disconnectRuntimeAdapter));
            _updateSessionState = updateSessionState ?? throw new ArgumentNullException(nameof(updateSessionState));
            _recordConnectionSuccess = recordConnectionSuccess ??
                                       throw new ArgumentNullException(nameof(recordConnectionSuccess));
            _recordConnectionFailure = recordConnectionFailure ??
                                       throw new ArgumentNullException(nameof(recordConnectionFailure));
            _logger = logger.WithTag(nameof(RoomConnectionRuntimeAdapter));
        }

        public event Action<SessionStateChanged> StateChanged;

        public SessionState CurrentState => _currentStateProvider();
        public bool IsConnected => _isConnectedProvider();
        public string CurrentRoomName => _controllerProvider()?.RoomName ?? string.Empty;
        public string CurrentSessionId => _controllerProvider()?.SessionID ?? string.Empty;
        public bool CanConnect => _canConnectProvider();
        public bool HasStarted { get; private set; }

        public async Task<bool> WaitForStartCompletionAsync(CancellationToken ct)
        {
            _logger?.Info("Waiting for room manager startup to complete...");

            int timeoutMs = _startWaitTimeoutProvider();
            using var timeoutCts = new CancellationTokenSource(timeoutMs);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            try
            {
                await Task.WhenAny(_startCompletedTcs.Task, Task.Delay(Timeout.Infinite, linkedCts.Token));
            }
            catch (OperationCanceledException)
            {
                if (ct.IsCancellationRequested)
                    return false;
            }

            // Re-check after timeout in case SignalStartCompleted was called in the same moment
            if (!HasStarted)
                _logger?.Error("ConnectAsync timed out waiting for startup.");

            return HasStarted;
        }

        public bool PrepareConnection() => _prepareConnectionFunc();

        public async Task<bool> WaitForConnectionResolutionAsync(CancellationToken ct)
        {
            TaskCompletionSource<bool> tcs = GetOrCreateConnectionStateTcs();

            try
            {
                await Task.WhenAny(tcs.Task, Task.Delay(Timeout.Infinite, ct));
            }
            catch (OperationCanceledException)
            {
            }

            if (ct.IsCancellationRequested)
                return false;

            return tcs.Task.IsCompletedSuccessfully && tcs.Task.Result;
        }

        public async Task<RoomConnectionAttemptResult> ConnectAsync(CancellationToken ct)
        {
            ReconnectPolicy reconnectPolicy = _resolveReconnectPolicy();
            _setReconnectPolicy(reconnectPolicy);

            ConnectionContext context = _connectionContextProvider();
            _onConnectAttemptStarted(context.HasValidRoom);
            _updateSessionState(SessionState.Connecting, null);

            IConvaiCharacterAgent activeCharacter = _activeCharacterProvider();
            if (activeCharacter == null)
            {
                _logger?.Error("Cannot connect: no active character.");
                var failure = ConnectionFailure.Create(
                    SessionErrorCodes.ConnectionFailed,
                    "Cannot connect because no active character is available.",
                    SessionErrorStage.Configuration);
                _recordConnectionFailure(failure);
                return RoomConnectionAttemptResult.Fail(failure);
            }

            string explicitCharacterSessionId = ResolveCharacterSessionId(activeCharacter);
            RoomSessionConnectOptions invocationOptions = _consumePendingConnectOptions();
            RoomJoinOptions joinOptions = ApplyInvocationJoinOverrides(
                CreateJoinOptions(
                    context,
                    reconnectPolicy,
                    activeCharacter.EnableSessionResume,
                    explicitCharacterSessionId),
                invocationOptions);
            TurnTakingOptions turnTakingSourceOptions =
                TurnTakingOptionsResolver.ResolveSourceOptions(
                    _configuredTurnTakingOptionsProvider(),
                    invocationOptions);
            ResolvedTurnTakingOptions resolvedTurnTakingOptions =
                TurnTakingOptionsResolver.ResolveFromSource(turnTakingSourceOptions);
            UserVadSettings resolvedUserVadSettings =
                _configuredUserVadSettingsProvider()?.Clone() ?? UserVadSettings.CreateDefault();
            RoomVisionInputConfig resolvedVisionInputConfig = _visionInputConfigProvider();
            RoomRespondModesConfig resolvedRespondModes = _respondModesProvider();
            LogUserVadSettings(resolvedUserVadSettings);
            _prepareSessionTurnTakingState(turnTakingSourceOptions, resolvedTurnTakingOptions);
            _setCurrentResolvedTurnTakingOptions(resolvedTurnTakingOptions);
            LogActionConfigDiagnostics(activeCharacter, invocationOptions);
            ConvaiActionConfig resolvedActionConfig = ResolveActionConfig(activeCharacter, invocationOptions);
            IReadOnlyList<ConvaiActionDefinition> actionDefinitionCatalog =
                ResolveActionDefinitionCatalog(activeCharacter, invocationOptions);
            IReadOnlyList<ConvaiActionDefinition> resolvedActionDefinitions =
                ResolveActionDefinitions(actionDefinitionCatalog, resolvedActionConfig);
            ApplyResolvedActionContext(
                activeCharacter,
                resolvedActionConfig,
                resolvedActionDefinitions,
                actionDefinitionCatalog);
            RoomEmotionConfig emotionConfig = ResolveEmotionConfig(activeCharacter);
            if (joinOptions != null)
            {
                joinOptions.ResolvedTurnTakingOptions = resolvedTurnTakingOptions;
                joinOptions.ResolvedUserVadSettings = resolvedUserVadSettings;
                joinOptions.ResolvedEmotionConfig = emotionConfig;
                joinOptions.ResolvedEndUserId = string.IsNullOrWhiteSpace(invocationOptions?.EndUserId)
                    ? null
                    : invocationOptions.EndUserId.Trim();
                joinOptions.ResolvedEndUserMetadata = CloneMetadata(invocationOptions?.EndUserMetadata);
                if (invocationOptions?.MaxNumParticipants > 0)
                    joinOptions.ResolvedMaxNumParticipants = invocationOptions.MaxNumParticipants;
                if (!string.IsNullOrWhiteSpace(invocationOptions?.SharedSessionKey))
                    joinOptions.ResolvedSharedSessionKey = invocationOptions.SharedSessionKey.Trim();
                joinOptions.ResolvedActionConfig = resolvedActionConfig?.Clone();
                joinOptions.ResolvedVisionInputConfig = resolvedVisionInputConfig;
                joinOptions.ResolvedRespondModes = resolvedRespondModes;
            }

            string reconnectMode = joinOptions.IsJoinRequest ? "rejoin" : "create";
            string coreServerUrl = _coreServerUrlProvider();
            _logger?.Info(
                $"Attempting {reconnectMode} for character: {activeCharacter.CharacterName} via {coreServerUrl}");

            try
            {
                return await ConnectWithRetryAsync(
                    _connectionTypeProvider().ToApiString(),
                    coreServerUrl,
                    activeCharacter.CharacterId,
                    activeCharacter.EnableSessionResume,
                    explicitCharacterSessionId,
                    activeCharacter.InitialDynamicInfoText,
                    activeCharacter.InitialDynamicInfoKeepInContext,
                    joinOptions,
                    ct);
            }
            catch (Exception ex)
            {
                ClearResolvedActionContext(activeCharacter);
                _logger?.Error($"ConnectAsync failed: {ex.Message}");
                ConnectionFailure failure = ConnectionFailure.FromException(
                    ex,
                    SessionErrorCodes.ConnectionFailed,
                    ex.Message,
                    SessionErrorStage.Runtime,
                    true);
                _recordConnectionFailure(failure);
                return RoomConnectionAttemptResult.Fail(failure);
            }
        }

        public async Task DisconnectAsync(CancellationToken ct) => await _disconnectRuntimeAdapter.DisconnectAsync(ct);

        public void NotifyStateChanged(SessionStateChanged stateChanged) =>
            StateChanged?.Invoke(stateChanged);

        public void SignalStartCompleted()
        {
            HasStarted = true;
            _startCompletedTcs.TrySetResult(true);
        }

        public void SignalConnectionStateWaiters(SessionState newState)
        {
            lock (_connectionStateLock)
            {
                if (_connectionStateTcs == null || _connectionStateTcs.Task.IsCompleted)
                    return;

                switch (newState)
                {
                    case SessionState.Connected:
                        _connectionStateTcs.TrySetResult(true);
                        break;
                    case SessionState.Error:
                    case SessionState.Disconnected:
                        _connectionStateTcs.TrySetResult(false);
                        break;
                }
            }
        }

        private TaskCompletionSource<bool> GetOrCreateConnectionStateTcs()
        {
            lock (_connectionStateLock)
            {
                if (_connectionStateTcs == null || _connectionStateTcs.Task.IsCompleted)
                    _connectionStateTcs = new TaskCompletionSource<bool>();
                return _connectionStateTcs;
            }
        }

        private RoomJoinOptions CreateJoinOptions(
            ConnectionContext connectionContext,
            ReconnectPolicy reconnectPolicy,
            bool enableSessionResume,
            string explicitCharacterSessionId)
        {
            reconnectPolicy ??= ReconnectPolicy.Default;

            ResumePolicy effectiveResumePolicy = enableSessionResume
                ? reconnectPolicy.ResumePolicy
                : ResumePolicy.AlwaysFresh;

            RoomJoinOptions joinOptions = RoomJoinOptions.FromContext(connectionContext, reconnectPolicy);
            if (enableSessionResume && !string.IsNullOrEmpty(explicitCharacterSessionId))
                return WithCharacterSessionId(joinOptions, explicitCharacterSessionId);

            if (effectiveResumePolicy != ResumePolicy.AlwaysFresh && explicitCharacterSessionId == null)
                return joinOptions;

            return joinOptions.IsJoinRequest
                ? new RoomJoinOptions(joinOptions.RoomName, null, joinOptions.SpawnAgent,
                    joinOptions.MaxNumParticipants)
                : RoomJoinOptions.CreateNew(null, joinOptions.MaxNumParticipants);
        }

        private async Task<RoomConnectionAttemptResult> ConnectWithRetryAsync(
            string connectionType,
            string coreServerUrl,
            string characterId,
            bool enableSessionResume,
            string explicitCharacterSessionId,
            string dynamicInfoText,
            bool keepDynamicInfoInContext,
            RoomJoinOptions joinOptions,
            CancellationToken cancellationToken)
        {
            ResolvedTurnTakingOptions resolvedTurnTakingOptions =
                joinOptions?.ResolvedTurnTakingOptions ?? ResolvedTurnTakingOptions.DefaultHandsFree;
            UserVadSettings resolvedUserVadSettings =
                joinOptions?.ResolvedUserVadSettings ?? UserVadSettings.CreateDefault();
            string sessionId = joinOptions?.CharacterSessionId;
            bool allowStoredSessionFallback = enableSessionResume && explicitCharacterSessionId == null;
            if (string.IsNullOrEmpty(sessionId) && allowStoredSessionFallback)
                sessionId = _sessionPersistenceProvider()?.LoadSession(characterId);

            RoomJoinOptions currentJoinOptions = joinOptions;
            string currentSessionId = sessionId;

            var executor = new RetryExecutor(new ExponentialBackoffPolicy());
            ConnectionFailure lastFailure = default;

            try
            {
                return await executor.ExecuteAsync(async (attempt, ct) =>
                {
                    string mode = currentJoinOptions?.IsJoinRequest == true ? "join" : "create";
                    _logger?.Info(
                        $"Attempt {attempt + 1} connecting character {characterId} (mode={mode})");

                    IConvaiRoomController controller = _controllerProvider();
                    if (controller == null)
                    {
                        lastFailure = ConnectionFailure.Create(
                            SessionErrorCodes.ConnectionFailed,
                            "Room controller is not initialized.",
                            SessionErrorStage.Configuration);
                        throw lastFailure.ToException();
                    }

                    RoomConnectionAttemptResult attemptResult = await controller.InitializeAsync(
                        connectionType,
                        coreServerUrl,
                        characterId,
                        currentSessionId,
                        enableSessionResume,
                        dynamicInfoText,
                        keepDynamicInfoInContext,
                        currentJoinOptions,
                        ct);

                    if (attemptResult.Succeeded)
                    {
                        PersistSession(characterId, enableSessionResume);
                        ApplyCurrentCharacterSessionId(_activeCharacterProvider(), controller.CharacterSessionID);
                        _logger?.Info(
                            $"Character {characterId} connected successfully (mode={mode}).");
                        _recordConnectionSuccess(
                            controller.RoomName,
                            controller.CharacterSessionID,
                            controller.SessionID,
                            characterId);
                        return RoomConnectionAttemptResult.Success();
                    }

                    lastFailure = attemptResult.Failure;

                    if (currentJoinOptions?.IsJoinRequest == true && attempt == 0)
                    {
                        _logger?.Info(
                            $"Join failed for room {currentJoinOptions.RoomName}, falling back to create mode.");
                        currentJoinOptions =
                            RoomJoinOptions.CreateNew(currentSessionId, currentJoinOptions.MaxNumParticipants);
                        currentJoinOptions.ResolvedTurnTakingOptions = resolvedTurnTakingOptions;
                        currentJoinOptions.ResolvedUserVadSettings = resolvedUserVadSettings;
                        currentJoinOptions.ResolvedEmotionConfig = joinOptions?.ResolvedEmotionConfig;
                        currentJoinOptions.ResolvedEndUserId = joinOptions?.ResolvedEndUserId;
                        currentJoinOptions.ResolvedEndUserMetadata =
                            CloneMetadata(joinOptions?.ResolvedEndUserMetadata);
                        currentJoinOptions.ResolvedSharedSessionKey = joinOptions?.ResolvedSharedSessionKey;
                        currentJoinOptions.ResolvedMaxNumParticipants = joinOptions?.ResolvedMaxNumParticipants;
                        currentJoinOptions.ResolvedActionConfig = joinOptions?.ResolvedActionConfig?.Clone();
                        currentJoinOptions.ResolvedVisionInputConfig = joinOptions?.ResolvedVisionInputConfig;
                        currentJoinOptions.ResolvedRespondModes = joinOptions?.ResolvedRespondModes;
                    }

                    currentSessionId = null;
                    throw lastFailure.ToException();
                }, cancellationToken);
            }
            catch (ConvaiOperationException)
            {
                _logger?.Error(
                    $"Failed to connect character {characterId} after retry exhaustion.");
                if (string.IsNullOrWhiteSpace(lastFailure.Code))
                {
                    lastFailure = ConnectionFailure.Create(
                        SessionErrorCodes.ConnectionFailed,
                        "Failed to connect to Convai room.",
                        SessionErrorStage.Runtime,
                        true);
                }

                _recordConnectionFailure(lastFailure);
                return RoomConnectionAttemptResult.Fail(lastFailure);
            }
        }

        private static string ResolveCharacterSessionId(IConvaiCharacterAgent activeCharacter)
        {
            if (activeCharacter is not ConvaiCharacter character)
                return null;

            return character.EnableSessionResume ? character.CharacterSessionId : null;
        }

        private static RoomJoinOptions WithCharacterSessionId(
            RoomJoinOptions joinOptions,
            string characterSessionId)
        {
            if (joinOptions == null || !joinOptions.IsJoinRequest)
                return RoomJoinOptions.CreateNew(characterSessionId, joinOptions?.MaxNumParticipants);

            return new RoomJoinOptions(
                joinOptions.RoomName,
                characterSessionId,
                joinOptions.SpawnAgent,
                joinOptions.MaxNumParticipants,
                joinOptions.CharacterId);
        }

        private static RoomJoinOptions ApplyInvocationJoinOverrides(
            RoomJoinOptions joinOptions,
            RoomSessionConnectOptions invocationOptions)
        {
            if (invocationOptions == null)
                return joinOptions;

            int? maxNumParticipants = invocationOptions.MaxNumParticipants > 0
                ? invocationOptions.MaxNumParticipants
                : null;
            string sharedSessionKey = string.IsNullOrWhiteSpace(invocationOptions.SharedSessionKey)
                ? null
                : invocationOptions.SharedSessionKey.Trim();

            if (!maxNumParticipants.HasValue && string.IsNullOrEmpty(sharedSessionKey))
                return joinOptions;

            RoomJoinOptions resolvedJoinOptions = WithMaxNumParticipants(joinOptions, maxNumParticipants);
            if (maxNumParticipants.HasValue)
                resolvedJoinOptions.ResolvedMaxNumParticipants = maxNumParticipants;
            if (!string.IsNullOrEmpty(sharedSessionKey))
                resolvedJoinOptions.ResolvedSharedSessionKey = sharedSessionKey;

            return resolvedJoinOptions;
        }

        private static RoomJoinOptions WithMaxNumParticipants(
            RoomJoinOptions joinOptions,
            int? maxNumParticipants)
        {
            if (!maxNumParticipants.HasValue)
                return joinOptions;

            if (joinOptions == null || !joinOptions.IsJoinRequest)
                return RoomJoinOptions.CreateNew(joinOptions?.CharacterSessionId, maxNumParticipants);

            return new RoomJoinOptions(
                joinOptions.RoomName,
                joinOptions.CharacterSessionId,
                joinOptions.SpawnAgent,
                maxNumParticipants,
                joinOptions.CharacterId);
        }

        private RoomEmotionConfig ResolveEmotionConfig(IConvaiCharacterAgent activeCharacter)
        {
            RoomEmotionConfig emotionConfig = EmotionConfigResolver.Resolve(activeCharacter.EmotionDetectionMode);
            if (emotionConfig != null)
                _logger?.Debug(
                    $"Emotion detection '{emotionConfig.Provider}' requested for character {activeCharacter.CharacterId} (client-controlled).");
            else
                _logger?.Debug(
                    $"Emotion detection disabled for character {activeCharacter.CharacterId} (no emotion controller, or mode Off).");

            return emotionConfig;
        }

        private static void ApplyCurrentCharacterSessionId(
            IConvaiCharacterAgent activeCharacter,
            string characterSessionId)
        {
            if (activeCharacter is ConvaiCharacter character)
                character.SetCurrentCharacterSessionId(characterSessionId);
        }

        private static IReadOnlyDictionary<string, object> CloneMetadata(
            IReadOnlyDictionary<string, object> metadata)
        {
            if (metadata == null || metadata.Count == 0)
                return null;

            return new Dictionary<string, object>(metadata);
        }

        private static ConvaiActionConfig ResolveActionConfig(
            IConvaiCharacterAgent activeCharacter,
            RoomSessionConnectOptions invocationOptions)
        {
            if (invocationOptions?.ActionConfigOverride != null)
                return invocationOptions.ActionConfigOverride.Clone();

            if (activeCharacter is not Component component)
                return null;

            ConvaiActionConfigSource source = component.GetComponent<ConvaiActionConfigSource>();
            return source?.BuildActionConfig();
        }

        private static IReadOnlyList<ConvaiActionDefinition> ResolveActionDefinitionCatalog(
            IConvaiCharacterAgent activeCharacter,
            RoomSessionConnectOptions invocationOptions)
        {
            if (invocationOptions?.ActionDefinitionsOverride != null &&
                invocationOptions.ActionDefinitionsOverride.Count > 0)
                return ConvaiActionDefinition.FilterAndClone(
                    invocationOptions.ActionDefinitionsOverride,
                    requireExecutable: true);

            if (activeCharacter is not Component component)
                return null;

            ConvaiActionConfigSource source = component.GetComponent<ConvaiActionConfigSource>();
            return source?.GetEffectiveDefinitions(requireExecutable: true);
        }

        private static IReadOnlyList<ConvaiActionDefinition> ResolveActionDefinitions(
            IReadOnlyList<ConvaiActionDefinition> definitionCatalog,
            ConvaiActionConfig resolvedActionConfig)
        {
            if (resolvedActionConfig?.Actions == null || resolvedActionConfig.Actions.Count == 0)
                return Array.Empty<ConvaiActionDefinition>();

            return ConvaiActionDefinition.FilterAndClone(
                definitionCatalog,
                resolvedActionConfig?.Actions,
                requireExecutable: true);
        }

        private void LogUserVadSettings(UserVadSettings resolvedUserVadSettings)
        {
            if (resolvedUserVadSettings == null || resolvedUserVadSettings.UseServerDefault)
            {
                _logger?.Debug(
                    "Using server default vad_params (field omitted from connect payload).");
                return;
            }

            _logger?.Info(
                "Sending custom vad_params: " +
                $"confidence={resolvedUserVadSettings.Confidence}, " +
                $"start_secs={resolvedUserVadSettings.StartSecs}, " +
                $"stop_secs={resolvedUserVadSettings.StopSecs}, " +
                $"min_volume={resolvedUserVadSettings.MinVolume}");
        }

        private void LogActionConfigDiagnostics(
            IConvaiCharacterAgent activeCharacter,
            RoomSessionConnectOptions invocationOptions)
        {
            if (invocationOptions?.ActionConfigOverride != null)
                return;

            if (activeCharacter is not Component component)
                return;

            ConvaiActionConfigSource source = component.GetComponent<ConvaiActionConfigSource>();
            if (source == null)
                return;

            IReadOnlyList<ConvaiActionConfigDiagnostic> diagnostics = ConvaiActionConfigValidator.Validate(source);
            if (diagnostics == null || diagnostics.Count == 0)
                return;

            for (int i = 0; i < diagnostics.Count; i++)
            {
                ConvaiActionConfigDiagnostic diagnostic = diagnostics[i];
                if (diagnostic == null)
                    continue;

                string message = $"[RoomConnectionRuntimeAdapter] Action config diagnostic: {diagnostic}";
                if (diagnostic.Severity == ConvaiActionConfigDiagnosticSeverity.Error)
                    _logger?.Error(message);
                else
                    _logger?.Warning(message);
            }
        }

        private static void ApplyResolvedActionContext(
            IConvaiCharacterAgent activeCharacter,
            ConvaiActionConfig resolvedActionConfig,
            IReadOnlyList<ConvaiActionDefinition> resolvedActionDefinitions,
            IReadOnlyList<ConvaiActionDefinition> actionDefinitionCatalog)
        {
            if (activeCharacter is ConvaiCharacter character)
            {
                character.SetResolvedSessionActionConfig(resolvedActionConfig);
                character.SetResolvedSessionActionDefinitions(resolvedActionDefinitions);
                character.SetResolvedSessionActionDefinitionCatalog(
                    actionDefinitionCatalog ?? Array.Empty<ConvaiActionDefinition>());
                return;
            }

            if (activeCharacter is Component component &&
                component.TryGetComponent(out ConvaiCharacter targetCharacter))
            {
                targetCharacter.SetResolvedSessionActionConfig(resolvedActionConfig);
                targetCharacter.SetResolvedSessionActionDefinitions(resolvedActionDefinitions);
                targetCharacter.SetResolvedSessionActionDefinitionCatalog(
                    actionDefinitionCatalog ?? Array.Empty<ConvaiActionDefinition>());
            }
        }

        private static void ClearResolvedActionContext(IConvaiCharacterAgent activeCharacter)
        {
            if (activeCharacter is ConvaiCharacter character)
            {
                character.ClearResolvedSessionActionConfig();
                character.ClearResolvedSessionActionDefinitions();
                character.ClearResolvedSessionActionDefinitionCatalog();
                return;
            }

            if (activeCharacter is Component component &&
                component.GetComponent<ConvaiCharacter>() is { } targetCharacter)
            {
                targetCharacter.ClearResolvedSessionActionConfig();
                targetCharacter.ClearResolvedSessionActionDefinitions();
                targetCharacter.ClearResolvedSessionActionDefinitionCatalog();
            }
        }

        private void PersistSession(string characterId, bool enableSessionResume)
        {
            if (!enableSessionResume) return;

            string sessionId = _controllerProvider()?.CharacterSessionID;
            if (string.IsNullOrEmpty(sessionId)) return;

            _sessionPersistenceProvider()?.SaveSession(characterId, sessionId);

            ConnectionContext currentContext = _connectionContextProvider();
            if (!currentContext.HasValidRoom)
                return;

            _setConnectionContext(currentContext);
        }
    }

    internal sealed class RoomDisconnectRuntimeAdapter
    {
        private readonly Func<AudioTrackManager> _audioTrackManagerProvider;
        private readonly Action<bool, string> _completeDisconnectionTracking;
        private readonly Func<IConvaiRoomController> _controllerProvider;
        private readonly ILogger _logger;
        private readonly Action _resetBargeInCoordinatorState;
        private readonly Action<SessionState, SessionError?> _updateSessionState;

        public RoomDisconnectRuntimeAdapter(
            Func<AudioTrackManager> audioTrackManagerProvider,
            Func<IConvaiRoomController> controllerProvider,
            Action<SessionState, SessionError?> updateSessionState,
            Action<bool, string> completeDisconnectionTracking,
            Action resetBargeInCoordinatorState,
            ILogger logger = null)
        {
            _audioTrackManagerProvider = audioTrackManagerProvider ??
                                         throw new ArgumentNullException(nameof(audioTrackManagerProvider));
            _controllerProvider = controllerProvider ?? throw new ArgumentNullException(nameof(controllerProvider));
            _updateSessionState = updateSessionState ?? throw new ArgumentNullException(nameof(updateSessionState));
            _completeDisconnectionTracking =
                completeDisconnectionTracking ?? throw new ArgumentNullException(nameof(completeDisconnectionTracking));
            _resetBargeInCoordinatorState = resetBargeInCoordinatorState ??
                                            throw new ArgumentNullException(
                                                nameof(resetBargeInCoordinatorState));
            _logger = logger.WithTag(nameof(RoomDisconnectRuntimeAdapter));
        }

        public async Task DisconnectAsync(CancellationToken ct)
        {
            _resetBargeInCoordinatorState();
            try
            {
                AudioTrackManager audioTrackManager = _audioTrackManagerProvider();
                if (audioTrackManager != null)
                {
                    try
                    {
                        await audioTrackManager.UnpublishMicrophoneAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger?.Warning(
                            $"DisconnectAsync failed to unpublish microphone cleanly: {ex.Message}");
                    }

                    audioTrackManager.SetMicMuted(true);
                    audioTrackManager.ClearState();
                }

                _logger?.Info("DisconnectAsync called.");

                _updateSessionState(SessionState.Disconnecting, null);

                IConvaiRoomController controller = _controllerProvider();
                if (controller != null)
                    await controller.DisconnectFromRoomAsync(ct);

                _completeDisconnectionTracking(true, "Disconnected from room");
            }
            finally
            {
                _resetBargeInCoordinatorState();
            }
        }

        public void HandleUnexpectedDisconnect(bool updateSessionState, string completionMessage)
        {
            _resetBargeInCoordinatorState();
            try
            {
                AudioTrackManager audioTrackManager = _audioTrackManagerProvider();
                audioTrackManager?.SetMicMuted(true);
                audioTrackManager?.ClearState();
                _completeDisconnectionTracking(updateSessionState, completionMessage);
            }
            finally
            {
                _resetBargeInCoordinatorState();
            }
        }
    }
}
