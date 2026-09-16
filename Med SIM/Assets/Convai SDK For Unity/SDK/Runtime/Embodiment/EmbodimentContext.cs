using System;
using System.Collections.Generic;
using Convai.Domain.Embodiment.Interfaces;
using Convai.Domain.EventSystem;
using Convai.Runtime.Animation;
using Convai.Runtime.Behaviors;
using Convai.Runtime.Components;
using Convai.Domain.Logging;
using Convai.Runtime.Core.DependencyInjection;
using Convai.Runtime.Logging;
using UnityEngine;
using ILogger = Convai.Domain.Logging.ILogger;

namespace Convai.Runtime.Embodiment
{
    /// <summary>
    ///     Character-scoped composition root that exposes the shared infrastructure used by
    ///     decoupled embodiment modules.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The context lives on the character root and is populated at runtime through
    ///         the neutral character dependency injection contract. Embodiment modules resolve the
    ///         context via <see cref="TryResolve(Component, out EmbodimentContext)" />
    ///         during their <c>Awake</c> and keep the reference for the component lifetime.
    ///     </para>
    ///     <para>
    ///         Every infrastructure dependency is resolved lazily so the context stays usable in
    ///         edit-mode previews (before <see cref="Populate" /> has been called). The heavy
    ///         lifting is delegated to <see cref="EmbodimentDependencyResolver" />,
    ///         <see cref="EmbodimentProfileReceiverIndex" />, and per-source
    ///         <see cref="EmbodimentContextSlot{T}" /> instances so this type remains a thin facade.
    ///     </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(EmbodimentExecutionOrders.Context)]
    [AddComponentMenu("")]
    [EmbodimentComponentBranding("Embodiment Context", "Runtime Embodiment")]
    public sealed class EmbodimentContext : MonoBehaviour, IInjectable<IConvaiCharacterDependencies>
    {
        [Header("Facial Animation")]
        [Tooltip(
            "Optional override for the facial blendshape composition profile. " +
            "When assigned, this profile is pushed to the FacialBlendshapeCompositorHost " +
            "during initialization. Leave empty to use the packaged default loaded from Resources " +
            "(Embodiment/ConvaiFacialCompositionProfile_Default, then compatibility Convai/ paths; see " +
            "FacialCompositionProfileResourceResolution).")]
        [SerializeField] private ConvaiFacialCompositionProfile _facialCompositionProfileOverride;

        private IEventHub _eventHub;
        private ILogger _logger;

        private EmbodimentDependencyResolver _dependencies;

        private readonly EmbodimentProfileReceiverIndex _profileReceivers = new();

        private readonly EmbodimentContextSlot<IConversationFlowSource> _conversationFlowSlot = new("conversation flow source");
        private readonly EmbodimentContextSlot<IAttentionSource> _attentionSlot = new("attention source");
        private readonly EmbodimentContextSlot<IEmotionStateSource> _emotionStateSlot = new("emotion state source");
        private readonly EmbodimentContextSlot<IEmotionMouthWeightProvider> _emotionMouthSlot = new("emotion mouth provider");
        private readonly EmbodimentContextSlot<IGazeIntentProvider> _gazeIntentSlot = new("gaze intent provider");
        private readonly EmbodimentContextSlot<ISpeechEnergyProvider> _speechEnergySlot = new("speech energy provider");

        private bool _conversationFlowDriverDemanded;

        /// <summary>Character root transform.</summary>
        public Transform CharacterRoot => transform;

        /// <summary>Event bus for listening to domain events.</summary>
        public IEventHub EventHub => _eventHub;

        /// <summary>Logger for structured diagnostics (may be null if not injected).</summary>
        public ILogger Logger => _logger;

        /// <summary>Compositor host responsible for writing facial blendshapes.</summary>
        public FacialBlendshapeCompositorHost Compositor => _dependencies.Compositor;

        /// <summary>
        ///     Optional facial composition profile override assigned in the inspector. When
        ///     <c>null</c>, the compositor loads the packaged default from <c>Resources</c> via
        ///     <see cref="FacialCompositionProfileResourceResolution" /> (see
        ///     <see cref="FacialCompositionProfileResourceResolution.PrimaryBuiltInResourceRelativePath" />).
        /// </summary>
        public ConvaiFacialCompositionProfile FacialCompositionProfileOverride => _facialCompositionProfileOverride;

        /// <summary>Single-writer animator conductor.</summary>
        public AnimatorConductor AnimatorConductor => _dependencies.AnimatorConductor;

        /// <summary>Deterministic tick scheduler for embodiment modules.</summary>
        public EmbodimentTickScheduler TickScheduler => _dependencies.TickScheduler;

        /// <summary>Rig binding abstraction (semantic bones / blendshapes).</summary>
        public IStandardRigBinding RigBinding => _dependencies.RigBinding;

        /// <summary>The owning character, when one is present on this hierarchy.</summary>
        public ConvaiCharacter Character => _dependencies.Character;

        /// <summary>
        ///     Adapter exposing LipSync's speech-state through <see cref="IDialoguePhaseProvider" />.
        /// </summary>
        public IDialoguePhaseProvider DialoguePhase => _dependencies.DialoguePhase;

        /// <summary>Currently registered speech energy source (if any).</summary>
        public ISpeechEnergyProvider SpeechEnergyProvider => _speechEnergySlot.Current;

        /// <summary>Currently registered conversation flow source (if any).</summary>
        public IConversationFlowSource ConversationFlowSource => _conversationFlowSlot.Current;

        /// <summary>Currently registered attention source (if any).</summary>
        public IAttentionSource AttentionSource => _attentionSlot.Current;

        /// <summary>Currently registered emotion state source (if any).</summary>
        public IEmotionStateSource EmotionStateSource => _emotionStateSlot.Current;

        /// <summary>Currently registered emotion mouth weight provider (if any).</summary>
        public IEmotionMouthWeightProvider EmotionMouthProvider => _emotionMouthSlot.Current;

        /// <summary>Currently registered gaze intent provider (if any).</summary>
        public IGazeIntentProvider GazeIntentProvider => _gazeIntentSlot.Current;

        /// <summary>
        ///     Raised when the conversation flow source is registered or cleared. Modules
        ///     that need to subscribe to the source's events can hook this notification to
        ///     connect lazily, avoiding sibling <c>OnEnable</c> ordering fragility.
        /// </summary>
        /// <remarks>
        ///     The event is raised with the new source (or <c>null</c> on unregister). It
        ///     fires synchronously from Register/Unregister; subscriber exceptions are logged
        ///     but do not propagate to the registrar.
        /// </remarks>
        public event Action<IConversationFlowSource> ConversationFlowSourceChanged
        {
            add => _conversationFlowSlot.Changed += value;
            remove => _conversationFlowSlot.Changed -= value;
        }

        /// <summary>Raised when the attention source is registered or cleared.</summary>
        public event Action<IAttentionSource> AttentionSourceChanged
        {
            add => _attentionSlot.Changed += value;
            remove => _attentionSlot.Changed -= value;
        }

        /// <summary>Raised when the emotion state source is registered or cleared.</summary>
        public event Action<IEmotionStateSource> EmotionStateSourceChanged
        {
            add => _emotionStateSlot.Changed += value;
            remove => _emotionStateSlot.Changed -= value;
        }

        /// <summary>Raised when the emotion mouth weight provider is registered or cleared.</summary>
        public event Action<IEmotionMouthWeightProvider> EmotionMouthProviderChanged
        {
            add => _emotionMouthSlot.Changed += value;
            remove => _emotionMouthSlot.Changed -= value;
        }

        /// <summary>Raised when the gaze intent provider is registered or cleared.</summary>
        public event Action<IGazeIntentProvider> GazeIntentProviderChanged
        {
            add => _gazeIntentSlot.Changed += value;
            remove => _gazeIntentSlot.Changed -= value;
        }

        /// <summary>Raised when a profile receiver registers on this character.</summary>
        public event Action<EmbodimentProfileReceiverRegistration> ProfileReceiverRegistered
        {
            add => _profileReceivers.Registered += value;
            remove => _profileReceivers.Registered -= value;
        }

        /// <summary>Raised when a speech energy provider is registered or cleared.</summary>
        public event Action<ISpeechEnergyProvider> SpeechEnergyProviderChanged
        {
            add => _speechEnergySlot.Changed += value;
            remove => _speechEnergySlot.Changed -= value;
        }

        /// <summary>
        ///     Raised when the semantic rig binding is rebuilt or replaced at runtime.
        ///     Modules that cache bone or mesh references should resolve them again when this
        ///     fires.
        /// </summary>
        public event Action<IStandardRigBinding> RigBindingChanged;

        /// <summary>
        ///     Raised after embodiment-module configuration has been updated at runtime
        ///     (for example by swapping a <c>CharacterEmbodimentPreset</c>).
        /// </summary>
        public event Action EmbodimentConfigurationChanged;

        /// <summary>
        ///     Raised after runtime-only dependencies such as <see cref="EventHub" /> and the
        ///     lazily provisioned scheduler / animator infrastructure become available.
        /// </summary>
        public event Action DependenciesPopulated;

        internal static void RegisterDefaultConversationFlowSourceFactory(
            Func<EmbodimentContext, IConversationFlowSource> factory) =>
            EmbodimentContextConversationFlowProvisioner.RegisterDefaultFactory(factory);

        int IInjectable<IConvaiCharacterDependencies>.InjectionOrder => -100;

        void IInjectable<IConvaiCharacterDependencies>.InjectDependencies(IConvaiCharacterDependencies dependencies)
        {
            if (dependencies == null) throw new ArgumentNullException(nameof(dependencies));
            Populate(dependencies.EventHub, dependencies.Logger);
        }

        private void Awake()
        {
            hideFlags = RuntimeInfrastructureHideFlags();
            EnsureCollaborators();
            _dependencies.ResolveOptionalComponents(_facialCompositionProfileOverride);
        }

        /// <summary>
        ///     Locates or creates a context on the supplied component's character root and
        ///     resolves any already-authored infrastructure components.
        /// </summary>
        public static bool TryResolve(Component origin, out EmbodimentContext context)
        {
            context = null;
            if (origin == null) return false;

            context = origin.GetComponentInParent<EmbodimentContext>(true);
            if (context != null)
            {
                context.EnsureCollaborators();
                context._dependencies.ResolveOptionalComponents(context._facialCompositionProfileOverride);
                return true;
            }

            GameObject owner = ResolveContextOwner(origin);
            if (owner == null) return false;

            context = owner.GetComponent<EmbodimentContext>();
            if (context == null)
            {
                context = owner.AddComponent<EmbodimentContext>();
                context.hideFlags = RuntimeInfrastructureHideFlags();
            }

            context.EnsureCollaborators();
            context._dependencies.ResolveOptionalComponents(context._facialCompositionProfileOverride);
            return true;
        }

        /// <summary>
        ///     Convenience helper for modules: resolves the parent embodiment context and returns
        ///     a null-safe pointer. Logs a warning and returns <c>false</c> on failure so the
        ///     caller can disable itself gracefully.
        /// </summary>
        public static bool TryResolveFor(Component owner, out EmbodimentContext context)
        {
            if (owner == null)
            {
                context = null;
                ConvaiLogger.Warning(
                    "TryResolveFor was called with a null owner reference.",
                    LogCategory.Character);
                return false;
            }

            return TryResolve(owner, out context);
        }

        /// <summary>
        ///     Called after dependency composition so embodiment modules have a live event hub and logger.
        /// </summary>
        public void Populate(IEventHub eventHub, ILogger logger)
        {
            _eventHub = eventHub;
            _logger = logger;

            EnsureCollaborators();
            _dependencies.ResolveOptionalComponents(_facialCompositionProfileOverride);
            RaiseDependenciesPopulated();
        }

        /// <summary>Registers the conversation flow source; only one is supported per character.</summary>
        public void RegisterConversationFlowSource(IConversationFlowSource source) =>
            _conversationFlowSlot.TryRegister(source);

        /// <summary>Unregisters the conversation flow source when the owning module is destroyed.</summary>
        public void UnregisterConversationFlowSource(IConversationFlowSource source) =>
            _conversationFlowSlot.Unregister(source);

        /// <summary>Registers the attention source.</summary>
        public void RegisterAttentionSource(IAttentionSource source) =>
            _attentionSlot.TryRegister(source);

        /// <summary>Unregisters the attention source.</summary>
        public void UnregisterAttentionSource(IAttentionSource source) =>
            _attentionSlot.Unregister(source);

        /// <summary>Registers the emotion state source.</summary>
        public void RegisterEmotionStateSource(IEmotionStateSource source) =>
            _emotionStateSlot.TryRegister(source);

        /// <summary>Unregisters the emotion state source.</summary>
        public void UnregisterEmotionStateSource(IEmotionStateSource source) =>
            _emotionStateSlot.Unregister(source);

        /// <summary>Registers the emotion mouth weight provider.</summary>
        public void RegisterEmotionMouthProvider(IEmotionMouthWeightProvider provider) =>
            _emotionMouthSlot.TryRegister(provider);

        /// <summary>Unregisters the emotion mouth weight provider.</summary>
        public void UnregisterEmotionMouthProvider(IEmotionMouthWeightProvider provider) =>
            _emotionMouthSlot.Unregister(provider);

        /// <summary>Registers the gaze intent provider.</summary>
        public void RegisterGazeIntentProvider(IGazeIntentProvider provider) =>
            _gazeIntentSlot.TryRegister(provider);

        /// <summary>Unregisters the gaze intent provider.</summary>
        public void UnregisterGazeIntentProvider(IGazeIntentProvider provider) =>
            _gazeIntentSlot.Unregister(provider);

        /// <summary>Registers the speech energy provider.</summary>
        public void RegisterSpeechEnergyProvider(ISpeechEnergyProvider provider) =>
            _speechEnergySlot.TryRegister(provider);

        /// <summary>Unregisters the speech energy provider.</summary>
        public void UnregisterSpeechEnergyProvider(ISpeechEnergyProvider provider) =>
            _speechEnergySlot.Unregister(provider);

        /// <summary>Registers an embodiment profile receiver for live preset application.</summary>
        public void RegisterProfileReceiver(IEmbodimentProfileReceiver receiver, Component owner) =>
            _profileReceivers.Register(receiver, owner);

        /// <summary>Unregisters a profile receiver when its component disables.</summary>
        public void UnregisterProfileReceiver(IEmbodimentProfileReceiver receiver) =>
            _profileReceivers.Unregister(receiver);

        /// <summary>Copies active profile receiver registrations into <paramref name="results" />.</summary>
        public void GetProfileReceivers(List<EmbodimentProfileReceiverRegistration> results) =>
            _profileReceivers.CopyTo(results);

        /// <summary>
        ///     Marks that an embodiment module on this character wants the default conversation-flow
        ///     driver to be auto-provisioned when <see cref="TryEnsureConversationFlowSource" /> runs.
        /// </summary>
        internal void MarkConversationFlowDriverDemanded() => _conversationFlowDriverDemanded = true;

        /// <summary>Whether a module has signaled that auto-creating a flow driver is allowed.</summary>
        internal bool IsConversationFlowDriverDemanded => _conversationFlowDriverDemanded;

        internal bool TryEnsureConversationFlowSource()
        {
            if (_conversationFlowSlot.Current != null) return true;
            if (!UnityEngine.Application.isPlaying) return false;

            IConversationFlowSource source = EmbodimentContextConversationFlowProvisioner.CreateDefault(this);
            if (source != null && _conversationFlowSlot.Current == null)
                RegisterConversationFlowSource(source);

            return _conversationFlowSlot.Current != null;
        }

        /// <summary>
        ///     Returns the currently registered speech energy provider, or attempts to bootstrap
        ///     one via the LipSync bridge if no provider is registered. In most scenarios, providers
        ///     self-register during their lifecycle (e.g., <c>OnEnable</c>), so this method returns
        ///     the already-registered instance without additional scanning.
        /// </summary>
        public ISpeechEnergyProvider EnsureSpeechEnergyProvider()
        {
            if (_speechEnergySlot.Current != null) return _speechEnergySlot.Current;

            if (UnityEngine.Application.isPlaying)
                EmbodimentLipSyncBridge.TryRegisterSpeechEnergyAdapter(this);

            return _speechEnergySlot.Current;
        }

        /// <summary>
        ///     Publishes that the character's semantic rig binding has changed. Modules use
        ///     this to rebuild cached bone / blendshape resolution without a disable-enable
        ///     cycle.
        /// </summary>
        public void NotifyRigBindingChanged(IStandardRigBinding rigBinding = null)
        {
            _dependencies.NotifyRigBindingChanged(rigBinding);
            RaiseRigBindingChanged(_dependencies.RigBinding);
        }

        /// <summary>
        ///     Publishes that one or more embodiment-module configurations changed at runtime.
        /// </summary>
        public void NotifyEmbodimentConfigurationChanged()
        {
            Action handler = EmbodimentConfigurationChanged;
            if (handler == null) return;

            try
            {
                handler.Invoke();
            }
            catch (Exception ex)
            {
                LogEventSubscriberException(
                    ex,
                    "[EmbodimentContext] A subscriber threw while handling EmbodimentConfigurationChanged.");
            }
        }

        public FacialBlendshapeCompositorHost EnsureCompositor() =>
            _dependencies.EnsureCompositor(_facialCompositionProfileOverride);

        public AnimatorConductor EnsureAnimatorConductor() => _dependencies.EnsureAnimatorConductor();

        public EmbodimentTickScheduler EnsureTickScheduler() => _dependencies.EnsureTickScheduler();

        public IStandardRigBinding EnsureRigBinding() => _dependencies.EnsureRigBinding();

        public IDialoguePhaseProvider EnsureDialoguePhase() =>
            _dependencies.EnsureDialoguePhase(_facialCompositionProfileOverride);

        private void EnsureCollaborators()
        {
            EnsureSlotsLoggingAttached();
            if (_dependencies == null) _dependencies = new EmbodimentDependencyResolver(this);
        }

        private void EnsureSlotsLoggingAttached()
        {
            _conversationFlowSlot.EnsureAttached(this);
            _attentionSlot.EnsureAttached(this);
            _emotionStateSlot.EnsureAttached(this);
            _emotionMouthSlot.EnsureAttached(this);
            _gazeIntentSlot.EnsureAttached(this);
            _speechEnergySlot.EnsureAttached(this);
            _profileReceivers.EnsureAttached(this);
        }

        private void LogEventSubscriberException(Exception ex, string message)
        {
            if (_logger != null)
                _logger.Error(ex, message, LogCategory.Character);
            else
                ConvaiLogger.Exception(ex, LogCategory.Character);
        }

        private static GameObject ResolveContextOwner(Component origin)
        {
            ConvaiCharacter character = origin.GetComponentInParent<ConvaiCharacter>(true);
            return character != null ? character.gameObject : origin.gameObject;
        }

        private void RaiseDependenciesPopulated()
        {
            Action handler = DependenciesPopulated;
            if (handler == null) return;

            try
            {
                handler.Invoke();
            }
            catch (Exception ex)
            {
                LogEventSubscriberException(
                    ex,
                    "[EmbodimentContext] A subscriber threw while handling DependenciesPopulated.");
            }
        }

        private void RaiseRigBindingChanged(IStandardRigBinding rigBinding)
        {
            Action<IStandardRigBinding> handler = RigBindingChanged;
            if (handler == null) return;

            try
            {
                handler.Invoke(rigBinding);
            }
            catch (Exception ex)
            {
                LogEventSubscriberException(
                    ex,
                    "[EmbodimentContext] A subscriber threw while handling RigBindingChanged.");
            }
        }

        internal static HideFlags RuntimeInfrastructureHideFlags() =>
            UnityEngine.Application.isPlaying ? HideFlags.None : HideFlags.HideInInspector;
    }
}
