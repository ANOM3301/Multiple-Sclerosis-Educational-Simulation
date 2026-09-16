using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Convai.Domain.Embodiment.Modules;
using Convai.Runtime.Animation;
using Convai.Runtime.Embodiment;
using NUnit.Framework;
using UnityEngine;

namespace Convai.Tests.EditMode.Architecture
{
    /// <summary>
    ///     Architectural contract tests for the five Convai embodiment module controllers:
    ///     Attention, Gaze, Emotion, DialogueAnimation, ConversationFlow.
    ///     Verifies public-surface hygiene, assembly contracts, and post-rename cleanliness
    ///     via reflection + file scanning. No MonoBehaviour lifecycle is driven here.
    /// </summary>
    [Category("Architecture")]
    public sealed class EmbodimentModuleContractTests
    {
        // ── assembly names ──────────────────────────────────────────────────────────
        private const string AssemblyAttention = "Convai.Modules.Attention";
        private const string AssemblyConversationFlow = "Convai.Modules.ConversationFlow";
        private const string AssemblyDialogueAnimation = "Convai.Modules.DialogueAnimation";
        private const string AssemblyEmotion = "Convai.Modules.Emotion";
        private const string AssemblyGaze = "Convai.Modules.Gaze";
        private const string AssemblyDomainEmbodiment = "Convai.Domain.Embodiment";
        private const string AssemblyRuntime = "Convai.Runtime";

        // ── expected controller + profile pairs ─────────────────────────────────────
        private static readonly (string asm, string controllerName, string profileName, string moduleId)[] ControllerMap =
        {
            (AssemblyAttention,        "Convai.Modules.Attention.Components.ConvaiAttentionController",          "Convai.Modules.Attention.Profiles.ConvaiAttentionProfile",              ModuleIds.Attention),
            (AssemblyGaze,             "Convai.Modules.Gaze.Components.ConvaiGazeCoordinator",                  "Convai.Modules.Gaze.Profiles.ConvaiGazeCoordinationProfile",            ModuleIds.GazeCoordination),
            (AssemblyEmotion,          "Convai.Modules.Emotion.Components.ConvaiEmotionController",             "Convai.Modules.Emotion.Profiles.ConvaiEmotionProfile",                  ModuleIds.Emotion),
            (AssemblyDialogueAnimation,"Convai.Modules.DialogueAnimation.Components.ConvaiDialogueAnimationController", "Convai.Modules.DialogueAnimation.Profiles.ConvaiDialogueAnimationProfile", ModuleIds.DialogueAnimation),
            (AssemblyConversationFlow, "Convai.Modules.ConversationFlow.Components.ConvaiConversationFlowController","Convai.Modules.ConversationFlow.Profiles.ConvaiConversationFlowProfile", ModuleIds.ConversationFlow),
        };

        private static readonly string[] AllFiveModuleAssemblies =
        {
            AssemblyAttention, AssemblyConversationFlow, AssemblyDialogueAnimation,
            AssemblyEmotion, AssemblyGaze
        };

        private static readonly string[] RuntimeSdkAssemblies =
        {
            AssemblyRuntime, AssemblyDomainEmbodiment,
            AssemblyAttention, AssemblyConversationFlow, AssemblyDialogueAnimation,
            AssemblyEmotion, AssemblyGaze, "Convai.Modules.Embodiment"
        };

        // ── path helper ─────────────────────────────────────────────────────────────

        private static string PackageRoot => Path.GetFullPath(Path.Combine(
            UnityEngine.Application.dataPath, "..", "Packages", "com.convai.convai-sdk-for-unity"));

        // ── reflection helpers ───────────────────────────────────────────────────────

        private static Assembly FindOrLoadAssembly(string name)
        {
            Assembly loaded = AppDomain.CurrentDomain
                .GetAssemblies()
                .FirstOrDefault(a => string.Equals(a.GetName().Name, name, StringComparison.Ordinal));
            if (loaded != null) return loaded;
            try { return Assembly.Load(name); }
            catch { return null; }
        }

        private static Type FindType(string fullName, string preferredAssembly)
        {
            Assembly asm = FindOrLoadAssembly(preferredAssembly);
            Type t = asm?.GetType(fullName, false);
            if (t != null) return t;
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType(fullName, false))
                .FirstOrDefault(x => x != null);
        }

        private static bool InheritsEmbodimentProfileReceiverOf(Type controllerType, Type profileType)
        {
            Type open = typeof(EmbodimentProfileReceiver<>);
            Type cursor = controllerType.BaseType;
            while (cursor != null && cursor != typeof(object))
            {
                if (cursor.IsGenericType &&
                    cursor.GetGenericTypeDefinition() == open &&
                    cursor.GetGenericArguments()[0] == profileType)
                    return true;
                cursor = cursor.BaseType;
            }
            return false;
        }

        // ── tests ────────────────────────────────────────────────────────────────────

        [Test]
        public void AllFiveControllers_ExistInExpectedAssemblies()
        {
            var missing = new List<string>();
            foreach ((string asm, string name, _, _) in ControllerMap)
            {
                Type t = FindType(name, asm);
                if (t == null) missing.Add($"{asm}: {name}");
            }
            Assert.IsEmpty(missing, $"Controllers not found:\n{string.Join("\n", missing)}");
        }

        [Test]
        public void AllFiveControllers_InheritEmbodimentProfileReceiverOfTheirProfile()
        {
            var violations = new List<string>();
            foreach ((string asm, string controllerName, string profileName, _) in ControllerMap)
            {
                Type controllerType = FindType(controllerName, asm);
                Type profileType = FindType(profileName, asm);
                if (controllerType == null || profileType == null)
                {
                    violations.Add($"Type not found: {controllerName} or {profileName}");
                    continue;
                }
                if (!InheritsEmbodimentProfileReceiverOf(controllerType, profileType))
                    violations.Add($"{controllerName} does not inherit EmbodimentProfileReceiver<{profileName}>");
            }
            Assert.IsEmpty(violations, string.Join("\n", violations));
        }

        [Test]
        public void AllFiveControllers_ImplementIEmbodimentTickable()
        {
            var violations = new List<string>();
            Type iface = typeof(IEmbodimentTickable);
            foreach ((string asm, string name, _, _) in ControllerMap)
            {
                Type t = FindType(name, asm);
                if (t == null) { violations.Add($"Not found: {name}"); continue; }
                if (!iface.IsAssignableFrom(t))
                    violations.Add($"{name} does not implement IEmbodimentTickable");
            }
            Assert.IsEmpty(violations, string.Join("\n", violations));
        }

        [Test]
        public void AllFiveControllers_HaveDisallowMultipleComponentAttribute()
        {
            var violations = new List<string>();
            foreach ((string asm, string name, _, _) in ControllerMap)
            {
                Type t = FindType(name, asm);
                if (t == null) { violations.Add($"Not found: {name}"); continue; }
                if (t.GetCustomAttribute<DisallowMultipleComponent>() == null)
                    violations.Add($"{name} is missing [DisallowMultipleComponent]");
            }
            Assert.IsEmpty(violations, string.Join("\n", violations));
        }

        [Test]
        public void ModuleIds_ContainsExactlyExpectedConstants()
        {
            HashSet<string> expected = new(StringComparer.Ordinal)
            {
                "convai.attention",
                "convai.baked-facial-clip",
                "convai.conversation-flow",
                "convai.dialogue-animation",
                "convai.emotion",
                "convai.gaze-coordination",
                "convai.gaze-eye",
                "convai.gaze-head",
                "convai.runtime-facial-clip"
            };

            FieldInfo[] fields = typeof(ModuleIds).GetFields(
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

            HashSet<string> actual = fields
                .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                .Select(f => (string)f.GetValue(null))
                .ToHashSet(StringComparer.Ordinal);

            Assert.That(actual, Is.EquivalentTo(expected),
                "ModuleIds constants do not match the expected set.");
        }

        [Test]
        public void EmbodimentExecutionOrders_AllValuesAreUnique()
        {
            FieldInfo[] fields = typeof(EmbodimentExecutionOrders).GetFields(
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

            int[] values = fields
                .Where(f => f.IsLiteral && f.FieldType == typeof(int))
                .Select(f => (int)f.GetValue(null))
                .ToArray();

            HashSet<int> seen = new();
            var duplicates = new List<int>();
            foreach (int v in values)
                if (!seen.Add(v)) duplicates.Add(v);

            Assert.IsEmpty(duplicates,
                $"EmbodimentExecutionOrders has duplicate values: [{string.Join(", ", duplicates)}]");
        }

        [Test]
        public void AllConvaiProfiles_HaveCreateAssetMenuAttribute()
        {
            var violations = new List<string>();
            foreach (string asmName in AllFiveModuleAssemblies)
            {
                Assembly asm = FindOrLoadAssembly(asmName);
                if (asm == null) continue;

                foreach (Type t in asm.GetExportedTypes())
                {
                    if (!t.Name.StartsWith("Convai", StringComparison.Ordinal)) continue;
                    if (!t.Name.EndsWith("Profile", StringComparison.Ordinal)) continue;
                    if (!typeof(ScriptableObject).IsAssignableFrom(t)) continue;
                    if (t.IsAbstract) continue;

                    if (t.GetCustomAttribute<CreateAssetMenuAttribute>() == null)
                        violations.Add($"{asmName}: {t.FullName}");
                }
            }
            Assert.IsEmpty(violations,
                $"Profile types missing [CreateAssetMenu]:\n{string.Join("\n", violations)}");
        }

        [Test]
        public void AllConvaiProfiles_HaveCreateDefaultStaticMethod()
        {
            var violations = new List<string>();
            foreach (string asmName in AllFiveModuleAssemblies)
            {
                Assembly asm = FindOrLoadAssembly(asmName);
                if (asm == null) continue;

                foreach (Type t in asm.GetExportedTypes())
                {
                    if (!t.Name.StartsWith("Convai", StringComparison.Ordinal)) continue;
                    if (!t.Name.EndsWith("Profile", StringComparison.Ordinal)) continue;
                    if (!typeof(ScriptableObject).IsAssignableFrom(t)) continue;
                    if (t.IsAbstract) continue;

                    MethodInfo factory = t.GetMethod("CreateDefault",
                        BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
                    if (factory == null || !t.IsAssignableFrom(factory.ReturnType))
                        violations.Add($"{t.FullName} — CreateDefault() missing or wrong return type");
                }
            }
            Assert.IsEmpty(violations, string.Join("\n", violations));
        }

        [Test]
        public void NoBehaviorTokenInPublicSurface_EmbodimentModuleAssemblies()
        {
            var violations = new List<string>();
            foreach (string asmName in AllFiveModuleAssemblies)
            {
                Assembly asm = FindOrLoadAssembly(asmName);
                if (asm == null) continue;

                foreach (Type t in asm.GetExportedTypes())
                {
                    if (t.Name.Contains("Behavior", StringComparison.Ordinal))
                        violations.Add($"{asmName}: {t.FullName}");
                }
            }
            Assert.IsEmpty(violations,
                $"Post-rename hygiene: 'Behavior' token found in public surface of embodiment module assemblies:\n{string.Join("\n", violations)}");
        }

        [Test]
        public void NoTestUtilitiesInRuntimeAssemblies()
        {
            string[] testPrefixes = { "Mock", "Fake", "Stub", "Recording", "Spy" };
            var violations = new List<string>();
            foreach (string asmName in RuntimeSdkAssemblies)
            {
                Assembly asm = FindOrLoadAssembly(asmName);
                if (asm == null) continue;

                foreach (Type t in asm.GetExportedTypes())
                {
                    string name = t.Name;
                    if (testPrefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal)))
                        violations.Add($"{asmName}: {t.FullName}");
                }
            }
            Assert.IsEmpty(violations,
                $"Test-utility types must not live in runtime assemblies:\n{string.Join("\n", violations)}");
        }

        [Test]
        public void EmbodimentContext_PopulateMethod_AcceptsIEventHubAndILogger()
        {
            MethodInfo populate = typeof(EmbodimentContext).GetMethod(
                "Populate", BindingFlags.Public | BindingFlags.Instance);

            Assert.IsNotNull(populate, "EmbodimentContext.Populate must be public.");

            ParameterInfo[] parameters = populate.GetParameters();
            Assert.That(parameters.Length, Is.EqualTo(2),
                "EmbodimentContext.Populate must take exactly 2 parameters.");
            Assert.That(parameters[0].ParameterType.Name, Is.EqualTo("IEventHub"),
                "First parameter must be IEventHub.");
            Assert.That(parameters[1].ParameterType.Name, Is.EqualTo("ILogger"),
                "Second parameter must be ILogger.");
        }

        [Test]
        public void EmbodimentModuleTestFolders_HaveNoThreadSleepOrTaskDelay()
        {
            // Scoped to embodiment module test folders only; pre-existing async mocks
            // in Core/Infrastructure/Runtime test folders are intentionally excluded.
            string[] moduleFolders =
            {
                Path.Combine(PackageRoot, "Tests", "EditMode", "Attention"),
                Path.Combine(PackageRoot, "Tests", "EditMode", "ConversationFlow"),
                Path.Combine(PackageRoot, "Tests", "EditMode", "DialogueAnimation"),
                Path.Combine(PackageRoot, "Tests", "EditMode", "Emotion"),
                Path.Combine(PackageRoot, "Tests", "EditMode", "Gaze"),
                Path.Combine(PackageRoot, "Tests", "EditMode", "Embodiment"),
                Path.Combine(PackageRoot, "Tests", "PlayMode", "Attention"),
                Path.Combine(PackageRoot, "Tests", "PlayMode", "ConversationFlow"),
                Path.Combine(PackageRoot, "Tests", "PlayMode", "DialogueAnimation"),
                Path.Combine(PackageRoot, "Tests", "PlayMode", "Emotion"),
                Path.Combine(PackageRoot, "Tests", "PlayMode", "Gaze"),
                Path.Combine(PackageRoot, "Tests", "PlayMode", "Integration"),
            };

            // Split tokens so this file doesn't self-flag.
            string[] forbidden =
            {
                "Thread" + ".Sleep",
                "Task" + ".Delay",
                "WaitFor" + "Seconds"
            };
            var violations = new List<string>();

            foreach (string folder in moduleFolders)
            {
                if (!Directory.Exists(folder)) continue;
                foreach (string filePath in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
                {
                    string content = File.ReadAllText(filePath);
                    foreach (string token in forbidden)
                    {
                        if (content.Contains(token, StringComparison.Ordinal))
                        {
                            violations.Add($"{Path.GetRelativePath(PackageRoot, filePath).Replace('\\', '/')}: {token}");
                            break;
                        }
                    }
                }
            }
            Assert.IsEmpty(violations,
                $"Embodiment module test files must not use Thread.Sleep, Task.Delay, or WaitForSeconds:\n{string.Join("\n", violations)}");
        }

        [Test]
        public void TestTree_HasNoTautologicalAssertions()
        {
            string testsRoot = Path.Combine(PackageRoot, "Tests");
            Assert.IsTrue(Directory.Exists(testsRoot), $"Tests root not found: {testsRoot}");

            // Split tokens so this file doesn't self-flag.
            string[] forbidden =
            {
                "Assert" + ".Pass()",
                "Assert" + ".True(true)",
                "Assert" + ".IsTrue(true)"
            };
            var violations = new List<string>();

            foreach (string filePath in Directory.EnumerateFiles(testsRoot, "*.cs", SearchOption.AllDirectories))
            {
                // Skip this file itself to avoid self-flagging.
                if (filePath.EndsWith("EmbodimentModuleContractTests.cs", StringComparison.OrdinalIgnoreCase))
                    continue;

                string content = File.ReadAllText(filePath);
                foreach (string token in forbidden)
                {
                    if (content.Contains(token, StringComparison.Ordinal))
                        violations.Add($"{Path.GetRelativePath(PackageRoot, filePath).Replace('\\', '/')}: `{token}`");
                }
            }
            Assert.IsEmpty(violations,
                $"Tautological assertions must be removed:\n{string.Join("\n", violations)}");
        }
    }
}
