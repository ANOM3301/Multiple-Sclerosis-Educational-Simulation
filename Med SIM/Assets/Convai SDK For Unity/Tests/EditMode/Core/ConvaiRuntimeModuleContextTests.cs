using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Convai.Domain.DomainEvents.Session;
using Convai.Domain.EventSystem;
using Convai.Domain.Logging;
using Convai.Infrastructure.Networking;
using Convai.Runtime.Behaviors;
using Convai.Runtime.Core;
using Convai.Runtime.Core.Coordinators;
using Convai.Runtime.Core.Modules;
using Convai.Runtime.Core.Registry;
using NUnit.Framework;
using ILogger = Convai.Domain.Logging.ILogger;

namespace Convai.Tests.EditMode.Core
{
    [Category("Phase3")]
    public sealed class ConvaiRuntimeModuleContextTests
    {
        [Test]
        public async Task ModuleContext_ProvideModuleService_MakesServiceAvailableToDependentModules()
        {
            var provider = new ModuleServiceProviderModule();
            var consumer = new ModuleServiceConsumerModule();

            ConvaiRuntime runtime = new ConvaiRuntimeBuilder()
                .UseEventHub(new EventHub(new ImmediateScheduler(), new TestLogger()))
                .UseAgentRegistry(new AgentRegistry())
                .UseRoomRuntime(() => new RoomRuntimeBuilder()
                    .WithConnectionAdapter(new TestConnectionRuntimeAdapter())
                    .WithAudioAdapter(new TestAudioRuntimeAdapter())
                    .WithAgentRegistry(new AgentRegistry())
                    .Build())
                .AddModule(provider)
                .AddModule(consumer)
                .Build();

            await runtime.StartAsync();

            Assert.That(consumer.ObservedService, Is.Not.Null);
            Assert.That(consumer.ObservedService.Value, Is.EqualTo("module-service"));
        }

        [Test]
        public async Task AddAndStartModuleAsync_AfterRuntimeStartup_StartsLateModuleWithContext()
        {
            ConvaiRuntime runtime = CreateRuntime();
            await runtime.StartAsync();
            var module = new LateModule("late-module");

            await runtime.AddAndStartModuleAsync(module);

            Assert.IsTrue(module.WasRegistered);
            Assert.IsTrue(module.WasStarted);
            Assert.AreSame(runtime.Events, module.ObservedEvents);
            Assert.That(runtime.Modules, Does.Contain(module));
        }

        [Test]
        public async Task AddAndStartModuleAsync_AllowsMultipleComponentInstancesWithSameModuleId()
        {
            ConvaiRuntime runtime = CreateRuntime();
            await runtime.StartAsync();
            var first = new LateModule("shared-component-module");
            var second = new LateModule("shared-component-module");

            await runtime.AddAndStartModuleAsync(first);
            await runtime.AddAndStartModuleAsync(second);

            Assert.IsTrue(first.WasStarted);
            Assert.IsTrue(second.WasStarted);
            Assert.That(runtime.Modules, Does.Contain(first));
            Assert.That(runtime.Modules, Does.Contain(second));
        }

        private static ConvaiRuntime CreateRuntime() => new ConvaiRuntimeBuilder()
            .UseEventHub(new EventHub(new ImmediateScheduler(), new TestLogger()))
            .UseAgentRegistry(new AgentRegistry())
            .UseRoomRuntime(() => new RoomRuntimeBuilder()
                .WithConnectionAdapter(new TestConnectionRuntimeAdapter())
                .WithAudioAdapter(new TestAudioRuntimeAdapter())
                .WithAgentRegistry(new AgentRegistry())
                .Build())
            .Build();

        private sealed class ImmediateScheduler : IUnityScheduler
        {
            public void ScheduleOnMainThread(Action action) => action?.Invoke();
            public void ScheduleOnBackground(Action action) => action?.Invoke();
            public bool IsMainThread() => true;
        }

        private sealed class TestLogger : ILogger
        {
            public bool IsEnabled(LogLevel level, LogCategory category) => false;
            public void Log(LogLevel level, string message, LogCategory category = LogCategory.SDK) { }
            public void Log(LogLevel level, string message, IReadOnlyDictionary<string, object> context,
                LogCategory category = LogCategory.SDK)
            { }
            public void Debug(string message, LogCategory category = LogCategory.SDK) { }
            public void Debug(string message, IReadOnlyDictionary<string, object> context,
                LogCategory category = LogCategory.SDK)
            { }
            public void Info(string message, LogCategory category = LogCategory.SDK) { }
            public void Info(string message, IReadOnlyDictionary<string, object> context,
                LogCategory category = LogCategory.SDK)
            { }
            public void Warning(string message, LogCategory category = LogCategory.SDK) { }
            public void Warning(string message, IReadOnlyDictionary<string, object> context,
                LogCategory category = LogCategory.SDK)
            { }
            public void Error(string message, LogCategory category = LogCategory.SDK) { }
            public void Error(string message, IReadOnlyDictionary<string, object> context,
                LogCategory category = LogCategory.SDK)
            { }
            public void Error(Exception exception, string message, LogCategory category = LogCategory.SDK) { }
            public void Error(Exception exception, string message, IReadOnlyDictionary<string, object> context,
                LogCategory category = LogCategory.SDK)
            { }
        }

        private sealed class TestConnectionRuntimeAdapter : IRoomConnectionRuntimeAdapter
        {
            public event Action<SessionStateChanged> StateChanged;

            public SessionState CurrentState => SessionState.Disconnected;
            public bool IsConnected => false;
            public string CurrentRoomName => string.Empty;
            public string CurrentSessionId => string.Empty;
            public bool CanConnect => true;
            public bool HasStarted => true;

            public Task<bool> WaitForStartCompletionAsync(CancellationToken ct) => Task.FromResult(true);
            public bool PrepareConnection() => true;
            public Task<bool> WaitForConnectionResolutionAsync(CancellationToken ct) => Task.FromResult(true);
            public Task<RoomConnectionAttemptResult> ConnectAsync(CancellationToken ct) =>
                Task.FromResult(RoomConnectionAttemptResult.Success());
            public Task DisconnectAsync(CancellationToken ct) => Task.CompletedTask;

            public void NotifyStateChanged(SessionStateChanged stateChanged) => StateChanged?.Invoke(stateChanged);
        }

        private sealed class TestAudioRuntimeAdapter : IRoomAudioRuntimeAdapter
        {
            public bool IsMicMuted => false;
            public void SetMicMuted(bool muted) { }
            public Task StartListeningAsync(int microphoneIndex, CancellationToken ct) => Task.CompletedTask;
            public Task StopListeningAsync(CancellationToken ct) => Task.CompletedTask;
            public bool SetCharacterMuted(string characterId, bool muted) => true;
            public bool IsCharacterMuted(string characterId) => false;
        }

        [Test]
        public void IModuleContext_Does_Not_Expose_Legacy_Generic_Service_Methods()
        {
            string[] methodNames = typeof(IModuleContext)
                .GetMethods()
                .Select(m => m.Name)
                .ToArray();

            Assert.That(methodNames, Does.Contain("TryGetModuleService"));
            Assert.That(methodNames, Does.Contain("ProvideModuleService"));
            Assert.That(methodNames, Does.Not.Contain("GetService"));
            Assert.That(methodNames, Does.Not.Contain("TryGetService"));
            Assert.That(methodNames, Does.Not.Contain("RegisterService"));
        }

        private sealed class ModuleServiceMarker
        {
            public ModuleServiceMarker(string value) => Value = value;

            public string Value { get; }
        }

        private sealed class ModuleServiceProviderModule : IConvaiModule
        {
            public string ModuleId => "provider";
            public string DisplayName => "provider";
            public IReadOnlyList<string> RequiredModules => Array.Empty<string>();
            public IReadOnlyList<Type> RequiredServices => Array.Empty<Type>();
            public IReadOnlyList<Type> ProvidedServices =>
                new[] { typeof(ModuleServiceMarker) };
            public bool IsActive => true;

            public ValueTask RegisterAsync(IModuleContext context, System.Threading.CancellationToken ct = default)
            {
                context.ProvideModuleService(new ModuleServiceMarker("module-service"));
                return default;
            }

            public ValueTask StartAsync(IModuleContext context, System.Threading.CancellationToken ct = default) => default;
            public ValueTask PauseAsync(RuntimePauseReason reason, System.Threading.CancellationToken ct = default) => default;
            public ValueTask ResumeAsync(System.Threading.CancellationToken ct = default) => default;
            public ValueTask StopAsync(System.Threading.CancellationToken ct = default) => default;
        }

        private sealed class ModuleServiceConsumerModule : IConvaiModule
        {
            public ModuleServiceMarker ObservedService { get; private set; }

            public string ModuleId => "consumer";
            public string DisplayName => "consumer";
            public IReadOnlyList<string> RequiredModules => new[] { "provider" };
            public IReadOnlyList<Type> RequiredServices => Array.Empty<Type>();
            public IReadOnlyList<Type> ProvidedServices => Array.Empty<Type>();
            public bool IsActive => true;

            public ValueTask RegisterAsync(IModuleContext context, System.Threading.CancellationToken ct = default) => default;

            public ValueTask StartAsync(IModuleContext context, System.Threading.CancellationToken ct = default)
            {
                context.TryGetModuleService(out ModuleServiceMarker service);
                ObservedService = service;
                return default;
            }

            public ValueTask PauseAsync(RuntimePauseReason reason, System.Threading.CancellationToken ct = default) => default;
            public ValueTask ResumeAsync(System.Threading.CancellationToken ct = default) => default;
            public ValueTask StopAsync(System.Threading.CancellationToken ct = default) => default;
        }

        private sealed class LateModule : IConvaiModule
        {
            public LateModule(string moduleId) => ModuleId = moduleId;

            public bool WasRegistered { get; private set; }
            public bool WasStarted { get; private set; }
            public IEventHub ObservedEvents { get; private set; }
            public string ModuleId { get; }
            public string DisplayName => ModuleId;
            public IReadOnlyList<string> RequiredModules => Array.Empty<string>();
            public IReadOnlyList<Type> RequiredServices => Array.Empty<Type>();
            public IReadOnlyList<Type> ProvidedServices => Array.Empty<Type>();
            public bool IsActive => true;

            public ValueTask RegisterAsync(IModuleContext context, CancellationToken ct = default)
            {
                WasRegistered = true;
                return default;
            }

            public ValueTask StartAsync(IModuleContext context, CancellationToken ct = default)
            {
                WasStarted = true;
                ObservedEvents = context.Events;
                return default;
            }

            public ValueTask PauseAsync(RuntimePauseReason reason, CancellationToken ct = default) => default;
            public ValueTask ResumeAsync(CancellationToken ct = default) => default;
            public ValueTask StopAsync(CancellationToken ct = default) => default;
        }
    }
}
