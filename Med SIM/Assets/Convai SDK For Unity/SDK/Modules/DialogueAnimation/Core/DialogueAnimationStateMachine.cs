using Convai.Domain.Embodiment.Semantics;
using Convai.Modules.DialogueAnimation.Runtime.Driver;

namespace Convai.Modules.DialogueAnimation.Core
{
    /// <summary>
    ///     Pure-logic state tracker for the dialogue animation pipeline. Owns the "last
    ///     observed primary state" plus the active talk session record so the orchestrator can
    ///     stay free of mutable bookkeeping. Edit-mode testable; no Unity dependencies.
    /// </summary>
    internal sealed class DialogueAnimationStateMachine
    {
        private DialogueState _lastPrimary = DialogueState.Idle;
        private DialogueTalkSession _activeTalk = DialogueTalkSession.Empty;

        /// <summary>The most recently processed primary dialogue state.</summary>
        public DialogueState LastPrimary => _lastPrimary;

        /// <summary>Talk session currently being driven on the talk animator layers.</summary>
        public ref readonly DialogueTalkSession ActiveTalk => ref _activeTalk;

        /// <summary>Whether the controller is currently driving talk layers.</summary>
        public bool IsTalking => IsTalkingState(_lastPrimary);

        /// <summary>Resets the machine, optionally seeding the last-primary value.</summary>
        public void Reset(DialogueState initial = DialogueState.Idle)
        {
            _lastPrimary = initial;
            _activeTalk = DialogueTalkSession.Empty;
        }

        /// <summary>
        ///     Folds <paramref name="current" /> into the machine and reports the resulting
        ///     transition relative to the previous tick.
        /// </summary>
        public DialogueAnimationTransition Process(DialogueState current)
        {
            if (current == _lastPrimary) return DialogueAnimationTransition.None;

            bool wasTalking = IsTalkingState(_lastPrimary);
            bool nowTalking = IsTalkingState(current);
            _lastPrimary = current;

            if (!wasTalking && nowTalking) return DialogueAnimationTransition.StartTalk;
            if (wasTalking && !nowTalking) return DialogueAnimationTransition.StopTalk;
            return DialogueAnimationTransition.None;
        }

        /// <summary>Records the talk session that the variant starter produced.</summary>
        public void SetActiveTalk(in DialogueTalkSession session) => _activeTalk = session;

        /// <summary>Clears any tracked talk session (e.g. on teardown).</summary>
        public void ClearActiveTalk() => _activeTalk = DialogueTalkSession.Empty;

        /// <summary>
        ///     The semantic talk-state predicate shared by the orchestrator and writer to decide
        ///     when to crossfade between idle and talk layers.
        /// </summary>
        public static bool IsTalkingState(DialogueState state) =>
            state == DialogueState.Speaking || state == DialogueState.Reacting;
    }

    /// <summary>Result of a single state-machine fold.</summary>
    internal enum DialogueAnimationTransition
    {
        None,
        StartTalk,
        StopTalk
    }
}
