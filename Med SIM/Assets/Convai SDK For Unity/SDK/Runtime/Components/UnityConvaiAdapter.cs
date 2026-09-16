using System;
using System.Threading;
using System.Threading.Tasks;
using Convai.Runtime.Core;
using Convai.Runtime.Core.Async;
using Convai.Runtime.Core.Coordinators;
using UnityEngine;

namespace Convai.Runtime.Components
{
    /// <summary>
    ///     Thin MonoBehaviour adapter that bridges Unity lifecycle to ConvaiRuntime.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>Design Note</b>: This component holds a reference to a <see cref="ConvaiRuntime" />
    ///         instance and maps Unity lifecycle events (Awake, Start, OnDestroy, OnApplicationPause,
    ///         OnApplicationFocus) to runtime lifecycle operations.
    ///     </para>
    ///     <para>
    ///         <b>Usage</b>: Create via <see cref="ConvaiManager" /> which builds the runtime
    ///         and passes it to this adapter, or programmatically via <see cref="Initialize" />.
    ///     </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class UnityConvaiAdapter : MonoBehaviour
    {
        private bool _isPaused;
        private CancellationTokenSource _lifetimeCts;
        private bool _startingOrStopping;

        public ConvaiRuntime Runtime { get; private set; }

        /// <summary>
        ///     Gets whether the adapter has been initialized with a runtime.
        /// </summary>
        public bool IsInitialized { get; private set; }

        public RuntimeState State => Runtime?.State ?? RuntimeState.Disposed;

        /// <summary>
        ///     Event fired when the runtime state changes.
        /// </summary>
        public event Action<RuntimeStateChanged> StateChanged;

        /// <summary>
        ///     Initializes the adapter with a runtime instance.
        /// </summary>
        /// <param name="runtime">The runtime to manage.</param>
        /// <exception cref="InvalidOperationException">If already initialized.</exception>
        public void Initialize(ConvaiRuntime runtime)
        {
            if (IsInitialized)
            {
                throw new InvalidOperationException(
                    "[UnityConvaiAdapter] Already initialized. Cannot initialize twice.");
            }

            Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _lifetimeCts = new CancellationTokenSource();
            IsInitialized = true;

            // Subscribe to runtime state changes
            if (Runtime.Events != null) Runtime.Events.Subscribe<RuntimeStateChanged>(OnRuntimeStateChanged);

            Debug.Log("[UnityConvaiAdapter] Initialized with runtime.");
        }

        /// <summary>
        ///     Starts the runtime asynchronously.
        /// </summary>
        public IConvaiOperation<Unit> StartRuntimeAsync() =>
            ConvaiOperation<Unit>.FromTask(StartRuntimeAsyncCore());

        private async Task<Unit> StartRuntimeAsyncCore()
        {
            if (!IsInitialized || Runtime == null)
            {
                Debug.LogError("[UnityConvaiAdapter] Cannot start: not initialized.");
                return Unit.Value;
            }

            if (_startingOrStopping)
            {
                Debug.LogWarning("[UnityConvaiAdapter] Start/stop already in progress.");
                return Unit.Value;
            }

            if (Runtime.State != RuntimeState.Created)
            {
                Debug.LogWarning($"[UnityConvaiAdapter] Cannot start: runtime is in {Runtime.State} state.");
                return Unit.Value;
            }

            try
            {
                _startingOrStopping = true;
                await Runtime.StartAsync(_lifetimeCts.Token);
            }
            finally
            {
                _startingOrStopping = false;
            }

            return Unit.Value;
        }

        /// <summary>
        ///     Stops the runtime asynchronously.
        /// </summary>
        public IConvaiOperation<Unit> StopRuntimeAsync() =>
            ConvaiOperation<Unit>.FromTask(StopRuntimeAsyncCore());

        private async Task<Unit> StopRuntimeAsyncCore()
        {
            if (!IsInitialized || Runtime == null)
                return Unit.Value;

            if (_startingOrStopping)
            {
                Debug.LogWarning("[UnityConvaiAdapter] Start/stop already in progress.");
                return Unit.Value;
            }

            try
            {
                _startingOrStopping = true;
                await Runtime.StopAsync(_lifetimeCts.Token);
            }
            finally
            {
                _startingOrStopping = false;
            }

            return Unit.Value;
        }

        #region Unity Lifecycle

        private void OnEnable()
        {
            if (IsInitialized && _isPaused && Runtime?.State == RuntimeState.Paused)
            {
                // Resume on re-enable (e.g., scene re-activated)
                _ = ResumeRuntimeAsync(RuntimePauseReason.SceneTransition);
            }
        }

        private void OnDisable()
        {
            if (IsInitialized && Runtime?.State == RuntimeState.Running)
            {
                // Pause on disable (e.g., scene deactivated)
                _ = PauseRuntimeAsync(RuntimePauseReason.SceneTransition);
            }
        }

        private async void OnDestroy()
        {
            if (Runtime != null)
            {
                _lifetimeCts?.Cancel();

                try
                {
                    await Runtime.DisposeAsync();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[UnityConvaiAdapter] Error disposing runtime: {ex.Message}");
                }

                Runtime = null;
            }

            _lifetimeCts?.Dispose();
            _lifetimeCts = null;
            IsInitialized = false;
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (!IsInitialized || Runtime == null)
                return;

            if (pauseStatus)
                _ = PauseRuntimeAsync(RuntimePauseReason.ApplicationBackground);
            else
                _ = ResumeRuntimeAsync(RuntimePauseReason.ApplicationBackground);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!IsInitialized || Runtime == null)
                return;

#if !UNITY_EDITOR
            if (!hasFocus)
            {
                _ = PauseRuntimeAsync(RuntimePauseReason.ApplicationFocusLost);
            }
            else
            {
                _ = ResumeRuntimeAsync(RuntimePauseReason.ApplicationFocusLost);
            }
#endif
        }

        #endregion

        #region Internal Operations

        private async Task PauseRuntimeAsync(RuntimePauseReason reason)
        {
            if (Runtime?.State != RuntimeState.Running)
                return;

            try
            {
                _isPaused = true;
                await Runtime.PauseAsync(reason, _lifetimeCts.Token);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnityConvaiAdapter] Error pausing runtime: {ex.Message}");
            }
        }

        private async Task ResumeRuntimeAsync(RuntimePauseReason reason)
        {
            if (Runtime?.State != RuntimeState.Paused)
                return;

            try
            {
                await Runtime.ResumeAsync(_lifetimeCts.Token);
                _isPaused = false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnityConvaiAdapter] Error resuming runtime: {ex.Message}");
            }
        }

        private void OnRuntimeStateChanged(RuntimeStateChanged stateChange)
        {
            Debug.Log($"[UnityConvaiAdapter] State changed: {stateChange.PreviousState} -> {stateChange.NewState}");
            StateChanged?.Invoke(stateChange);
        }

        #endregion
    }
}
