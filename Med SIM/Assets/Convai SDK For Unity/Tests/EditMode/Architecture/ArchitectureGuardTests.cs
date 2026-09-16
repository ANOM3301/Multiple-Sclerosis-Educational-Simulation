using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Convai.Tests.EditMode.Architecture
{
    /// <summary>
    ///     Architectural guardrails for API exposure, namespace conventions, and error-code ownership.
    /// </summary>
    public class ArchitectureGuardTests
    {
        private static readonly Dictionary<string, string> ArchitecturePrefixes = new(StringComparer.Ordinal)
        {
            ["Domain"] = "Convai.Domain",
            ["Application"] = "Convai.Application",
            ["Shared"] = "Convai.Shared",
            ["Infrastructure"] = "Convai.Infrastructure",
            ["Runtime"] = "Convai.Runtime",
            ["Modules"] = "Convai.Modules",
            ["Editor"] = "Convai.Editor"
        };

        private static string PackageRoot => Path.GetFullPath(Path.Combine(
            UnityEngine.Application.dataPath,
            "..",
            "Packages",
            "com.convai.convai-sdk-for-unity"));

        private static string SdkRoot => Path.Combine(PackageRoot, "SDK");

        private static string ToRelativePath(string fullPath) =>
            Path.GetRelativePath(PackageRoot, fullPath).Replace('\\', '/');

        private static readonly Regex MetaGuidPattern = new(
            @"^guid:\s*([0-9a-f]{32})\s*$",
            RegexOptions.Multiline | RegexOptions.CultureInvariant);

        private static readonly Regex ReferenceGuidPattern = new(
            @"guid:\s*([0-9a-f]{32})",
            RegexOptions.Multiline | RegexOptions.CultureInvariant);

        private static readonly string[] SerializedAssetExtensions =
        {
            ".unity", ".prefab", ".asset", ".mat", ".controller", ".anim", ".overrideController"
        };

        private static readonly string[] ApprovedInternalReadmes =
        {
            "SDK/README.md",
            "SDK/Infrastructure/Networking/README.md",
            "SDK/Modules/Vision/README.md",
            "SDK/Modules/LipSync/README.md",
            "SDK/Modules/Narrative/README.md"
        };

        private static readonly string[] ApprovedLipSyncPublicTypes =
        {
            "Convai.Modules.LipSync.BlendshapeMatchMode",
            "Convai.Modules.LipSync.BlendshapeSnapshot",
            "Convai.Modules.LipSync.BlendshapeSnapshot+Enumerator",
            "Convai.Modules.LipSync.ConvaiLipSyncComponent",
            "Convai.Modules.LipSync.ConvaiLipSyncDefaultMapRegistry",
            "Convai.Modules.LipSync.ConvaiLipSyncDefaultMapRegistry+ProfileDefaultMapEntry",
            "Convai.Modules.LipSync.ConvaiLipSyncMapAsset",
            "Convai.Modules.LipSync.ConvaiLipSyncMapAsset+BlendshapeMappingEntry",
            "Convai.Modules.LipSync.ConvaiLipSyncMapAsset+BlendshapeMappingSnapshot",
            "Convai.Modules.LipSync.ConvaiLipSyncSpeechEnergyAdapter",
            "Convai.Modules.LipSync.IPlaybackClock",
            "Convai.Modules.LipSync.LipSyncEngineConfig",
            "Convai.Modules.LipSync.LipSyncLatencyMode",
            "Convai.Modules.LipSync.LipSyncPlaybackEngine",
            "Convai.Modules.LipSync.PlaybackState",
            "Convai.Modules.LipSync.Profiles.ConvaiLipSyncProfile",
            "Convai.Modules.LipSync.Profiles.ConvaiLipSyncProfileRegistry",
            "Convai.Modules.LipSync.Profiles.LipSyncBuiltInProfileLibrary",
            "Convai.Modules.LipSync.Profiles.LipSyncProfileCatalog",
            "Convai.Modules.LipSync.RealtimePlaybackClock"
        };

        private static readonly Dictionary<string, string[]> LipSyncSerializedFields =
            new(StringComparer.Ordinal)
            {
                ["Convai.Modules.LipSync.ConvaiLipSyncComponent"] = new[]
                {
                    "_deliverChunksAhead", "_fadeInDuration", "_fadeOutDuration", "_latencyMode",
                    "_lockedProfileId", "_mapping", "_maxBufferedSeconds", "_minResumeHeadroomSeconds",
                    "_smoothingFactor", "_targetMeshes", "_timeOffset"
                },
                ["Convai.Modules.LipSync.ConvaiLipSyncDefaultMapRegistry"] = new[] { "_entries" },
                ["Convai.Modules.LipSync.ConvaiLipSyncDefaultMapRegistry+ProfileDefaultMapEntry"] =
                    new[] { "_defaultMap", "_profileId" },
                ["Convai.Modules.LipSync.ConvaiLipSyncMapAsset"] = new[]
                {
                    "_allowUnmappedPassthrough", "_description", "_globalMultiplier", "_globalOffset", "_mappings",
                    "_targetProfileId"
                },
                ["Convai.Modules.LipSync.ConvaiLipSyncMapAsset+BlendshapeMappingEntry"] = new[]
                {
                    "clampMaxValue", "clampMinValue", "curveExponent", "enabled", "ignoreGlobalModifiers",
                    "multiplier", "offset", "overrideValue", "sourceBlendshape", "targetNames", "useOverrideValue"
                },
                ["Convai.Modules.LipSync.Profiles.ConvaiLipSyncProfile"] =
                    new[] { "_displayName", "_profileId", "_transportFormat" },
                ["Convai.Modules.LipSync.Profiles.ConvaiLipSyncProfileRegistry"] =
                    new[] { "_priority", "_profiles" }
            };

        private static readonly Dictionary<string, string> ShippedLipSyncAssetGuids =
            new(StringComparer.Ordinal)
            {
                ["DefaultMaps/ConvaiLipSyncDefaultMap_ARKit.asset"] = "72ac40a64996b054da875b76f1bec0e2",
                ["DefaultMaps/ConvaiLipSyncDefaultMap_ARKitToCC4Extended.asset"] = "723dcc0a54572524cb370f44facc55e1",
                ["DefaultMaps/ConvaiLipSyncDefaultMap_CC4Extended.asset"] = "4a6a64db478f45bb876c79820fa58b8f",
                ["DefaultMaps/ConvaiLipSyncDefaultMap_MetaHuman.asset"] = "bfbaf969f6bb7c54b811e1c100a85007",
                ["DefaultMaps/LipSyncDefaultMapRegistry.asset"] = "9f5ed2a878fd4a0b95e19d75a1aeed91",
                ["ProfileRegistries/LipSyncBuiltInProfileRegistry.asset"] = "6e9f26db2a5b4a86af179fb5fdbb9d40",
                ["Profiles/ConvaiLipSyncProfile_ARKit.asset"] = "7f70ff5d65b14d79b1237e2f14f74798",
                ["Profiles/ConvaiLipSyncProfile_CC4Extended.asset"] = "f98f47e1fcf04957afed54c47dbf96b0",
                ["Profiles/ConvaiLipSyncProfile_MetaHuman.asset"] = "9f3b414f6d3646d9ab4069f5972711a2"
            };

        private static readonly Dictionary<string, string> SerializedLipSyncScriptGuids =
            new(StringComparer.Ordinal)
            {
                ["SDK/Modules/LipSync/Components/ConvaiLipSyncComponent.cs"] =
                    "6c88cf6223c045f9b3c64df967531a5b",
                ["SDK/Modules/LipSync/Mapping/ConvaiLipSyncDefaultMapRegistry.cs"] =
                    "cae9f96d041a48f181496ec11b15d09d",
                ["SDK/Modules/LipSync/Mapping/ConvaiLipSyncMapAsset.cs"] =
                    "3e269b39c8e9499c8b7febc3548e30fa",
                ["SDK/Modules/LipSync/Profiles/ConvaiLipSyncProfile.cs"] =
                    "22f3e71500c0e4d43b838a925e816010",
                ["SDK/Modules/LipSync/Profiles/ConvaiLipSyncProfileRegistry.cs"] =
                    "21ea64cc32ca1f14eae1be7d5fe0e993"
            };

        private static Dictionary<string, string> BuildGuidPathMap()
        {
            return Directory
                .EnumerateFiles(PackageRoot, "*.meta", SearchOption.AllDirectories)
                .Select(path => new { Path = path, Match = MetaGuidPattern.Match(File.ReadAllText(path)) })
                .Where(entry => entry.Match.Success)
                .ToDictionary(
                    entry => entry.Match.Groups[1].Value,
                    entry => Path.ChangeExtension(entry.Path, null),
                    StringComparer.Ordinal);
        }

        private static IEnumerable<string> EnumerateSerializedAssetFiles(string rootPath)
        {
            return Directory
                .EnumerateFiles(rootPath, "*", SearchOption.AllDirectories)
                .Where(path => SerializedAssetExtensions.Contains(Path.GetExtension(path),
                    StringComparer.OrdinalIgnoreCase));
        }

        private static IEnumerable<string> GetResolvedAssetReferences(string assetPath,
            IReadOnlyDictionary<string, string> guidPathMap)
        {
            return ReferenceGuidPattern
                .Matches(File.ReadAllText(assetPath))
                .Select(match => match.Groups[1].Value)
                .Distinct(StringComparer.Ordinal)
                .Select(guid => guidPathMap.TryGetValue(guid, out string resolvedPath) ? ToRelativePath(resolvedPath) : null)
                .Where(path => !string.IsNullOrEmpty(path));
        }

        private static Assembly FindOrLoadAssembly(string name)
        {
            Assembly loaded = AppDomain.CurrentDomain
                .GetAssemblies()
                .FirstOrDefault(a => string.Equals(a.GetName().Name, name, StringComparison.Ordinal));

            if (loaded != null) return loaded;

            try
            {
                return Assembly.Load(name);
            }
            catch
            {
                return null;
            }
        }

        private static Type FindType(string fullName, params string[] preferredAssemblies)
        {
            if (preferredAssemblies != null)
            {
                foreach (string assemblyName in preferredAssemblies)
                {
                    Assembly assembly = FindOrLoadAssembly(assemblyName);
                    Type preferredType = assembly?.GetType(fullName, false);
                    if (preferredType != null) return preferredType;
                }
            }

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }

            return null;
        }

        private static string FormatViolations(string header, IReadOnlyList<string> violations)
        {
            const int maxToPrint = 20;
            var sb = new StringBuilder();
            sb.AppendLine(header);

            for (int i = 0; i < violations.Count && i < maxToPrint; i++) sb.AppendLine($"- {violations[i]}");

            if (violations.Count > maxToPrint) sb.AppendLine($"... and {violations.Count - maxToPrint} more");

            return sb.ToString();
        }

        [Test]
        [Category("Architecture")]
        public void Namespaces_Use_ArchitectureLayer_Prefixes()
        {
            Assert.IsTrue(Directory.Exists(SdkRoot), $"SDK root not found: {SdkRoot}");

            var violations = new List<string>();
            var namespacePattern = new Regex(@"^\s*namespace\s+([A-Za-z_][A-Za-z0-9_.]*)", RegexOptions.Multiline);

            foreach (string filePath in Directory.EnumerateFiles(SdkRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (string.Equals(Path.GetFileName(filePath), "AssemblyInfo.cs", StringComparison.Ordinal)) continue;

                string relativeToSdk = Path.GetRelativePath(SdkRoot, filePath).Replace('\\', '/');
                string[] pathSegments = relativeToSdk.Split('/');
                string rootFolder = pathSegments[0];
                string[] expectedPrefixes = pathSegments
                    .Take(pathSegments.Length - 1)
                    .Where(segment => ArchitecturePrefixes.ContainsKey(segment))
                    .Select(segment => ArchitecturePrefixes[segment])
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

                if (expectedPrefixes.Length == 0) continue;

                string source = File.ReadAllText(filePath);
                Match nsMatch = namespacePattern.Match(source);
                if (!nsMatch.Success) continue;

                string declaredNamespace = nsMatch.Groups[1].Value;
                bool valid = expectedPrefixes.Any(expectedPrefix =>
                                 declaredNamespace.Equals(expectedPrefix, StringComparison.Ordinal) ||
                                 declaredNamespace.StartsWith(expectedPrefix + ".", StringComparison.Ordinal));

                if (!valid &&
                    string.Equals(rootFolder, "Editor", StringComparison.Ordinal) &&
                    (declaredNamespace.Contains(".Editor.", StringComparison.Ordinal) ||
                     declaredNamespace.EndsWith(".Editor", StringComparison.Ordinal) ||
                     declaredNamespace.Equals("Convai.Editor", StringComparison.Ordinal)))
                    valid = true;

                if (!valid)
                    violations.Add(
                        $"{ToRelativePath(filePath)} declares '{declaredNamespace}' (expected one of: {string.Join(", ", expectedPrefixes)})");
            }

            Assert.IsEmpty(violations, FormatViolations("Architecture namespace prefix violations:", violations));
        }

        [Test]
        [Category("Architecture")]
        public void Domain_Layer_Must_Not_Reference_Outer_Layers()
        {
            Assembly domainAssembly = FindOrLoadAssembly("Convai.Domain");
            if (domainAssembly == null)
            {
                Assert.Inconclusive("Convai.Domain assembly is not loaded.");
                return;
            }

            string[] forbidden =
            {
                "Convai.Application", "Convai.Infrastructure", "Convai.Infrastructure.Networking",
                "Convai.Infrastructure.Protocol", "Convai.Runtime", "Convai.Runtime.Behaviors",
                "Convai.Modules.Vision", "Convai.Modules.Narrative", "Convai.Editor"
            };

            HashSet<string> referencedNames = domainAssembly
                .GetReferencedAssemblies()
                .Select(a => a.Name)
                .ToHashSet(StringComparer.Ordinal);

            List<string> violations = forbidden
                .Where(referencedNames.Contains)
                .ToList();

            Assert.IsEmpty(violations,
                $"Convai.Domain has forbidden outer-layer dependencies: [{string.Join(", ", violations)}]");
        }

        [Test]
        [Category("Architecture")]
        public void Canonical_Errors_Are_Single_Source_Of_Truth()
        {
            Type canonicalErrorCodesType = FindType(
                "Convai.Domain.Errors.SessionErrorCodes",
                "Convai.Domain");

            Assert.NotNull(canonicalErrorCodesType, "Could not locate SessionErrorCodes.");

            FieldInfo[] canonicalFields = canonicalErrorCodesType.GetFields(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance |
                BindingFlags.DeclaredOnly);

            List<string> localConstStringFields = canonicalFields
                .Where(f => f.FieldType == typeof(string) && f.IsLiteral && !f.IsInitOnly)
                .Select(f => f.Name)
                .ToList();

            Assert.IsNotEmpty(localConstStringFields,
                "SessionErrorCodes should expose canonical string constants.");

            string infraNetworkingRoot = Path.Combine(SdkRoot, "Infrastructure", "Networking");
            Assert.IsTrue(Directory.Exists(infraNetworkingRoot),
                $"Infrastructure networking path not found: {infraNetworkingRoot}");

            var canonicalCodeConstPattern = new Regex(
                @"const\s+string\s+\w+\s*=\s*""([a-z][a-z0-9_]*\.[a-z0-9_.]+)""",
                RegexOptions.Multiline);

            var infraViolations = new List<string>();

            foreach (string filePath in Directory.EnumerateFiles(infraNetworkingRoot, "*.cs",
                         SearchOption.AllDirectories))
            {
                string source = File.ReadAllText(filePath);
                MatchCollection matches = canonicalCodeConstPattern.Matches(source);

                foreach (Match match in matches)
                    if (match.Success)
                        infraViolations.Add($"{ToRelativePath(filePath)} => \"{match.Groups[1].Value}\"");
            }

            Assert.IsEmpty(infraViolations,
                FormatViolations("Infrastructure networking declares canonical-style const error codes:",
                    infraViolations));
        }

        [Test]
        [Category("Architecture")]
        public void LegacyCompatibilityAndLocatorTypes_Are_Removed()
        {
            Type serviceLocator = FindType("Convai.Shared.DependencyInjection.ConvaiServiceLocator");
            Type roomSession = FindType("Convai.Application.ConvaiRoomSession");
            Type transcriptUIServicesInterface = FindType("Convai.Runtime.Presentation.Services.ITranscriptUIServices");
            Type transcriptBroadcaster = FindType("Convai.Infrastructure.Networking.ITranscriptBroadcaster");
            Type compatibilityProvider = FindType("Convai.Runtime.Adapters.Networking.ConvaiRoomManager+CompatibilitySceneOwnershipProvider");

            Assert.IsNull(serviceLocator, "ConvaiServiceLocator should not exist. Use typed dependencies and manager-owned runtime composition.");
            Assert.IsNull(roomSession, "ConvaiRoomSession should not exist. Room connection logic lives in ConvaiRoomManager.");
            Assert.IsNull(transcriptUIServicesInterface, "ITranscriptUIServices should not exist.");
            Assert.IsNull(transcriptBroadcaster, "ITranscriptBroadcaster should not exist.");
            Assert.IsNull(compatibilityProvider, "CompatibilitySceneOwnershipProvider should not exist.");

            Type managerType = FindType("Convai.Runtime.Components.ConvaiManager");
            if (managerType != null)
            {
                PropertyInfo instanceProp = managerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                Assert.IsNull(instanceProp, "ConvaiManager.Instance singleton should not exist.");
            }

            Type roomManagerType = FindType("Convai.Runtime.Adapters.Networking.ConvaiRoomManager");
            if (roomManagerType != null)
            {
                MethodInfo injectMethod = roomManagerType.GetMethod("Inject", BindingFlags.Public | BindingFlags.Instance);
                Assert.IsNull(injectMethod, "ConvaiRoomManager.Inject should not exist.");
            }

            Type bootstrapType = FindType("Convai.Runtime.ConvaiServiceBootstrap");
            if (bootstrapType != null)
            {
                MethodInfo manualBootstrap = bootstrapType.GetMethod("BootstrapManually",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                MethodInfo manualShutdown = bootstrapType.GetMethod("ShutdownManually",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

                Assert.IsNull(manualBootstrap, "ConvaiServiceBootstrap.BootstrapManually should not exist.");
                Assert.IsNull(manualShutdown, "ConvaiServiceBootstrap.ShutdownManually should not exist.");
            }

            Type transcriptControllerType = FindType("Convai.Runtime.Presentation.Services.TranscriptUIController");
            Assert.IsNull(transcriptControllerType,
                "TranscriptUIController should not exist. Use ConvaiManager.Transcripts subscriptions.");
            Assert.IsNull(FindType("Convai.Runtime.Presentation.Services.ITranscriptUI"),
                "ITranscriptUI should not exist. Use ConvaiManager.Transcripts.Subscribe.");
            Assert.IsNull(FindType("Convai.Runtime.Presentation.Services.ITranscriptListener"),
                "ITranscriptListener should not exist. Use ConvaiManager.Transcripts.Subscribe.");
            Assert.IsNull(FindType("Convai.Application.Services.Transcript.ConversationHistoryService"),
                "ConversationHistoryService should not duplicate the canonical transcript timeline.");
        }

        [Test]
        [Category("Architecture")]
        public void LegacyActionHandlerInterface_Is_Removed()
        {
            Type handlerInterface = FindType("Convai.Runtime.Actions.IConvaiActionHandler", "Convai.Runtime");
            Assert.IsNull(handlerInterface,
                "IConvaiActionHandler should not exist. Use IConvaiActionExecutor with typed ConvaiActionDefinition bindings.");
        }

        [Test]
        [Category("Architecture")]
        public void LegacyConnectionStrategyLayer_Is_Removed()
        {
            Type strategyInterface = FindType(
                "Convai.Runtime.Strategies.IConvaiConnectionStrategy",
                "Convai.Runtime");
            Type strategyImplementation = FindType(
                "Convai.Runtime.Strategies.DefaultConnectionStrategy",
                "Convai.Runtime");

            Assert.IsNull(strategyInterface,
                "IConvaiConnectionStrategy should not exist once connection retry logic lives in the runtime orchestration path.");
            Assert.IsNull(strategyImplementation,
                "DefaultConnectionStrategy should not exist once connection retry logic lives in the runtime orchestration path.");

            Type legacyResumable = FindType(
                "Convai.Runtime.Strategies.IConvaiResumableConnectionStrategy",
                "Convai.Runtime");
            Type legacyReconnectable = FindType(
                "Convai.Runtime.Strategies.IConvaiReconnectableConnectionStrategy",
                "Convai.Runtime");

            Assert.IsNull(legacyResumable, "IConvaiResumableConnectionStrategy should not exist.");
            Assert.IsNull(legacyReconnectable, "IConvaiReconnectableConnectionStrategy should not exist.");
        }

        [Test]
        [Category("Architecture")]
        public void DeadContainerAndAudioArtifacts_Files_Are_Deleted()
        {
            string[] forbiddenFiles =
            {
                Path.Combine(SdkRoot, "Runtime", "Core", "DependencyInjection", "IServiceContainer.cs"),
                Path.Combine(SdkRoot, "Runtime", "Core", "DependencyInjection", "ServiceContainer.cs"),
                Path.Combine(SdkRoot, "Runtime", "Core", "DependencyInjection", "ServiceDescriptor.cs"),
                Path.Combine(SdkRoot, "Runtime", "Core", "DependencyInjection", "ServiceLifetime.cs"),
                Path.Combine(SdkRoot, "Runtime", "Components", "DefaultAudioManager.cs"),
                Path.Combine(SdkRoot, "Runtime", "Components", "IConvaiAudioManager.cs"),
                Path.Combine(SdkRoot, "Runtime", "Services", "ConvaiApplicationServiceRegistrar.cs"),
                Path.Combine(SdkRoot, "Runtime", "Core", "Providers", "ProviderServiceRegistration.cs"),
                Path.Combine(SdkRoot, "Runtime", "Presentation", "Services", "Utilities",
                    "Convai.Runtime.Presentation.Support.asmref")
            };

            List<string> violations = forbiddenFiles.Where(File.Exists).Select(ToRelativePath).ToList();

            Assert.IsEmpty(violations, FormatViolations("Dead artifact files must be deleted:", violations));
        }

        [Test]
        [Category("Architecture")]
        public void Internal_Readme_Surface_Is_Reduced_To_Approved_Files()
        {
            var readmes = Directory
                .EnumerateFiles(SdkRoot, "README.md", SearchOption.AllDirectories)
                .Select(ToRelativePath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();

            CollectionAssert.AreEquivalent(ApprovedInternalReadmes, readmes,
                FormatViolations("Internal SDK readme surface must match the approved set:", readmes));
        }

        [Test]
        [Category("Architecture")]
        public void Kept_Internal_Readmes_Do_Not_Use_Removed_Layer_Or_Bootstrap_Wording()
        {
            string[] forbiddenTokens =
            {
                "manager-driven bootstrap pipeline",
                "runtime bootstrap pipeline",
                "DI container",
                "Application/README.md",
                "Shared/README.md"
            };

            var violations = new List<string>();

            foreach (string relativePath in ApprovedInternalReadmes)
            {
                string fullPath = Path.Combine(PackageRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
                Assert.IsTrue(File.Exists(fullPath), $"Internal README not found: {relativePath}");

                string content = File.ReadAllText(fullPath);
                if (forbiddenTokens.Any(content.Contains))
                    violations.Add(relativePath);
            }

            Assert.IsEmpty(violations,
                FormatViolations("Kept internal readmes still contain removed layer/bootstrap wording:", violations));
        }

        [Test]
        [Category("Architecture")]
        public void Advanced_RuntimeBuilder_Hooks_Are_Documented_If_They_Remain_Public()
        {
            Type builderType = FindType("Convai.Runtime.Core.ConvaiRuntimeBuilder", "Convai.Runtime");
            Assert.NotNull(builderType, "ConvaiRuntimeBuilder should exist.");

            string apiEntrypointsPath = Path.Combine(PackageRoot, "Documentation~", "API-ENTRYPOINTS.md");
            Assert.IsTrue(File.Exists(apiEntrypointsPath), $"API entrypoints doc not found: {ToRelativePath(apiEntrypointsPath)}");
            string apiEntrypoints = File.ReadAllText(apiEntrypointsPath);

            string[] advancedHooks =
            {
                "WithFeatureVariants",
                "UsePersistence",
                "UseTelemetry"
            };

            var undocumented = new List<string>();
            foreach (string hook in advancedHooks)
            {
                MethodInfo method = builderType.GetMethod(hook, BindingFlags.Public | BindingFlags.Instance);
                if (method != null && !apiEntrypoints.Contains(hook, StringComparison.Ordinal))
                    undocumented.Add(hook);
            }

            Assert.IsEmpty(undocumented,
                $"Advanced ConvaiRuntimeBuilder hooks must be documented if they remain public: [{string.Join(", ", undocumented)}]");
        }

        [Test]
        [Category("Architecture")]
        public void Phase3_RoomManager_Uses_Formal_TypedInjection_And_Vision_Is_A_Module()
        {
            Type roomManagerType = FindType("Convai.Runtime.Adapters.Networking.ConvaiRoomManager", "Convai.Runtime");
            Type roomManagerDepsType =
                FindType("Convai.Runtime.Core.DependencyInjection.IConvaiRoomManagerDependencies", "Convai.Runtime");
            Type injectableOpenType = FindType("Convai.Runtime.Core.DependencyInjection.IInjectable`1", "Convai.Runtime");
            Type visionPublisherType = FindType("Convai.Modules.Vision.ConvaiVisionPublisher", "Convai.Modules.Vision");
            Type moduleType = FindType("Convai.Runtime.Core.Modules.IConvaiModule", "Convai.Runtime");

            Assert.IsNotNull(roomManagerType);
            Assert.IsNotNull(roomManagerDepsType);
            Assert.IsNotNull(injectableOpenType);
            Assert.IsNotNull(visionPublisherType);
            Assert.IsNotNull(moduleType);

            Type expectedInjectable = injectableOpenType.MakeGenericType(roomManagerDepsType);
            Assert.IsTrue(expectedInjectable.IsAssignableFrom(roomManagerType),
                "ConvaiRoomManager must formally implement IInjectable<IConvaiRoomManagerDependencies>.");
            Assert.IsTrue(moduleType.IsAssignableFrom(visionPublisherType),
                "ConvaiVisionPublisher must implement IConvaiModule.");
        }

        [Test]
        [Category("Architecture")]
        public void LegacyRoomOrchestrationLayer_Is_Removed()
        {
            Type orchestrationAdapter = FindType(
                "Convai.Runtime.Adapters.Networking.ConnectionOrchestrationAdapter",
                "Convai.Runtime");
            Type reconnectionServiceInterface = FindType(
                "Convai.Infrastructure.Networking.Services.IReconnectionService",
                "Convai.Infrastructure.Networking.Abstractions",
                "Convai.Infrastructure.Networking");
            Type reconnectionService = FindType(
                "Convai.Infrastructure.Networking.Services.ReconnectionService",
                "Convai.Infrastructure.Networking.Abstractions",
                "Convai.Infrastructure.Networking");

            Assert.IsNull(orchestrationAdapter,
                "ConnectionOrchestrationAdapter should not exist once ConvaiRoomManager owns the connection lifecycle.");
            Assert.IsNull(reconnectionServiceInterface,
                "IReconnectionService should not exist once reconnect state is represented by ConnectionContext and ReconnectPolicy.");
            Assert.IsNull(reconnectionService,
                "ReconnectionService should not exist once reconnect state is represented by ConnectionContext and ReconnectPolicy.");

            var legacyFiles = Directory
                .EnumerateFiles(PackageRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path =>
                {
                    string fileName = Path.GetFileName(path);
                    return fileName == "ConnectionOrchestrationAdapter.cs" ||
                           fileName == "IReconnectionService.cs" ||
                           fileName == "ReconnectionService.cs";
                })
                .Select(ToRelativePath)
                .ToList();

            Assert.IsEmpty(legacyFiles,
                FormatViolations("Legacy room orchestration files should be deleted:", legacyFiles));
        }

        [Test]
        [Category("Architecture")]
        public void SessionResume_Is_PerCharacter_Not_Global()
        {
            Type legacyCapability = FindType(
                "Convai.Runtime.Behaviors.ISessionResumable",
                "Convai.Runtime.Behaviors",
                "Convai.Runtime");
            Assert.IsNull(legacyCapability, "ISessionResumable should not exist.");

            Type characterAgentType = FindType(
                "Convai.Runtime.Behaviors.IConvaiCharacterAgent",
                "Convai.Runtime.Behaviors",
                "Convai.Runtime");
            Assert.NotNull(characterAgentType, "Could not locate IConvaiCharacterAgent.");

            PropertyInfo resumeProperty =
                characterAgentType.GetProperty("EnableSessionResume", BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(resumeProperty, "IConvaiCharacterAgent must expose EnableSessionResume.");
            Assert.AreEqual(typeof(bool), resumeProperty.PropertyType, "EnableSessionResume must be a bool.");

            Type dynamicContextType = FindType(
                "Convai.Runtime.DynamicContext.IConvaiDynamicContext",
                "Convai.Runtime.DynamicContext",
                "Convai.Runtime");
            Assert.NotNull(dynamicContextType, "Could not locate IConvaiDynamicContext.");

            PropertyInfo dynamicContextProperty =
                characterAgentType.GetProperty("DynamicContext", BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(dynamicContextProperty, "IConvaiCharacterAgent must expose DynamicContext.");
            Assert.AreEqual(dynamicContextType, dynamicContextProperty.PropertyType,
                "DynamicContext must use IConvaiDynamicContext.");

            Type settingsType = FindType(
                "Convai.Runtime.ConvaiSettings",
                "Convai.Runtime");
            Assert.NotNull(settingsType, "Could not locate ConvaiSettings.");

            PropertyInfo legacyGlobalProperty =
                settingsType.GetProperty("SessionResumeEnabled", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNull(legacyGlobalProperty, "ConvaiSettings.SessionResumeEnabled should not exist.");

            string runtimeRoot = Path.Combine(SdkRoot, "Runtime");
            Assert.IsTrue(Directory.Exists(runtimeRoot), $"Runtime path not found: {runtimeRoot}");

            List<string> legacyReferences = Directory
                .EnumerateFiles(runtimeRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => File.ReadAllText(path).Contains("ISessionResumable", StringComparison.Ordinal))
                .Select(ToRelativePath)
                .ToList();

            Assert.IsEmpty(legacyReferences,
                FormatViolations("Runtime still references ISessionResumable:", legacyReferences));
        }

        [Test]
        [Category("Architecture")]
        public void DynamicContext_Is_CharacterScoped_Not_RoomScoped()
        {
            Type roomServiceType = FindType(
                "Convai.Runtime.Room.IConvaiRoomConnectionService",
                "Convai.Runtime.Room",
                "Convai.Runtime");
            Assert.NotNull(roomServiceType, "Could not locate IConvaiRoomConnectionService.");

            MethodInfo legacyMethod = roomServiceType.GetMethod(
                "UpdateDynamicContext",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNull(legacyMethod, "IConvaiRoomConnectionService should not expose UpdateDynamicContext.");

            MethodInfo legacyDynamicInfoMethod = roomServiceType.GetMethod(
                "SendDynamicInfo",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNull(legacyDynamicInfoMethod, "IConvaiRoomConnectionService should not expose SendDynamicInfo.");

            Type characterAgentType = FindType(
                "Convai.Runtime.Behaviors.IConvaiCharacterAgent",
                "Convai.Runtime.Behaviors",
                "Convai.Runtime");
            Assert.NotNull(characterAgentType, "Could not locate IConvaiCharacterAgent.");

            MethodInfo characterLegacyDynamicInfoMethod = characterAgentType.GetMethod(
                "SendDynamicInfo",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNull(characterLegacyDynamicInfoMethod, "IConvaiCharacterAgent should not expose SendDynamicInfo.");
        }

        [Test]
        [Category("Architecture")]
        public void FutureMultiplayer_Seams_Are_Internal()
        {
            var targets = new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["Convai.Runtime.Networking.Media.IAudioTrackManager"] = new[] { "Convai.Runtime" },
                ["Convai.Infrastructure.Networking.IRemotePlayerRegistry"] =
                    new[] { "Convai.Infrastructure.Networking.Abstractions", "Convai.Infrastructure.Networking" }
            };

            var missing = new List<string>();
            var publicTypes = new List<string>();

            foreach (KeyValuePair<string, string[]> target in targets)
            {
                Type type = FindType(target.Key, target.Value);
                if (type == null)
                {
                    missing.Add(target.Key);
                    continue;
                }

                if (type.IsPublic) publicTypes.Add(target.Key);
            }

            Assert.IsEmpty(missing, $"Could not find expected seam types: [{string.Join(", ", missing)}]");
            Assert.IsEmpty(publicTypes, $"Future seam types must be internal: [{string.Join(", ", publicTypes)}]");
        }

        [Test]
        [Category("Architecture")]
        public void Internal_Implementations_Are_Not_Public()
        {
            var targets = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Convai.Runtime.Networking.Media.AudioTrackManager"] = "Convai.Runtime",
                ["Convai.Runtime.Vision.Transport.VideoTrackManager"] = "Convai.Runtime",
                ["Convai.Runtime.Adapters.Networking.PlayerSessionAdapter"] = "Convai.Runtime",
                ["Convai.Runtime.Adapters.Vision.VideoTrackUnpublisherAdapter"] = "Convai.Runtime",
                ["Convai.Runtime.Adapters.Platform.ConvaiPermissionService"] = "Convai.Runtime"
            };

            var missing = new List<string>();
            var publicTypes = new List<string>();

            foreach (KeyValuePair<string, string> target in targets)
            {
                string fullName = target.Key;
                string assemblyName = target.Value;
                Type type = FindType(fullName, assemblyName);
                if (type == null)
                {
                    missing.Add(fullName);
                    continue;
                }

                if (type.IsPublic) publicTypes.Add(fullName);
            }

            Assert.IsEmpty(missing,
                $"Could not find target internal implementation types: [{string.Join(", ", missing)}]");
            Assert.IsEmpty(publicTypes,
                $"Internal implementation types must not be public: [{string.Join(", ", publicTypes)}]");
        }

        [Test]
        [Category("Architecture")]
        public void LipSyncPublicSurface_MatchesApprovedCustomerApi()
        {
            Assembly assembly = FindOrLoadAssembly("Convai.Modules.LipSync");
            Assert.That(assembly, Is.Not.Null);

            string[] actual = SafeGetTypes(assembly)
                .Where(type => type.IsVisible)
                .Select(type => type.FullName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            string[] expected = ApprovedLipSyncPublicTypes
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.That(actual, Is.EqualTo(expected),
                "LipSync public API changed. Update this allowlist only with approved API review, migration notes, and CHANGELOG.");
        }

        [Test]
        [Category("Architecture")]
        public void LipSyncCleanupSurfaces_AreAbsentOrInternal()
        {
            string[] removedOrInternalized =
            {
                "Convai.Modules.LipSync.IBlendshapeSink",
                "Convai.Modules.LipSync.SkinnedMeshBlendshapeSink",
                "Convai.Modules.LipSync.LipSyncDriftMonitor",
                "Convai.Modules.LipSync.LipSyncDriftSample",
                "Convai.Modules.LipSync.LipSyncDriftEvent"
            };

            string[] visible = removedOrInternalized
                .Select(typeName => FindType(typeName))
                .Where(type => type != null && type.IsVisible)
                .Select(type => type.FullName)
                .ToArray();

            Assert.That(visible, Is.Empty,
                "Approved cleanup surfaces must not remain customer API: " + string.Join(", ", visible));
        }

        [Test]
        [Category("Architecture")]
        public void LipSyncSerializedFields_MatchCompatibilityBaseline()
        {
            foreach (KeyValuePair<string, string[]> entry in LipSyncSerializedFields)
            {
                Type type = FindType(entry.Key);
                Assert.That(type, Is.Not.Null, $"Missing serialized LipSync type: {entry.Key}");

                string[] actual = type.GetFields(
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Where(IsUnitySerializedField)
                    .Select(field => field.Name)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray();
                string[] expected = entry.Value.OrderBy(name => name, StringComparer.Ordinal).ToArray();

                Assert.That(actual, Is.EqualTo(expected),
                    $"Serialized schema changed for {entry.Key}. Preserve field names or add an explicit migration.");
            }
        }

        [Test]
        [Category("Architecture")]
        public void ShippedLipSyncResources_MatchPathAndGuidBaseline()
        {
            string resourceRoot = Path.Combine(PackageRoot, "SamplesShared", "Resources", "LipSync");
            string[] actualPaths = Directory.EnumerateFiles(resourceRoot, "*.asset", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(resourceRoot, path).Replace('\\', '/'))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            string[] expectedPaths = ShippedLipSyncAssetGuids.Keys
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            Assert.That(actualPaths, Is.EqualTo(expectedPaths),
                "Shipped LipSync resource paths changed. Serialized Resources paths are compatibility contracts.");

            foreach (KeyValuePair<string, string> entry in ShippedLipSyncAssetGuids)
            {
                string assetPath = Path.Combine(resourceRoot, entry.Key.Replace('/', Path.DirectorySeparatorChar));
                string metaPath = assetPath + ".meta";
                Assert.That(File.Exists(metaPath), Is.True, $"Missing meta file: {entry.Key}.meta");

                Match match = MetaGuidPattern.Match(File.ReadAllText(metaPath));
                Assert.That(match.Success, Is.True, $"Missing GUID in {entry.Key}.meta");
                Assert.That(match.Groups[1].Value, Is.EqualTo(entry.Value),
                    $"Asset GUID changed for {entry.Key}; existing customer references would break.");
            }

            foreach (KeyValuePair<string, string> entry in SerializedLipSyncScriptGuids)
            {
                string metaPath = Path.Combine(PackageRoot, entry.Key.Replace('/', Path.DirectorySeparatorChar)) +
                                  ".meta";
                Assert.That(File.Exists(metaPath), Is.True, $"Missing serialized type meta: {entry.Key}.meta");

                Match match = MetaGuidPattern.Match(File.ReadAllText(metaPath));
                Assert.That(match.Success, Is.True, $"Missing GUID in {entry.Key}.meta");
                Assert.That(match.Groups[1].Value, Is.EqualTo(entry.Value),
                    $"MonoScript GUID changed for {entry.Key}; serialized components/assets would break.");
            }
        }

        [Test]
        [Category("Architecture")]
        public void AudioTimelineCapability_ExposesTimingOnly()
        {
            Type capability = FindType("Convai.Infrastructure.Networking.IAudioTimelineSnapshotSource");
            Assert.That(capability, Is.Not.Null);
            Assert.That(capability.IsPublic, Is.False);
            Assert.That(capability.GetProperties(), Is.Empty);
            Assert.That(capability.GetEvents(), Is.Empty);

            MethodInfo[] methods = capability.GetMethods();
            Assert.That(methods, Has.Length.EqualTo(1));
            Assert.That(methods[0].Name, Is.EqualTo("TryGetAudioTimelineSnapshot"));
            Assert.That(methods[0].ReturnType, Is.EqualTo(typeof(bool)));
            ParameterInfo[] parameters = methods[0].GetParameters();
            Assert.That(parameters, Has.Length.EqualTo(1));
            Assert.That(parameters[0].IsOut, Is.True);
            Assert.That(parameters[0].ParameterType.Name, Is.EqualTo("AudioTimelineSnapshot&"));
        }

        [Test]
        [Category("Architecture")]
        public void Samples_Do_Not_CrossReference_Each_Other()
        {
            string basicRoot = Path.Combine(PackageRoot, "Samples", "BasicSample");
            string lipSyncRoot = Path.Combine(PackageRoot, "Samples", "LipSyncSample");

            Assert.IsTrue(Directory.Exists(basicRoot), $"BasicSample path not found: {basicRoot}");
            Assert.IsTrue(Directory.Exists(lipSyncRoot), $"LipSyncSample path not found: {lipSyncRoot}");

            Dictionary<string, string> guidPathMap = BuildGuidPathMap();
            var violations = new List<string>();

            static void CollectCrossSampleReferences(string sourceRoot, string forbiddenPrefix,
                Dictionary<string, string> map, List<string> results)
            {
                foreach (string filePath in EnumerateSerializedAssetFiles(sourceRoot))
                foreach (string referencePath in GetResolvedAssetReferences(filePath, map))
                    if (referencePath.StartsWith(forbiddenPrefix, StringComparison.Ordinal))
                        results.Add($"{ToRelativePath(filePath)} -> {referencePath}");
            }

            CollectCrossSampleReferences(basicRoot, "Samples/LipSyncSample/", guidPathMap, violations);
            CollectCrossSampleReferences(lipSyncRoot, "Samples/BasicSample/", guidPathMap, violations);

            Assert.IsEmpty(violations,
                FormatViolations("Sample folders must not directly reference each other:", violations));
        }

        [Test]
        [Category("Architecture")]
        public void Shared_Sample_Scene_References_Live_In_Core_Or_SamplesShared()
        {
            string basicScene = Path.Combine(PackageRoot, "Samples", "BasicSample", "Scenes", "Basic Sample.unity");
            string lipSyncScene =
                Path.Combine(PackageRoot, "Samples", "LipSyncSample", "Scenes", "LipSync Sample.unity");

            Assert.IsTrue(File.Exists(basicScene), $"Basic sample scene not found: {basicScene}");
            Assert.IsTrue(File.Exists(lipSyncScene), $"LipSync sample scene not found: {lipSyncScene}");

            Dictionary<string, string> guidPathMap = BuildGuidPathMap();
            var sharedReferences = GetResolvedAssetReferences(basicScene, guidPathMap)
                .Intersect(GetResolvedAssetReferences(lipSyncScene, guidPathMap), StringComparer.Ordinal)
                .Where(path => path.StartsWith("Samples/", StringComparison.Ordinal) &&
                               !path.StartsWith("SamplesShared/", StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();

            Assert.IsEmpty(sharedReferences,
                FormatViolations("Assets shared by both sample scenes must live in SamplesShared or core package roots:",
                    sharedReferences));
        }

        [Test]
        [Category("Architecture")]
        public void Sample_Content_Must_Not_Reference_Editor_Roots()
        {
            string[] sampleRoots =
            {
                Path.Combine(PackageRoot, "SamplesShared"),
                Path.Combine(PackageRoot, "Samples", "BasicSample"),
                Path.Combine(PackageRoot, "Samples", "LipSyncSample")
            };

            Dictionary<string, string> guidPathMap = BuildGuidPathMap();
            var violations = new List<string>();

            foreach (string sampleRoot in sampleRoots)
            {
                Assert.IsTrue(Directory.Exists(sampleRoot), $"Sample root not found: {sampleRoot}");

                foreach (string filePath in EnumerateSerializedAssetFiles(sampleRoot))
                foreach (string referencePath in GetResolvedAssetReferences(filePath, guidPathMap))
                {
                    if (referencePath.StartsWith("SDK/Editor/", StringComparison.Ordinal))
                    {
                        // Allow samples to use SDK branding graphics
                        if (referencePath.StartsWith("SDK/Editor/Art/UI/Branding", StringComparison.Ordinal))
                            continue;

                        violations.Add($"{ToRelativePath(filePath)} -> {referencePath}");
                    }
                }
            }

            Assert.IsEmpty(violations,
                FormatViolations("Sample-owned content must not reference editor-only package roots:", violations));
        }

        [Test]
        [Category("Architecture")]
        public void NonSample_Package_Content_Must_Not_Own_URP_Dependencies()
        {
            string[] nonSampleRoots =
            {
                Path.Combine(PackageRoot, "SDK"),
                Path.Combine(PackageRoot, "Prefabs"),
                Path.Combine(PackageRoot, "Resources"),
                Path.Combine(PackageRoot, "Tests")
            };

            string[] extensions =
            {
                ".cs", ".asmdef", ".unity", ".prefab", ".asset", ".mat", ".controller"
            };

            string[] forbiddenTokens =
            {
                "Unity.RenderPipelines.",
                "com.unity.render-pipelines."
            };

            var violations = new List<string>();

            foreach (string root in nonSampleRoots)
            {
                Assert.IsTrue(Directory.Exists(root), $"Non-sample root not found: {root}");

                foreach (string filePath in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    if (filePath.EndsWith("ArchitectureGuardTests.cs", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!extensions.Contains(Path.GetExtension(filePath), StringComparer.OrdinalIgnoreCase))
                        continue;

                    string content = File.ReadAllText(filePath);
                    if (forbiddenTokens.Any(content.Contains))
                        violations.Add(ToRelativePath(filePath));
                }
            }

            Assert.IsEmpty(violations,
                FormatViolations("Only sample-owned content should carry URP dependencies or serialized URP references:",
                    violations));
        }

        [Test]
        [Category("Architecture")]
        public void RC8_NoRuntimeReferences_IServiceConsumer()
        {
            string sdkRuntimeRoot = Path.Combine(SdkRoot, "Runtime");
            Assert.IsTrue(Directory.Exists(sdkRuntimeRoot), $"SDK Runtime root not found: {sdkRuntimeRoot}");

            var violations = new List<string>();
            foreach (string filePath in Directory.EnumerateFiles(sdkRuntimeRoot, "*.cs", SearchOption.AllDirectories))
            {
                string content = File.ReadAllText(filePath);
                if (content.Contains("IServiceConsumer", StringComparison.Ordinal) ||
                    content.Contains("ConsumeServices(", StringComparison.Ordinal))
                    violations.Add(ToRelativePath(filePath));
            }

            Assert.IsEmpty(violations,
                FormatViolations("Runtime must not reference IServiceConsumer or ConsumeServices:", violations));
        }

        [Test]
        [Category("Architecture")]
        public void RC8_NoRuntimeReferences_TransportProviderRegistry()
        {
            string packageRoot = Path.Combine(PackageRoot);
            var violations = new List<string>();
            foreach (string filePath in Directory.EnumerateFiles(packageRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (filePath.Contains("ArchitectureGuardTests", StringComparison.Ordinal)) continue;
                string content = File.ReadAllText(filePath);
                if (content.Contains("TransportProviderRegistry", StringComparison.Ordinal))
                    violations.Add(ToRelativePath(filePath));
            }

            Assert.IsEmpty(violations,
                FormatViolations("Package must not reference TransportProviderRegistry:", violations));
        }

        [Test]
        [Category("Architecture")]
        public void RC8_NoRuntimeReferences_ConversationProviderRegistry()
        {
            string packageRoot = Path.Combine(PackageRoot);
            var violations = new List<string>();
            foreach (string filePath in Directory.EnumerateFiles(packageRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (filePath.Contains("ArchitectureGuardTests", StringComparison.Ordinal)) continue;
                string content = File.ReadAllText(filePath);
                if (content.Contains("ConversationProviderRegistry", StringComparison.Ordinal))
                    violations.Add(ToRelativePath(filePath));
            }

            Assert.IsEmpty(violations,
                FormatViolations("Package must not reference ConversationProviderRegistry:", violations));
        }

        [Test]
        [Category("Architecture")]
        public void RC8_NoProductionStartup_FindObjectsByType_MonoBehaviour()
        {
            string managerPath = Path.Combine(SdkRoot, "Runtime", "Components", "ConvaiManager.cs");
            Assert.IsTrue(File.Exists(managerPath), "ConvaiManager.cs not found.");
            string content = File.ReadAllText(managerPath);
            bool hasFindMonoBehaviour = content.Contains("FindObjectsByType<MonoBehaviour>", StringComparison.Ordinal) ||
                                       content.Contains("FindObjectOfType<MonoBehaviour>", StringComparison.Ordinal);
            Assert.IsFalse(hasFindMonoBehaviour,
                "Production startup must not use FindObjectsByType<MonoBehaviour> or FindObjectOfType<MonoBehaviour> in ConvaiManager.");
        }

        [Test]
        [Category("Architecture")]
        public void TestTree_Has_No_Legacy_Remnants_Or_MacOs_Junk()
        {
            string testsRoot = Path.Combine(PackageRoot, "Tests");
            Assert.IsTrue(Directory.Exists(testsRoot), $"Tests root not found: {testsRoot}");

            string legacySdkTestsRoot = Path.Combine(PackageRoot, "SDK", "Tests");
            Assert.IsFalse(Directory.Exists(legacySdkTestsRoot),
                $"Legacy SDK/Tests tree should be deleted: {ToRelativePath(legacySdkTestsRoot)}");

            var dsStoreFiles = Directory
                .EnumerateFiles(testsRoot, ".DS_Store", SearchOption.AllDirectories)
                .Select(ToRelativePath)
                .ToList();

            Assert.IsEmpty(dsStoreFiles,
                FormatViolations("Tests tree must not contain macOS .DS_Store junk files:", dsStoreFiles));
        }

        [Test]
        [Category("Architecture")]
        public void PackageTree_Has_No_MacOs_Junk()
        {
            var dsStoreFiles = Directory
                .EnumerateFiles(PackageRoot, ".DS_Store", SearchOption.AllDirectories)
                .Select(ToRelativePath)
                .ToList();

            Assert.IsEmpty(dsStoreFiles,
                FormatViolations("Package tree must not contain macOS .DS_Store junk files:", dsStoreFiles));
        }

        [Test]
        [Category("Architecture")]
        [Category("Release")]
        public void PackageTree_ShipsLiveKitBootstrapWithoutDownloadedFfiPayloads()
        {
            string liveKitRoot = Path.Combine(
                PackageRoot,
                "Plugins",
                "client-sdk-unity-livekit");
            string nativePluginsRoot = Path.Combine(liveKitRoot, "Runtime", "Plugins");
            string downloaderPath = Path.Combine(liveKitRoot, "Editor", "LiveKitFFIDownloader.cs");
            string liveKitScriptsRoot = Path.Combine(liveKitRoot, "Runtime", "Scripts");
            string protobufPath = Path.Combine(nativePluginsRoot, "Google.Protobuf.dll");

            Assert.IsTrue(File.Exists(downloaderPath), "LiveKit FFI downloader must remain in the released package.");
            Assert.IsTrue(Directory.Exists(liveKitScriptsRoot), "Managed LiveKit runtime source must remain packaged.");
            Assert.IsTrue(
                Directory.EnumerateFiles(liveKitScriptsRoot, "*.cs", SearchOption.AllDirectories).Any(),
                "Managed LiveKit runtime source must not be stripped from the released package.");
            Assert.IsTrue(File.Exists(protobufPath), "Google.Protobuf.dll must remain in the released package.");

            string downloaderSource = File.ReadAllText(downloaderPath);
            StringAssert.Contains(
                "PluginImportSettingsUtil.ApplyNativePluginImportSettings",
                downloaderSource,
                "Downloaded native plugins must retain their generated Unity import configuration.");
            StringAssert.Contains(
                "PluginImportSettingsUtil.ApplyAndroidJarImportSettings",
                downloaderSource,
                "The shared Android WebRTC JAR must retain its Android-only import configuration.");

            List<string> bundledFfiEntries = Directory
                .EnumerateFileSystemEntries(nativePluginsRoot, "ffi-*", SearchOption.TopDirectoryOnly)
                .Select(ToRelativePath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();

            Assert.IsEmpty(
                bundledFfiEntries,
                FormatViolations(
                    "Downloaded LiveKit FFI architecture payloads must not ship in the released package:",
                    bundledFfiEntries));
        }

        [Test]
        [Category("Architecture")]
        public void RC8_LegacyIdentityBridges_Removed()
        {
            Type adapter = FindType("Convai.Runtime.Identity.LegacyEndUserIdentityProviderAdapter", "Convai.Runtime");
            Type bridge = FindType("Convai.Runtime.Identity.LegacyEndUserIdProviderBridge", "Convai.Runtime");
            Assert.IsNull(adapter, "LegacyEndUserIdentityProviderAdapter must be deleted.");
            Assert.IsNull(bridge, "LegacyEndUserIdProviderBridge must be deleted.");
        }

        [Test]
        [Category("Architecture")]
        public void WorkingWithEvents_Doc_Is_Present_And_Linked_From_Primary_Public_Docs()
        {
            string workingWithEventsPath = Path.Combine(PackageRoot, "Documentation~", "WORKING-WITH-EVENTS.md");
            Assert.IsTrue(File.Exists(workingWithEventsPath),
                $"Working-with-events doc not found: {ToRelativePath(workingWithEventsPath)}");

            string[] linkedDocs =
            {
                Path.Combine(PackageRoot, "Documentation~", "README.md"),
                Path.Combine(PackageRoot, "Documentation~", "Convai SDK For Unity.md"),
                Path.Combine(PackageRoot, "Documentation~", "SETUP.md"),
                Path.Combine(PackageRoot, "Documentation~", "API-ENTRYPOINTS.md"),
                Path.Combine(PackageRoot, "Documentation~", "TROUBLESHOOTING.md")
            };

            var missingLinks = new List<string>();
            foreach (string docPath in linkedDocs)
            {
                Assert.IsTrue(File.Exists(docPath), $"Public doc not found: {ToRelativePath(docPath)}");
                string content = File.ReadAllText(docPath);
                if (!content.Contains("WORKING-WITH-EVENTS.md", StringComparison.Ordinal))
                    missingLinks.Add(ToRelativePath(docPath));
            }

            Assert.IsEmpty(missingLinks,
                FormatViolations("Primary public docs must link WORKING-WITH-EVENTS.md:", missingLinks));
        }

        [Test]
        [Category("Architecture")]
        public void Public_Event_Docs_Describe_Approved_Event_Hierarchy()
        {
            string apiEntrypointsPath = Path.Combine(PackageRoot, "Documentation~", "API-ENTRYPOINTS.md");
            string workingWithEventsPath = Path.Combine(PackageRoot, "Documentation~", "WORKING-WITH-EVENTS.md");

            string apiEntrypoints = File.ReadAllText(apiEntrypointsPath);
            string workingWithEvents = File.ReadAllText(workingWithEventsPath);

            StringAssert.Contains("ConvaiManager.Events", apiEntrypoints);
            StringAssert.Contains("ConvaiManager.Transcripts", apiEntrypoints);
            StringAssert.Contains("ConvaiSessionEventRelay", apiEntrypoints);
            StringAssert.Contains("ConvaiTranscriptEventRelay", apiEntrypoints);
            StringAssert.Contains("ConvaiCharacterEventRelay", apiEntrypoints);
            StringAssert.Contains("ConvaiCharacter", apiEntrypoints);
            StringAssert.Contains("character-scoped", apiEntrypoints);

            StringAssert.Contains("canonical typed reactive API", workingWithEvents);
            StringAssert.Contains("canonical transcript timeline and history API", workingWithEvents);
            StringAssert.Contains("no-code UnityEvent relays", workingWithEvents);
            StringAssert.Contains("local character convenience callbacks", workingWithEvents);
        }

        [Test]
        [Category("Architecture")]
        public void Package_Docs_And_Runtime_Code_Do_Not_Reference_Removed_Facade_Subscription_Names()
        {
            string[] roots =
            {
                Path.Combine(PackageRoot, "SDK"),
                Path.Combine(PackageRoot, "Documentation~")
            };

            string[] extensions =
            {
                ".cs", ".md"
            };

            Regex[] forbiddenPatterns =
            {
                new(@"(?<!\w)manager\.Events\.OnCharacterTranscript(?!Received\b)", RegexOptions.CultureInvariant),
                new(@"(?<!\w)manager\.Events\.OnPlayerTranscript(?!Received\b)", RegexOptions.CultureInvariant),
                new(@"(?<!\w)manager\.Events\.OnActionReceived(?!\w)", RegexOptions.CultureInvariant),
                new(@"(?<!\w)manager\.Events\.OnModerationResponse(?!Received\b)", RegexOptions.CultureInvariant),
                new(@"(?<!\w)manager\.Events\.OnLlmNoResponse(?!Received\b)", RegexOptions.CultureInvariant),
                new(@"(?<!\w)manager\.Events\.OnUserIdleWarning(?!Received\b)", RegexOptions.CultureInvariant),
                new(@"(?<!\w)ConvaiEvents\.OnCharacterTranscript(?!Received\b)", RegexOptions.CultureInvariant),
                new(@"(?<!\w)ConvaiEvents\.OnPlayerTranscript(?!Received\b)", RegexOptions.CultureInvariant),
                new(@"(?<!\w)ConvaiEvents\.OnActionReceived(?!\w)", RegexOptions.CultureInvariant),
                new(@"(?<!\w)ConvaiEvents\.OnModerationResponse(?!Received\b)", RegexOptions.CultureInvariant),
                new(@"(?<!\w)ConvaiEvents\.OnLlmNoResponse(?!Received\b)", RegexOptions.CultureInvariant),
                new(@"(?<!\w)ConvaiEvents\.OnUserIdleWarning(?!Received\b)", RegexOptions.CultureInvariant)
            };

            var violations = new List<string>();

            foreach (string root in roots)
            {
                Assert.IsTrue(Directory.Exists(root), $"Documentation/runtime root not found: {root}");

                foreach (string filePath in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    if (!extensions.Contains(Path.GetExtension(filePath), StringComparer.OrdinalIgnoreCase))
                        continue;

                    if (filePath.EndsWith("ArchitectureGuardTests.cs", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string content = File.ReadAllText(filePath);
                    if (forbiddenPatterns.Any(pattern => pattern.IsMatch(content)))
                        violations.Add(ToRelativePath(filePath));
                }
            }

            Assert.IsEmpty(violations,
                FormatViolations("Package docs/runtime code still reference removed ConvaiEvents subscription names:", violations));
        }

        [Test]
        [Category("Architecture")]
        public void Package_Docs_And_Runtime_Code_Do_Not_Reference_Removed_Compatibility_Apis()
        {
            string[] roots =
            {
                Path.Combine(PackageRoot, "SDK"),
                Path.Combine(PackageRoot, "Documentation~")
            };

            string[] extensions =
            {
                ".cs", ".md"
            };

            string[] forbiddenTokens =
            {
                "ConvaiServiceLocator",
                "ConvaiRoomSession",
                "ConvaiManager.Instance",
                "ConvaiCompositionRoot",
                "Phase 09",
                "Phase 10",
                "SDK/Application/README.md",
                "SDK/Shared/README.md",
                "manager-driven bootstrap pipeline",
                "runtime bootstrap pipeline",
                "DI container",
                "IMultiUserTranscriptListener",
                "OnPlayerTranscriptWithSpeaker"
            };

            var violations = new List<string>();

            foreach (string root in roots)
            {
                Assert.IsTrue(Directory.Exists(root), $"Documentation/runtime root not found: {root}");

                foreach (string filePath in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    if (!extensions.Contains(Path.GetExtension(filePath), StringComparer.OrdinalIgnoreCase))
                        continue;

                    if (filePath.EndsWith("ArchitectureGuardTests.cs", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string content = File.ReadAllText(filePath);
                    if (forbiddenTokens.Any(content.Contains))
                        violations.Add(ToRelativePath(filePath));
                }
            }

            Assert.IsEmpty(violations,
                FormatViolations("Package docs/runtime code still reference removed compatibility APIs:", violations));
        }

        [Test]
        [Category("Architecture")]
        public void FacialStack_RemovedLegacyFiles_DoNotExist()
        {
            string[] removedFiles =
            {
                Path.Combine(SdkRoot, "Runtime", "Animation", "AnimatorBlendshapeCaptureSource.cs"),
                Path.Combine(SdkRoot, "Modules", "Animation", "ConvaiExplicitFocusTargetProvider.cs"),
                Path.Combine(PackageRoot, "Samples", "LipSyncSample", "Scripts", "Showcase", "Runtime",
                    "ShowcaseEyeContactController.cs"),
                Path.Combine(PackageRoot, "Samples", "LipSyncSample", "Scripts", "Showcase", "Runtime",
                    "ShowcaseEyeContactConfig.cs"),
                Path.Combine(PackageRoot, "SamplesShared", "Behaviors", "SpeechAnimationBehavior.cs")
            };

            var violations = removedFiles
                .Where(File.Exists)
                .Select(ToRelativePath)
                .ToList();

            Assert.IsEmpty(violations,
                FormatViolations("Removed legacy facial/sample files must not exist:", violations));
        }

        [Test]
        [Category("Architecture")]
        public void FacialStack_DoesNotReference_RemovedLegacyPaths()
        {
            // The legacy `Convai.Modules.Animation` assembly and its
            // `ConvaiAnimationDependencyUtility` have been removed wholesale by the Character
            // Behavior Subsystem redesign; confirm the folder no longer exists.
            string legacyAnimationModule = Path.Combine(SdkRoot, "Modules", "Animation");
            Assert.IsFalse(Directory.Exists(legacyAnimationModule),
                "Legacy Convai.Modules.Animation folder must not exist after the behavior redesign.");

            string legacyAnimationEditor = Path.Combine(SdkRoot, "Editor", "Animation");
            Assert.IsFalse(Directory.Exists(legacyAnimationEditor),
                "Legacy SDK/Editor/Animation folder must not exist after the behavior redesign.");

            string compositorPath = Path.Combine(SdkRoot, "Runtime", "Animation", "FacialBlendshapeCompositorHost.cs");
            string setupPrefabPath = Path.Combine(PackageRoot, "Prefabs", "Setup", "Convai Character.prefab");
            string basicSampleScenePath = Path.Combine(PackageRoot, "Samples", "BasicSample", "Scenes",
                "Basic Sample.unity");
            string lipSyncSampleScenePath = Path.Combine(PackageRoot, "Samples", "LipSyncSample", "Scenes",
                "LipSync Sample.unity");

            string compositor = File.ReadAllText(compositorPath);
            StringAssert.DoesNotContain("ComposeAndWriteLegacy", compositor);
            StringAssert.DoesNotContain("AnimatorCapture", compositor);

            if (File.Exists(setupPrefabPath))
            {
                string setupPrefab = File.ReadAllText(setupPrefabPath);
                StringAssert.DoesNotContain("SpeechAnimationBehavior", setupPrefab);
                StringAssert.DoesNotContain("Convai.Modules.Animation::", setupPrefab);
            }

            if (File.Exists(basicSampleScenePath))
            {
                string basicSampleScene = File.ReadAllText(basicSampleScenePath);
                StringAssert.DoesNotContain("SpeechAnimationBehavior", basicSampleScene);
                StringAssert.DoesNotContain("Convai.Modules.Animation::", basicSampleScene);
            }

            if (File.Exists(lipSyncSampleScenePath))
            {
                string lipSyncSampleScene = File.ReadAllText(lipSyncSampleScenePath);
                StringAssert.DoesNotContain("ShowcaseEyeContactController", lipSyncSampleScene);
                StringAssert.DoesNotContain("Convai.Modules.Animation::", lipSyncSampleScene);
                StringAssert.DoesNotContain("ConvaiEmotionAnimationComponent", lipSyncSampleScene);
            }
        }

        private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.Where(type => type != null);
            }
        }

        private static bool IsUnitySerializedField(FieldInfo field) =>
            !field.IsStatic &&
            !field.IsInitOnly &&
            field.GetCustomAttribute<NonSerializedAttribute>() == null &&
            (field.IsPublic || field.GetCustomAttribute<UnityEngine.SerializeField>() != null);
    }
}
