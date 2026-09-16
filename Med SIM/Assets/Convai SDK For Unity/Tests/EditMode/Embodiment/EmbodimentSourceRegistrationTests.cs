using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Convai.Domain.Embodiment.Interfaces;
using Convai.Domain.Embodiment.Readings;
using Convai.Runtime.Embodiment;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Convai.Tests.EditMode.Embodiment
{
    public sealed class EmbodimentSourceRegistrationTests
    {
        // ── ConversationFlowSource ─────────────────────────────────────────────

        [Test]
        public void RegisterConversationFlowSource_FiresChangedEvent()
        {
            GameObject root = new("SrcReg_FlowRegister");
            try
            {
                EmbodimentContext ctx = root.AddComponent<EmbodimentContext>();
                IConversationFlowSource received = null;
                ctx.ConversationFlowSourceChanged += s => received = s;

                StubFlowSource source = new();
                ctx.RegisterConversationFlowSource(source);

                Assert.AreSame(source, received);
                Assert.AreSame(source, ctx.ConversationFlowSource);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void RegisterConversationFlowSource_SameSourceTwice_EventNotFiredSecondTime()
        {
            GameObject root = new("SrcReg_FlowDuplicateSame");
            try
            {
                EmbodimentContext ctx = root.AddComponent<EmbodimentContext>();
                int callCount = 0;
                ctx.ConversationFlowSourceChanged += _ => callCount++;

                StubFlowSource source = new();
                ctx.RegisterConversationFlowSource(source);
                ctx.RegisterConversationFlowSource(source);

                Assert.AreEqual(1, callCount, "Event must not fire on re-registration of same source");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void RegisterConversationFlowSource_DifferentSource_WarnsAndKeepsOriginal()
        {
            GameObject root = new("SrcReg_FlowDuplicateDiff");
            try
            {
                EmbodimentContext ctx = root.AddComponent<EmbodimentContext>();
                StubFlowSource first = new();
                StubFlowSource second = new();
                ctx.RegisterConversationFlowSource(first);

                LogAssert.Expect(LogType.Warning,
                    new Regex("Duplicate conversation flow source", RegexOptions.IgnoreCase));

                ctx.RegisterConversationFlowSource(second);

                Assert.AreSame(first, ctx.ConversationFlowSource, "Second source must not replace first");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void UnregisterConversationFlowSource_MatchingSource_ClearsAndFiresNull()
        {
            GameObject root = new("SrcReg_FlowUnregister");
            try
            {
                EmbodimentContext ctx = root.AddComponent<EmbodimentContext>();
                StubFlowSource source = new();
                ctx.RegisterConversationFlowSource(source);

                IConversationFlowSource received = source;
                ctx.ConversationFlowSourceChanged += s => received = s;

                ctx.UnregisterConversationFlowSource(source);

                Assert.IsNull(ctx.ConversationFlowSource);
                Assert.IsNull(received);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void UnregisterConversationFlowSource_NonMatchingSource_DoesNotClear()
        {
            GameObject root = new("SrcReg_FlowUnregisterWrong");
            try
            {
                EmbodimentContext ctx = root.AddComponent<EmbodimentContext>();
                StubFlowSource registered = new();
                StubFlowSource unrelated = new();
                ctx.RegisterConversationFlowSource(registered);

                ctx.UnregisterConversationFlowSource(unrelated);

                Assert.AreSame(registered, ctx.ConversationFlowSource);
            }
            finally { Object.DestroyImmediate(root); }
        }

        // ── AttentionSource ────────────────────────────────────────────────────

        [Test]
        public void RegisterAttentionSource_FiresChangedEvent()
        {
            GameObject root = new("SrcReg_AttentionRegister");
            try
            {
                EmbodimentContext ctx = root.AddComponent<EmbodimentContext>();
                IAttentionSource received = null;
                ctx.AttentionSourceChanged += s => received = s;

                StubAttentionSource source = new();
                ctx.RegisterAttentionSource(source);

                Assert.AreSame(source, received);
                Assert.AreSame(source, ctx.AttentionSource);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void UnregisterAttentionSource_NonMatchingSource_DoesNotClear()
        {
            GameObject root = new("SrcReg_AttentionUnregisterWrong");
            try
            {
                EmbodimentContext ctx = root.AddComponent<EmbodimentContext>();
                StubAttentionSource registered = new();
                StubAttentionSource unrelated = new();
                ctx.RegisterAttentionSource(registered);

                ctx.UnregisterAttentionSource(unrelated);

                Assert.AreSame(registered, ctx.AttentionSource);
            }
            finally { Object.DestroyImmediate(root); }
        }

        // ── EmotionStateSource ─────────────────────────────────────────────────

        [Test]
        public void RegisterEmotionStateSource_FiresChangedEvent()
        {
            GameObject root = new("SrcReg_EmotionRegister");
            try
            {
                EmbodimentContext ctx = root.AddComponent<EmbodimentContext>();
                IEmotionStateSource received = null;
                ctx.EmotionStateSourceChanged += s => received = s;

                StubEmotionSource source = new();
                ctx.RegisterEmotionStateSource(source);

                Assert.AreSame(source, received);
                Assert.AreSame(source, ctx.EmotionStateSource);
            }
            finally { Object.DestroyImmediate(root); }
        }

        // ── GazeIntentProvider ─────────────────────────────────────────────────

        [Test]
        public void RegisterGazeIntentProvider_FiresChangedEvent()
        {
            GameObject root = new("SrcReg_GazeRegister");
            try
            {
                EmbodimentContext ctx = root.AddComponent<EmbodimentContext>();
                IGazeIntentProvider received = null;
                ctx.GazeIntentProviderChanged += s => received = s;

                StubGazeProvider source = new();
                ctx.RegisterGazeIntentProvider(source);

                Assert.AreSame(source, received);
                Assert.AreSame(source, ctx.GazeIntentProvider);
            }
            finally { Object.DestroyImmediate(root); }
        }

        // ── ProfileReceiver ────────────────────────────────────────────────────

        [Test]
        public void RegisterProfileReceiver_FiresRegisteredEvent()
        {
            GameObject root = new("SrcReg_ReceiverRegister");
            try
            {
                EmbodimentContext ctx = root.AddComponent<EmbodimentContext>();
                EmbodimentProfileReceiverRegistration? received = null;
                ctx.ProfileReceiverRegistered += r => received = r;

                StubReceiver receiver = root.AddComponent<StubReceiver>();
                ctx.RegisterProfileReceiver(receiver, receiver);

                Assert.IsTrue(received.HasValue);
                Assert.AreSame(receiver, received.Value.Receiver);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void RegisterProfileReceiver_SameReceiverTwice_IsIdempotent()
        {
            GameObject root = new("SrcReg_ReceiverDuplicate");
            try
            {
                EmbodimentContext ctx = root.AddComponent<EmbodimentContext>();
                int callCount = 0;
                ctx.ProfileReceiverRegistered += _ => callCount++;

                StubReceiver receiver = root.AddComponent<StubReceiver>();
                ctx.RegisterProfileReceiver(receiver, receiver);
                ctx.RegisterProfileReceiver(receiver, receiver);

                Assert.AreEqual(1, callCount, "Second registration of same receiver must be ignored");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void UnregisterProfileReceiver_RemovesFromGetProfileReceivers()
        {
            GameObject root = new("SrcReg_ReceiverUnregister");
            try
            {
                EmbodimentContext ctx = root.AddComponent<EmbodimentContext>();
                StubReceiver receiver = root.AddComponent<StubReceiver>();
                ctx.RegisterProfileReceiver(receiver, receiver);

                ctx.UnregisterProfileReceiver(receiver);

                var results = new List<EmbodimentProfileReceiverRegistration>();
                ctx.GetProfileReceivers(results);
                Assert.AreEqual(0, results.Count);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void GetProfileReceivers_AfterRegister_ContainsReceiver()
        {
            GameObject root = new("SrcReg_GetReceivers");
            try
            {
                EmbodimentContext ctx = root.AddComponent<EmbodimentContext>();
                StubReceiver receiver = root.AddComponent<StubReceiver>();
                ctx.RegisterProfileReceiver(receiver, receiver);

                var results = new List<EmbodimentProfileReceiverRegistration>();
                ctx.GetProfileReceivers(results);

                Assert.AreEqual(1, results.Count);
                Assert.AreSame(receiver, results[0].Receiver);
            }
            finally { Object.DestroyImmediate(root); }
        }

        // ── Stubs ──────────────────────────────────────────────────────────────

        private sealed class StubFlowSource : IConversationFlowSource
        {
            public DialogueStateReading Current => DialogueStateReading.Idle;
            public event Action<DialogueStateReading> Changed
            {
                add { }
                remove { }
            }
        }

        private sealed class StubAttentionSource : IAttentionSource
        {
            public AttentionReading Current => AttentionReading.Empty;
        }

        private sealed class StubEmotionSource : IEmotionStateSource
        {
            public EmotionReading Current => EmotionReading.Neutral;
        }

        private sealed class StubGazeProvider : IGazeIntentProvider
        {
            public GazeIntent Current => GazeIntent.Relaxed;
        }

        private sealed class StubReceiver : MonoBehaviour, IEmbodimentProfileReceiver
        {
            public string ModuleId => "test.src-reg-stub";
            public ScriptableObject Profile => null;
            public bool CanApplyProfile(ScriptableObject candidate) => true;
            public bool ApplyProfile(ScriptableObject candidate) => true;
        }
    }
}
