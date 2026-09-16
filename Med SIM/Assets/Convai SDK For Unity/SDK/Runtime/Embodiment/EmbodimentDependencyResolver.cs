using Convai.Domain.Embodiment.Interfaces;
using Convai.Runtime.Animation;
using Convai.Runtime.Behaviors;
using Convai.Runtime.Components;
using UnityEngine;

namespace Convai.Runtime.Embodiment
{
    /// <summary>
    ///     Owns lazy resolution of the optional infrastructure components (compositor, animator
    ///     conductor, tick scheduler, rig binding, character, dialogue phase adapter) that
    ///     <see cref="EmbodimentContext" /> exposes to embodiment modules. Encapsulates the
    ///     "find existing or create on demand" pattern so the context itself can stay a thin
    ///     facade.
    /// </summary>
    internal sealed class EmbodimentDependencyResolver
    {
        private readonly EmbodimentContext _owner;

        private FacialBlendshapeCompositorHost _compositor;
        private AnimatorConductor _animatorConductor;
        private EmbodimentTickScheduler _tickScheduler;
        private StandardRigBinding _rigBinding;
        private ConvaiCharacter _character;
        private CompositorDialoguePhaseAdapter _dialoguePhaseAdapter;

        private bool _resolved;
        private bool _tickablesRegistered;

        public EmbodimentDependencyResolver(EmbodimentContext owner)
        {
            _owner = owner;
        }

        public ConvaiCharacter Character => _character;
        public FacialBlendshapeCompositorHost Compositor => _compositor;
        public AnimatorConductor AnimatorConductor => _animatorConductor;
        public EmbodimentTickScheduler TickScheduler => _tickScheduler;
        public IStandardRigBinding RigBinding => _rigBinding;
        public IDialoguePhaseProvider DialoguePhase => _dialoguePhaseAdapter;

        public void ResolveOptionalComponents(ConvaiFacialCompositionProfile facialOverride)
        {
            if (_resolved) return;
            _resolved = true;

            if (_character == null)
                _character = _owner.GetComponentInChildren<ConvaiCharacter>(true);
            if (_compositor == null)
                _compositor = _owner.GetComponentInChildren<FacialBlendshapeCompositorHost>(true);
            if (_compositor != null)
                ApplyFacialCompositionProfile(facialOverride);
            if (_animatorConductor == null)
                _animatorConductor = _owner.GetComponentInChildren<AnimatorConductor>(true);
            if (_tickScheduler == null)
                _tickScheduler = _owner.GetComponentInChildren<EmbodimentTickScheduler>(true);
            if (_rigBinding == null)
                _rigBinding = _owner.GetComponentInChildren<StandardRigBinding>(true);
            if (_dialoguePhaseAdapter == null && _compositor != null)
                _dialoguePhaseAdapter = _compositor.GetComponent<CompositorDialoguePhaseAdapter>();

            RegisterActiveTickablesIfPossible();
        }

        public FacialBlendshapeCompositorHost EnsureCompositor(ConvaiFacialCompositionProfile facialOverride)
        {
            if (_compositor == null)
                _compositor = _owner.GetComponentInChildren<FacialBlendshapeCompositorHost>(true);
            if (_compositor == null && UnityEngine.Application.isPlaying)
                _compositor = FacialBlendshapeCompositorHost.GetOrCreate(_owner);
            if (_compositor != null)
                ApplyFacialCompositionProfile(facialOverride);
            return _compositor;
        }

        public AnimatorConductor EnsureAnimatorConductor()
        {
            if (_animatorConductor == null)
                _animatorConductor = _owner.GetComponentInChildren<AnimatorConductor>(true);
            if (_animatorConductor == null && UnityEngine.Application.isPlaying)
                _animatorConductor = AnimatorConductor.GetOrCreate(_owner);
            return _animatorConductor;
        }

        public EmbodimentTickScheduler EnsureTickScheduler()
        {
            if (_tickScheduler == null)
                _tickScheduler = _owner.GetComponentInChildren<EmbodimentTickScheduler>(true);
            if (_tickScheduler == null && UnityEngine.Application.isPlaying)
                _tickScheduler = EmbodimentTickScheduler.GetOrCreate(_owner);
            return _tickScheduler;
        }

        public IStandardRigBinding EnsureRigBinding()
        {
            if (_rigBinding == null)
                _rigBinding = _owner.GetComponentInChildren<StandardRigBinding>(true);
            if (_rigBinding == null && UnityEngine.Application.isPlaying)
            {
                _rigBinding = _owner.gameObject.AddComponent<StandardRigBinding>();
                _rigBinding.hideFlags = EmbodimentContext.RuntimeInfrastructureHideFlags();
            }
            return _rigBinding;
        }

        public IDialoguePhaseProvider EnsureDialoguePhase(ConvaiFacialCompositionProfile facialOverride)
        {
            if (_dialoguePhaseAdapter != null) return _dialoguePhaseAdapter;

            FacialBlendshapeCompositorHost compositor = EnsureCompositor(facialOverride);
            if (compositor == null) return null;

            _dialoguePhaseAdapter = compositor.GetComponent<CompositorDialoguePhaseAdapter>();
            if (_dialoguePhaseAdapter == null && UnityEngine.Application.isPlaying)
            {
                _dialoguePhaseAdapter = compositor.gameObject.AddComponent<CompositorDialoguePhaseAdapter>();
                _dialoguePhaseAdapter.hideFlags = EmbodimentContext.RuntimeInfrastructureHideFlags();
            }
            return _dialoguePhaseAdapter;
        }

        public void NotifyRigBindingChanged(IStandardRigBinding incoming)
        {
            if (incoming is StandardRigBinding concrete)
                _rigBinding = concrete;
            else if (_rigBinding == null)
                _rigBinding = _owner.GetComponentInChildren<StandardRigBinding>(true);

            _animatorConductor?.RefreshAnimator();
        }

        public void ApplyFacialCompositionProfile(ConvaiFacialCompositionProfile overrideProfile)
        {
            if (_compositor == null) return;

            if (overrideProfile != null)
            {
                _compositor.SetCompositionProfile(overrideProfile);
                return;
            }

            _compositor.EnsureDefaultProfileLoaded();
        }

        private void RegisterActiveTickablesIfPossible()
        {
            if (_tickablesRegistered) return;
            _tickablesRegistered = true;

            MonoBehaviour[] components = _owner.GetComponentsInChildren<MonoBehaviour>(true);
            EmbodimentTickScheduler scheduler = _tickScheduler;
            for (int i = 0; i < components.Length; i++)
            {
                MonoBehaviour behaviour = components[i];
                if (behaviour == null || !behaviour.isActiveAndEnabled) continue;
                if (behaviour is IEmbodimentTickable tickable)
                {
                    scheduler ??= EnsureTickScheduler();
                    scheduler?.Register(tickable);
                }
            }
        }
    }
}
