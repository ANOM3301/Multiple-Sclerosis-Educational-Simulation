using Convai.Domain.Models;
using NUnit.Framework;

namespace Convai.Tests.EditMode.Domain
{
    /// <summary>
    ///     Unit tests for <see cref="SpeakerInfo" /> struct.
    /// </summary>
    public class SpeakerInfoTests
    {
        [Test]
        public void Constructor_Sets_All_Fields()
        {
            var info = new SpeakerInfo(
                "speaker-123",
                "John",
                "PA_xyz"
            );

            Assert.AreEqual("speaker-123", info.SpeakerId);
            Assert.AreEqual("John", info.SpeakerName);
            Assert.AreEqual("PA_xyz", info.ParticipantId);
            Assert.AreEqual(SpeakerType.Player, info.SpeakerType);
        }

        [Test]
        public void Constructor_Handles_Null_Values()
        {
            var info = new SpeakerInfo(null, null, null);

            Assert.AreEqual(string.Empty, info.SpeakerId);
            Assert.AreEqual(string.Empty, info.SpeakerName);
            Assert.AreEqual(string.Empty, info.ParticipantId);
        }

        [Test]
        public void IsValid_Returns_True_When_SpeakerId_Set()
        {
            var info = new SpeakerInfo("speaker-123", null, null);

            Assert.IsTrue(info.IsValid);
        }

        [Test]
        public void IsValid_Returns_True_When_SpeakerName_Set()
        {
            var info = new SpeakerInfo(null, "John", null);

            Assert.IsTrue(info.IsValid);
        }

        [Test]
        public void IsValid_Returns_False_When_Both_Empty()
        {
            var info = new SpeakerInfo(null, null, null);

            Assert.IsFalse(info.IsValid);
        }

        [Test]
        public void DefaultPlayer_Returns_Valid_Default()
        {
            var info = SpeakerInfo.DefaultPlayer;

            Assert.AreEqual("local-player", info.SpeakerId);
            Assert.AreEqual("You", info.SpeakerName);
            Assert.AreEqual(SpeakerType.Player, info.SpeakerType);
            Assert.IsTrue(info.IsValid);
        }

        [Test]
        public void Empty_Returns_Invalid_SpeakerInfo()
        {
            var info = SpeakerInfo.Empty;

            Assert.IsFalse(info.IsValid);
            Assert.AreEqual(string.Empty, info.SpeakerId);
            Assert.AreEqual(string.Empty, info.SpeakerName);
        }

        [Test]
        public void IsDefaultPlayer_Returns_True_For_LocalPlayer()
        {
            var info = new SpeakerInfo("local-player", "You", null);

            Assert.IsTrue(info.IsDefaultPlayer);
        }

        [Test]
        public void IsDefaultPlayer_Returns_True_For_Empty_SpeakerId()
        {
            var info = new SpeakerInfo(null, "Some Name", null);

            Assert.IsTrue(info.IsDefaultPlayer);
        }

        [Test]
        public void IsDefaultPlayer_Returns_False_For_Custom_SpeakerId()
        {
            var info = new SpeakerInfo("custom-speaker-id", "John", null);

            Assert.IsFalse(info.IsDefaultPlayer);
        }

        [Test]
        public void GetDisplayName_Returns_SpeakerName_When_Set()
        {
            var info = new SpeakerInfo("speaker-123", "John", null);

            Assert.AreEqual("John", info.GetDisplayName());
        }

        [Test]
        public void GetDisplayName_Returns_SpeakerId_When_Name_Empty()
        {
            var info = new SpeakerInfo("speaker-123", null, null);

            Assert.AreEqual("speaker-123", info.GetDisplayName());
        }

        [Test]
        public void GetDisplayName_Returns_Unknown_When_Both_Empty()
        {
            var info = new SpeakerInfo(null, null, null);

            Assert.AreEqual("Unknown", info.GetDisplayName());
        }

        [Test]
        public void FromMessage_Creates_SpeakerInfo_From_TranscriptMessage()
        {
            TranscriptMessage message = TranscriptMessage.ForPlayer(
                "Hello",
                true,
                "speaker-123",
                "John",
                "PA_xyz"
            );

            SpeakerInfo info = SpeakerInfo.FromMessage(message);

            Assert.AreEqual("speaker-123", info.SpeakerId);
            Assert.AreEqual("John", info.SpeakerName);
            Assert.AreEqual("PA_xyz", info.ParticipantId);
            Assert.AreEqual(SpeakerType.Player, info.SpeakerType);
        }

        [Test]
        public void ToString_Returns_Formatted_String()
        {
            var info = new SpeakerInfo("speaker-123", "John", "PA_xyz");

            string result = info.ToString();

            Assert.IsTrue(result.Contains("Player"));
            Assert.IsTrue(result.Contains("John"));
            Assert.IsTrue(result.Contains("speaker-123"));
        }
    }
}
