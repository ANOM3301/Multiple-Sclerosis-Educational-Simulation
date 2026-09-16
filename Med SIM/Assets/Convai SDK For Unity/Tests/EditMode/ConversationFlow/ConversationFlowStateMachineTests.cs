using Convai.Domain.Embodiment.Readings;
using Convai.Domain.Embodiment.Semantics;
using Convai.Modules.ConversationFlow.Core;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Convai.Tests.EditMode.ConversationFlow
{
    [TestFixture]
    public sealed class ConversationFlowStateMachineTests
    {
        [TearDown]
        public void TearDown() => LogAssert.NoUnexpectedReceived();

        private static ConversationFlowTimings Timings(
            float transitionDuration = 0.25f,
            float thinkingMinHold = 0.25f,
            float thinkingMaxHold = 2.5f,
            float attendingGracePeriod = 0.3f,
            float settlingDuration = 0.6f,
            float idleReturnDelay = 60f,
            float interruptedFreezeDuration = 0.25f,
            float speakingBaseEnergy = 0.6f)
            => new(
                transitionDuration,
                thinkingMinHold,
                thinkingMaxHold,
                attendingGracePeriod,
                settlingDuration,
                idleReturnDelay,
                interruptedFreezeDuration,
                speakingBaseEnergy);

        private static ConversationFlowInputs Inputs(
            bool ready = true,
            bool playerSpeaking = false,
            bool pendingTurn = false,
            bool characterSpeaking = false,
            bool lipSync = false,
            bool interrupted = false,
            bool turnCompleted = false)
            => new(ready, playerSpeaking, pendingTurn, characterSpeaking, lipSync, interrupted, turnCompleted);

        [Test]
        public void Initial_StateIsIdle()
        {
            var sm = new ConversationFlowStateMachine();
            Assert.That(sm.Current.Primary, Is.EqualTo(DialogueState.Idle));
        }

        [Test]
        public void NotReady_ForcesIdle_EvenWhenPlayerSpeaks()
        {
            var sm = new ConversationFlowStateMachine();
            DialogueStateReading r = sm.Tick(Inputs(ready: false, playerSpeaking: true), Timings(), 0.1f);
            Assert.That(r.Primary, Is.EqualTo(DialogueState.Idle));
        }

        [Test]
        public void NoInteraction_KeepsStateIdle()
        {
            var sm = new ConversationFlowStateMachine();

            for (int i = 0; i < 20; i++)
                sm.Tick(Inputs(), Timings(), 0.1f);

            Assert.That(sm.Current.Primary, Is.EqualTo(DialogueState.Idle));
        }

        [Test]
        public void PlayerSpeaking_EntersListening()
        {
            var sm = new ConversationFlowStateMachine();
            DialogueStateReading r = sm.Tick(Inputs(playerSpeaking: true), Timings(), 0.1f);
            Assert.That(r.Primary, Is.EqualTo(DialogueState.Listening));
        }

        [Test]
        public void CharacterSpeaking_EntersSpeaking_OverridesOthers()
        {
            var sm = new ConversationFlowStateMachine();
            DialogueStateReading r = sm.Tick(
                Inputs(playerSpeaking: true, characterSpeaking: true),
                Timings(), 0.1f);
            Assert.That(r.Primary, Is.EqualTo(DialogueState.Speaking));
        }

        [Test]
        public void PlayerStopsWithPendingTurn_EntersThinking()
        {
            var sm = new ConversationFlowStateMachine();
            sm.Tick(Inputs(playerSpeaking: true), Timings(), 0.1f);
            DialogueStateReading r = sm.Tick(Inputs(pendingTurn: true), Timings(), 0.1f);
            Assert.That(r.Primary, Is.EqualTo(DialogueState.Thinking));
        }

        [Test]
        public void ThinkingTimeout_FallsBackToAttending()
        {
            var sm = new ConversationFlowStateMachine();
            ConversationFlowTimings t = Timings();

            sm.Tick(Inputs(pendingTurn: true), t, 0.1f);
            for (int i = 0; i < 40; i++)
                sm.Tick(Inputs(pendingTurn: true), t, 0.1f);

            Assert.That(sm.Current.Primary, Is.EqualTo(DialogueState.Attending));
        }

        [Test]
        public void ThinkingTimeout_DoesNotBounceBackToThinking_WhilePendingTurnRemains()
        {
            var sm = new ConversationFlowStateMachine();
            ConversationFlowTimings t = Timings(thinkingMaxHold: 0.3f);

            sm.Tick(Inputs(pendingTurn: true), t, 0.1f);
            for (int i = 0; i < 8; i++)
                sm.Tick(Inputs(pendingTurn: true), t, 0.1f);

            Assert.That(sm.Current.Primary, Is.EqualTo(DialogueState.Attending));
        }

        [Test]
        public void ThinkingMinHold_DelaysTransitionIntoSpeaking_WhenReplyStartsImmediately()
        {
            var sm = new ConversationFlowStateMachine();
            ConversationFlowTimings t = Timings(thinkingMinHold: 0.35f, thinkingMaxHold: 1.5f);

            sm.Tick(Inputs(pendingTurn: true), t, 0.05f);
            DialogueStateReading heldThinking = sm.Tick(
                Inputs(pendingTurn: true, characterSpeaking: true),
                t,
                0.1f);

            Assert.That(heldThinking.Primary, Is.EqualTo(DialogueState.Thinking));

            DialogueStateReading speaking = sm.Tick(
                Inputs(characterSpeaking: true),
                t,
                0.3f);

            Assert.That(speaking.Primary, Is.EqualTo(DialogueState.Speaking));
        }

        [Test]
        public void CharacterSpeechEnds_EntersSettling_ThenAttending_ThenIdle()
        {
            var sm = new ConversationFlowStateMachine();
            ConversationFlowTimings t = Timings(settlingDuration: 0.3f, idleReturnDelay: 0.8f);

            sm.Tick(Inputs(characterSpeaking: true), t, 0.1f);
            DialogueStateReading settling = sm.Tick(Inputs(turnCompleted: true), t, 0.1f);
            Assert.That(settling.Primary, Is.EqualTo(DialogueState.Settling));

            for (int i = 0; i < 4; i++)
                sm.Tick(Inputs(), t, 0.1f);

            Assert.That(sm.Current.Primary, Is.EqualTo(DialogueState.Attending));

            for (int i = 0; i < 10; i++)
                sm.Tick(Inputs(), t, 0.1f);

            Assert.That(sm.Current.Primary, Is.EqualTo(DialogueState.Idle));
        }

        [Test]
        public void PlayerStopsSpeaking_RemainsAttending_UntilIdleTimeout()
        {
            var sm = new ConversationFlowStateMachine();
            ConversationFlowTimings t = Timings(attendingGracePeriod: 0.2f, idleReturnDelay: 0.8f);

            sm.Tick(Inputs(playerSpeaking: true), t, 0.1f);
            sm.Tick(Inputs(), t, 0.1f);
            sm.Tick(Inputs(), t, 0.2f);

            Assert.That(sm.Current.Primary, Is.EqualTo(DialogueState.Attending));

            for (int i = 0; i < 8; i++)
                sm.Tick(Inputs(), t, 0.1f);

            Assert.That(sm.Current.Primary, Is.EqualTo(DialogueState.Idle));
        }

        [Test]
        public void Interrupted_FreezesThenTransitionsToAttending()
        {
            var sm = new ConversationFlowStateMachine();
            ConversationFlowTimings t = Timings();

            sm.Tick(Inputs(characterSpeaking: true), t, 0.1f);
            DialogueStateReading frozen = sm.Tick(
                Inputs(turnCompleted: true, interrupted: true), t, 0.05f);
            Assert.That(frozen.Primary, Is.EqualTo(DialogueState.Interrupted));

            for (int i = 0; i < 15; i++)
                sm.Tick(Inputs(interrupted: true), t, 0.1f);

            Assert.That(sm.Current.Primary, Is.EqualTo(DialogueState.Attending));
        }

        [Test]
        public void InterruptedTurn_CoolsBackToIdle_WhenLatchExpiresAndSessionGoesQuiet()
        {
            var sm = new ConversationFlowStateMachine();
            ConversationFlowTimings t = Timings(
                interruptedFreezeDuration: 0.2f,
                idleReturnDelay: 0.35f);

            sm.Tick(Inputs(characterSpeaking: true), t, 0.1f);
            sm.Tick(Inputs(turnCompleted: true, interrupted: true), t, 0.05f);
            sm.Tick(Inputs(), t, 0.25f);

            Assert.That(sm.Current.Primary, Is.EqualTo(DialogueState.Attending));

            sm.Tick(Inputs(), t, 0.4f);

            Assert.That(sm.Current.Primary, Is.EqualTo(DialogueState.Idle));
        }

        [Test]
        public void Changed_Event_FiresOnPrimaryTransitions()
        {
            var sm = new ConversationFlowStateMachine();
            int changes = 0;
            DialogueState last = DialogueState.Idle;
            sm.Changed += r => { changes++; last = r.Primary; };

            sm.Tick(Inputs(playerSpeaking: true), Timings(), 0.1f);

            Assert.That(changes, Is.EqualTo(1));
            Assert.That(last, Is.EqualTo(DialogueState.Listening));
        }

        [Test]
        public void Pulse_ReactingOverridesDerivedState_UntilDurationElapses()
        {
            var sm = new ConversationFlowStateMachine();
            ConversationFlowTimings t = Timings();

            sm.Pulse(DialogueState.Reacting, 0.3f);
            DialogueStateReading r = sm.Tick(Inputs(), t, 0.05f);
            Assert.That(r.Primary, Is.EqualTo(DialogueState.Reacting));

            for (int i = 0; i < 10; i++)
                sm.Tick(Inputs(), t, 0.1f);

            Assert.That(sm.Current.Primary, Is.Not.EqualTo(DialogueState.Reacting));
        }

        [Test]
        public void Reset_RestoresIdleAndClearsBlend()
        {
            var sm = new ConversationFlowStateMachine();
            sm.Tick(Inputs(playerSpeaking: true), Timings(), 0.1f);
            sm.Reset();
            Assert.That(sm.Current.Primary, Is.EqualTo(DialogueState.Idle));
            Assert.That(sm.Current.BlendWeight, Is.EqualTo(0f));
        }

        // ── new cases (+4) ────────────────────────────────────────────────────────────

        [Test]
        public void BlendWeight_DuringTransition_IsNonZeroBeforeFullCommit()
        {
            // Arrange
            var sm = new ConversationFlowStateMachine();
            ConversationFlowTimings t = Timings(transitionDuration: 0.5f);

            // Act — one small tick into a new state; blend should be partial
            DialogueStateReading r = sm.Tick(Inputs(playerSpeaking: true), t, 0.01f);

            // Assert — transition has just started; blend weight must be between 0 and 1
            Assert.That(r.BlendWeight, Is.GreaterThanOrEqualTo(0f));
            Assert.That(r.BlendWeight, Is.LessThanOrEqualTo(1f));
        }

        [Test]
        public void ThinkingMinHold_BlocksTransitionToSpeaking_UntilThresholdReached()
        {
            // Arrange
            var sm = new ConversationFlowStateMachine();
            ConversationFlowTimings t = Timings(thinkingMinHold: 0.5f, thinkingMaxHold: 2f);

            // Enter Thinking state
            sm.Tick(Inputs(pendingTurn: true), t, 0.1f);

            // Act — send character-speaking before min hold expires (0.1s < 0.5s)
            DialogueStateReading held = sm.Tick(
                Inputs(pendingTurn: true, characterSpeaking: true), t, 0.1f);

            // Assert — must still be in Thinking, not Speaking
            Assert.That(held.Primary, Is.EqualTo(DialogueState.Thinking),
                "Min-hold must prevent Speaking transition before threshold.");

            // Now advance past the min hold
            DialogueStateReading released = sm.Tick(
                Inputs(characterSpeaking: true), t, 0.5f);

            // Assert — now may transition to Speaking
            Assert.That(released.Primary, Is.EqualTo(DialogueState.Speaking),
                "After min-hold elapsed, Speaking transition must occur.");
        }

        [Test]
        public void Interrupted_DoesNotRequireMinHold_TransitionsFast()
        {
            // Arrange
            var sm = new ConversationFlowStateMachine();
            ConversationFlowTimings t = Timings(
                thinkingMinHold: 10f,
                interruptedFreezeDuration: 0.1f);

            // Put machine into Speaking
            sm.Tick(Inputs(characterSpeaking: true), t, 0.1f);

            // Act — interrupt immediately
            DialogueStateReading frozen = sm.Tick(
                Inputs(turnCompleted: true, interrupted: true), t, 0.05f);

            // Assert — must enter Interrupted regardless of any min-hold
            Assert.That(frozen.Primary, Is.EqualTo(DialogueState.Interrupted),
                "Interrupt transition must bypass min-hold.");
        }

        [Test]
        public void Determinism_SameInputsProduceSameStates_Over100Ticks()
        {
            // Arrange
            ConversationFlowTimings t = Timings();
            bool[] speakingPattern =
            {
                false, true, true, true, false, false, false, false, false, false
            };

            DialogueState[] run1 = new DialogueState[100];
            DialogueState[] run2 = new DialogueState[100];

            // Act
            var sm1 = new ConversationFlowStateMachine();
            for (int i = 0; i < 100; i++)
            {
                bool ps = speakingPattern[i % speakingPattern.Length];
                run1[i] = sm1.Tick(Inputs(playerSpeaking: ps), t, 0.05f).Primary;
            }

            var sm2 = new ConversationFlowStateMachine();
            for (int i = 0; i < 100; i++)
            {
                bool ps = speakingPattern[i % speakingPattern.Length];
                run2[i] = sm2.Tick(Inputs(playerSpeaking: ps), t, 0.05f).Primary;
            }

            // Assert
            Assert.That(run1, Is.EqualTo(run2),
                "Identical input sequences must produce identical state sequences.");
        }
    }
}
