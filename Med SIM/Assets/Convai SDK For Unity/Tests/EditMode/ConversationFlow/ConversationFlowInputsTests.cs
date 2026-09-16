using Convai.Modules.ConversationFlow.Core;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Convai.Tests.EditMode.ConversationFlow
{
    /// <summary>
    ///     Contract tests for the <see cref="ConversationFlowInputs" /> value type:
    ///     field semantics, default state, and value equality.
    /// </summary>
    [TestFixture]
    public sealed class ConversationFlowInputsTests
    {
        [TearDown]
        public void TearDown() => LogAssert.NoUnexpectedReceived();

        [Test]
        public void Default_AllFieldsAreFalse()
        {
            // Arrange / Act
            ConversationFlowInputs inputs = default;

            // Assert
            Assert.That(inputs.IsCharacterReady, Is.False);
            Assert.That(inputs.IsPlayerSpeaking, Is.False);
            Assert.That(inputs.HasPendingPlayerTurn, Is.False);
            Assert.That(inputs.IsCharacterSpeaking, Is.False);
            Assert.That(inputs.IsLipSyncSpeaking, Is.False);
            Assert.That(inputs.WasRecentlyInterrupted, Is.False);
            Assert.That(inputs.TurnJustCompleted, Is.False);
        }

        [Test]
        public void Constructor_SetsAllFields_Correctly()
        {
            // Arrange / Act
            var inputs = new ConversationFlowInputs(
                isCharacterReady: true,
                isPlayerSpeaking: true,
                hasPendingPlayerTurn: true,
                isCharacterSpeaking: false,
                isLipSyncSpeaking: true,
                wasRecentlyInterrupted: true,
                turnJustCompleted: false);

            // Assert
            Assert.That(inputs.IsCharacterReady, Is.True);
            Assert.That(inputs.IsPlayerSpeaking, Is.True);
            Assert.That(inputs.HasPendingPlayerTurn, Is.True);
            Assert.That(inputs.IsCharacterSpeaking, Is.False);
            Assert.That(inputs.IsLipSyncSpeaking, Is.True);
            Assert.That(inputs.WasRecentlyInterrupted, Is.True);
            Assert.That(inputs.TurnJustCompleted, Is.False);
        }

        [Test]
        public void TwoInstances_WithIdenticalValues_AreStructurallyEqual()
        {
            // Arrange
            var a = new ConversationFlowInputs(true, false, true, false, false, false, true);
            var b = new ConversationFlowInputs(true, false, true, false, false, false, true);

            // Assert — readonly struct; no override of Equals, but field values match
            Assert.That(a.IsCharacterReady, Is.EqualTo(b.IsCharacterReady));
            Assert.That(a.IsPlayerSpeaking, Is.EqualTo(b.IsPlayerSpeaking));
            Assert.That(a.HasPendingPlayerTurn, Is.EqualTo(b.HasPendingPlayerTurn));
            Assert.That(a.IsCharacterSpeaking, Is.EqualTo(b.IsCharacterSpeaking));
            Assert.That(a.IsLipSyncSpeaking, Is.EqualTo(b.IsLipSyncSpeaking));
            Assert.That(a.WasRecentlyInterrupted, Is.EqualTo(b.WasRecentlyInterrupted));
            Assert.That(a.TurnJustCompleted, Is.EqualTo(b.TurnJustCompleted));
        }
    }
}
