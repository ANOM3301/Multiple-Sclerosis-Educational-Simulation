using System;
using Convai.Infrastructure.Networking.Models;
using NUnit.Framework;

namespace Convai.Tests.EditMode
{
    [TestFixture]
    public class ConnectionContextTests
    {
        [Test]
        public void Empty_HasNoValidRoom()
        {
            var context = ConnectionContext.Empty;
            Assert.IsFalse(context.HasValidRoom, "Empty context should not have a valid room.");
            Assert.IsFalse(context.CanResumeSession, "Empty context should not be able to resume session.");
        }

        [Test]
        public void Constructor_SetsPropertiesCorrectly()
        {
            DateTime connectedAt = DateTime.UtcNow;
            var context = new ConnectionContext(
                "test-room",
                "char-session-123",
                "session-456",
                "character-1",
                connectedAt);

            Assert.AreEqual("test-room", context.RoomName);
            Assert.AreEqual("char-session-123", context.CharacterSessionId);
            Assert.AreEqual("session-456", context.SessionId);
            Assert.AreEqual("character-1", context.CharacterId);
            Assert.AreEqual(connectedAt, context.ConnectedAtUtc);
            Assert.IsNull(context.DisconnectedAtUtc);
            Assert.IsTrue(context.HasValidRoom);
            Assert.IsTrue(context.CanResumeSession);
        }

        [Test]
        public void WithDisconnection_CreatesNewContextWithTimestamp()
        {
            DateTime connectedAt = DateTime.UtcNow.AddMinutes(-5);
            DateTime disconnectedAt = DateTime.UtcNow;
            var original = new ConnectionContext("room", "sess", "sid", "char", connectedAt);
            ConnectionContext withDisconnect = original.WithDisconnection(disconnectedAt);

            Assert.AreEqual("room", withDisconnect.RoomName);
            Assert.AreEqual(disconnectedAt, withDisconnect.DisconnectedAtUtc);
            Assert.IsNull(original.DisconnectedAtUtc, "Original should not be modified.");
        }

        [Test]
        public void IsRoomValidForRejoin_WithinTTL_ReturnsTrue()
        {
            DateTime connectedAt = DateTime.UtcNow.AddMinutes(-5);
            DateTime disconnectedAt = DateTime.UtcNow.AddSeconds(-30);
            var context = new ConnectionContext("room", "sess", "sid", "char", connectedAt, disconnectedAt);

            Assert.IsTrue(context.IsRoomValidForRejoin(60), "Room should be valid for rejoin within 60s TTL.");
        }

        [Test]
        public void IsRoomValidForRejoin_ExpiredTTL_ReturnsFalse()
        {
            DateTime connectedAt = DateTime.UtcNow.AddMinutes(-5);
            DateTime disconnectedAt = DateTime.UtcNow.AddSeconds(-120);
            var context = new ConnectionContext("room", "sess", "sid", "char", connectedAt, disconnectedAt);

            Assert.IsFalse(context.IsRoomValidForRejoin(60), "Room should not be valid for rejoin after TTL expired.");
        }

        [Test]
        public void IsRoomValidForRejoin_NoDisconnectTimestamp_ReturnsFalse()
        {
            var context = new ConnectionContext("room", "sess", "sid", "char", DateTime.UtcNow);
            Assert.IsFalse(context.IsRoomValidForRejoin(60),
                "Room should not be valid for rejoin without disconnect timestamp.");
        }

        [Test]
        public void IsRoomValidForRejoin_NoValidRoom_ReturnsFalse()
        {
            var context = new ConnectionContext(null, "sess", "sid", "char", DateTime.UtcNow, DateTime.UtcNow);
            Assert.IsFalse(context.IsRoomValidForRejoin(60), "Should return false when no valid room.");
        }
    }

    [TestFixture]
    public class ReconnectPolicyTests
    {
        [Test]
        public void Default_HasSensibleDefaults()
        {
            var policy = ReconnectPolicy.Default;

            Assert.AreEqual(60.0, policy.RoomRejoinTtlSeconds);
            Assert.AreEqual(ResumePolicy.ResumeIfPossible, policy.ResumePolicy);
            Assert.AreEqual(3, policy.MaxReconnectAttempts);
            Assert.IsTrue(policy.SpawnAgentOnRejoin);
        }

        [Test]
        public void AlwaysCreateNew_HasZeroTTLAndAlwaysFresh()
        {
            ReconnectPolicy policy = ReconnectPolicy.AlwaysCreateNew;

            Assert.AreEqual(0, policy.RoomRejoinTtlSeconds);
            Assert.AreEqual(ResumePolicy.AlwaysFresh, policy.ResumePolicy);
        }

        [Test]
        public void Constructor_CustomValues_ArePreserved()
        {
            var policy = new ReconnectPolicy(
                120,
                ResumePolicy.AlwaysResume,
                5,
                false,
                10000,
                1.0f);

            Assert.AreEqual(120, policy.RoomRejoinTtlSeconds);
            Assert.AreEqual(ResumePolicy.AlwaysResume, policy.ResumePolicy);
            Assert.AreEqual(5, policy.MaxReconnectAttempts);
            Assert.IsFalse(policy.SpawnAgentOnRejoin);
            Assert.AreEqual(10000, policy.StartWaitTimeoutMs);
            Assert.AreEqual(1.0f, policy.AutoMicStartDelaySeconds, 0.001f);
        }

        [Test]
        public void Default_HasCorrectTimeoutDefaults()
        {
            var policy = ReconnectPolicy.Default;

            Assert.AreEqual(5000, policy.StartWaitTimeoutMs);
            Assert.AreEqual(0.5f, policy.AutoMicStartDelaySeconds, 0.001f);
        }
    }

    [TestFixture]
    public class RoomJoinOptionsTests
    {
        [Test]
        public void IsJoinRequest_WithRoomName_ReturnsTrue()
        {
            var options = new RoomJoinOptions("my-room");
            Assert.IsTrue(options.IsJoinRequest);
        }

        [Test]
        public void IsJoinRequest_WithoutRoomName_ReturnsFalse()
        {
            var options = RoomJoinOptions.CreateNew();
            Assert.IsFalse(options.IsJoinRequest);
        }

        [Test]
        public void FromContext_ValidRoomWithinTTL_ReturnsJoinRequest()
        {
            DateTime disconnectedAt = DateTime.UtcNow.AddSeconds(-30);
            var context = new ConnectionContext("room", "sess", "sid", "char", DateTime.UtcNow.AddMinutes(-5),
                disconnectedAt);
            var policy = ReconnectPolicy.Default;

            RoomJoinOptions options = RoomJoinOptions.FromContext(context, policy);

            Assert.IsTrue(options.IsJoinRequest);
            Assert.AreEqual("room", options.RoomName);
            Assert.AreEqual("sess", options.CharacterSessionId);
        }

        [Test]
        public void FromContext_ExpiredRoom_ReturnsCreateNew()
        {
            DateTime disconnectedAt = DateTime.UtcNow.AddSeconds(-120);
            var context = new ConnectionContext("room", "sess", "sid", "char", DateTime.UtcNow.AddMinutes(-5),
                disconnectedAt);
            var policy = ReconnectPolicy.Default;

            RoomJoinOptions options = RoomJoinOptions.FromContext(context, policy);

            Assert.IsFalse(options.IsJoinRequest, "Expired room should result in create new.");
            Assert.IsNull(options.RoomName);
            Assert.AreEqual("sess", options.CharacterSessionId);
        }

        [Test]
        public void FromContext_NullContext_ReturnsCreateNew()
        {
            RoomJoinOptions options = RoomJoinOptions.FromContext(null, ReconnectPolicy.Default);
            Assert.IsFalse(options.IsJoinRequest);
        }

        [Test]
        public void FromContext_AlwaysFreshPolicy_DoesNotIncludeSessionId()
        {
            DateTime disconnectedAt = DateTime.UtcNow.AddSeconds(-30);
            var context = new ConnectionContext("room", "sess", "sid", "char", DateTime.UtcNow.AddMinutes(-5),
                disconnectedAt);
            ReconnectPolicy policy = ReconnectPolicy.AlwaysCreateNew;

            RoomJoinOptions options = RoomJoinOptions.FromContext(context, policy);

            Assert.IsNull(options.CharacterSessionId, "AlwaysFresh policy should not include session ID.");
        }

        [Test]
        public void FromContext_SpawnAgentOnRejoin_IsRespected()
        {
            DateTime disconnectedAt = DateTime.UtcNow.AddSeconds(-30);
            var context = new ConnectionContext("room", "sess", "sid", "char", DateTime.UtcNow.AddMinutes(-5),
                disconnectedAt);
            var policyWithSpawn = new ReconnectPolicy(spawnAgentOnRejoin: true);
            var policyNoSpawn = new ReconnectPolicy(spawnAgentOnRejoin: false);

            RoomJoinOptions optionsWithSpawn = RoomJoinOptions.FromContext(context, policyWithSpawn);
            RoomJoinOptions optionsNoSpawn = RoomJoinOptions.FromContext(context, policyNoSpawn);

            Assert.IsTrue(optionsWithSpawn.SpawnAgent);
            Assert.IsFalse(optionsNoSpawn.SpawnAgent);
        }

        [Test]
        public void CreateNew_SetsCorrectDefaults()
        {
            var options = RoomJoinOptions.CreateNew("session-123", 10);

            Assert.IsFalse(options.IsJoinRequest);
            Assert.AreEqual("session-123", options.CharacterSessionId);
            Assert.AreEqual(10, options.MaxNumParticipants);
            Assert.IsTrue(options.SpawnAgent);
        }
    }
}
