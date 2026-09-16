using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using Convai.Domain.Logging;
using Convai.Domain.Models.LipSync;
using Convai.Runtime.Logging;
using UnityEngine;

namespace Convai.Modules.LipSync.Profiles
{
    public static class LipSyncProfileCatalog
    {
        internal const string LegacyMetaHumanProfileId = "mha";
        private const string BuiltInRegistryResourcePath = "LipSync/ProfileRegistries/LipSyncBuiltInProfileRegistry";
        private const string RegistryResourcePath = "LipSync/ProfileRegistries";

        private static readonly Dictionary<string, ConvaiLipSyncProfile> ProfilesById =
            new(StringComparer.Ordinal);

        private static readonly List<ConvaiLipSyncProfile> OrderedProfiles = new();
        private static readonly List<string> ValidationIssues = new();
        private static readonly ReadOnlyCollection<ConvaiLipSyncProfile> ReadOnlyProfiles =
            OrderedProfiles.AsReadOnly();
        private static readonly ReadOnlyCollection<string> ReadOnlyValidationIssues = ValidationIssues.AsReadOnly();

        private static bool _initialized;
        private static readonly object InitLock = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void DomainReload()
        {
            lock (InitLock)
            {
                ClearCaches();
                Volatile.Write(ref _initialized, false);
            }
        }

        public static IReadOnlyList<ConvaiLipSyncProfile> GetProfiles()
        {
            EnsureInitialized();
            return ReadOnlyProfiles;
        }

        public static bool TryGetProfile(LipSyncProfileId profileId, out ConvaiLipSyncProfile profile)
        {
            EnsureInitialized();
            if (ProfilesById.TryGetValue(profileId.Value, out profile)) return true;

            return string.Equals(profileId.Value, LegacyMetaHumanProfileId, StringComparison.Ordinal) &&
                   ProfilesById.TryGetValue(LipSyncProfileId.MetaHumanValue, out profile);
        }

        public static bool TryGetProfile(string rawProfileId, out ConvaiLipSyncProfile profile) =>
            TryGetProfile(new LipSyncProfileId(rawProfileId), out profile);

        public static IReadOnlyList<string> GetSourceBlendshapeNamesOrEmpty(LipSyncProfileId profileId) =>
            LipSyncBuiltInProfileLibrary.GetSourceBlendshapeNamesOrEmpty(CanonicalizeProfileId(profileId));

        internal static LipSyncProfileId CanonicalizeProfileId(LipSyncProfileId profileId)
        {
            EnsureInitialized();
            if (ProfilesById.ContainsKey(profileId.Value)) return profileId;

            return string.Equals(profileId.Value, LegacyMetaHumanProfileId, StringComparison.Ordinal) &&
                   ProfilesById.ContainsKey(LipSyncProfileId.MetaHumanValue)
                ? LipSyncProfileId.MetaHuman
                : profileId;
        }

        public static IReadOnlyList<string> GetValidationIssues()
        {
            EnsureInitialized();
            return ReadOnlyValidationIssues;
        }

        private static void EnsureInitialized()
        {
            if (Volatile.Read(ref _initialized)) return;

            lock (InitLock)
            {
                if (Volatile.Read(ref _initialized)) return;

                ClearCaches();

                ConvaiLipSyncProfileRegistry builtInRegistry = ResolveBuiltInRegistry();
                if (builtInRegistry == null)
                {
                    AddValidationIssue(
                        "Built-in profile registry is missing at Resources path LipSync/ProfileRegistries/LipSyncBuiltInProfileRegistry " +
                        "(ship default under SamplesShared/Resources/LipSync in this package).");
                }
                else
                    RegisterRegistryProfiles(builtInRegistry);

                List<ConvaiLipSyncProfileRegistry> extensionRegistries =
                    ResolveExtensionRegistries(builtInRegistry);
                foreach (ConvaiLipSyncProfileRegistry registry in extensionRegistries)
                    RegisterRegistryProfiles(registry);

                RebuildFormatMapAndOrderedList();
                OrderedProfiles.Sort((a, b) => string.CompareOrdinal(a.ProfileId.Value, b.ProfileId.Value));
                Volatile.Write(ref _initialized, true);
            }
        }

        private static void RegisterRegistryProfiles(ConvaiLipSyncProfileRegistry registry)
        {
            IReadOnlyList<ConvaiLipSyncProfile> profiles = registry.Profiles;
            if (profiles == null || profiles.Count == 0) return;

            for (int i = 0; i < profiles.Count; i++)
            {
                ConvaiLipSyncProfile profile = profiles[i];
                if (profile == null) continue;

                LipSyncProfileId profileId = profile.ProfileId;
                if (!profileId.IsValid)
                {
                    AddValidationIssue(
                        $"Skipping profile in '{GetRegistryDisplayName(registry)}' because profile id is empty.");
                    continue;
                }

                if (!profile.IsValid)
                {
                    string issue = profile.DescribeValidationIssue();
                    AddValidationIssue(
                        $"Skipping invalid profile '{profileId}' in '{GetRegistryDisplayName(registry)}': {issue}");
                    continue;
                }

                if (ProfilesById.TryGetValue(profileId.Value, out ConvaiLipSyncProfile existing))
                {
                    AddValidationIssue(
                        $"Duplicate profile id '{profileId}' found. Overriding '{existing.name}' with '{profile.name}'.");
                }

                ProfilesById[profileId.Value] = profile;
            }
        }

        private static void RebuildFormatMapAndOrderedList()
        {
            OrderedProfiles.Clear();
            var profilesByTransportFormat = new Dictionary<string, ConvaiLipSyncProfile>(StringComparer.Ordinal);

            foreach (KeyValuePair<string, ConvaiLipSyncProfile> pair in ProfilesById)
            {
                ConvaiLipSyncProfile profile = pair.Value;
                OrderedProfiles.Add(profile);

                string transportFormat = profile.TransportFormat;
                if (string.IsNullOrWhiteSpace(transportFormat)) continue;

                if (profilesByTransportFormat.TryGetValue(transportFormat, out ConvaiLipSyncProfile existing))
                {
                    AddValidationIssue(
                        $"Duplicate transport format '{transportFormat}' mapped to both '{existing.name}' and '{profile.name}'. Last one wins.");
                }

                profilesByTransportFormat[transportFormat] = profile;
            }
        }

        private static ConvaiLipSyncProfileRegistry ResolveBuiltInRegistry()
        {
#if UNITY_EDITOR
            if (_builtInRegistryOverrideForTests != null) return _builtInRegistryOverrideForTests;
#endif
            return Resources.Load<ConvaiLipSyncProfileRegistry>(BuiltInRegistryResourcePath);
        }

        private static List<ConvaiLipSyncProfileRegistry> ResolveExtensionRegistries(
            ConvaiLipSyncProfileRegistry builtInRegistry)
        {
            List<ConvaiLipSyncProfileRegistry> result = new();

#if UNITY_EDITOR
            if (_extensionRegistryOverridesForTests != null)
            {
                for (int i = 0; i < _extensionRegistryOverridesForTests.Count; i++)
                {
                    ConvaiLipSyncProfileRegistry registry = _extensionRegistryOverridesForTests[i];
                    if (registry != null) result.Add(registry);
                }

                SortRegistries(result);
                return result;
            }
#endif

            ConvaiLipSyncProfileRegistry[] registries =
                Resources.LoadAll<ConvaiLipSyncProfileRegistry>(RegistryResourcePath);

            for (int i = 0; i < registries.Length; i++)
            {
                ConvaiLipSyncProfileRegistry registry = registries[i];
                if (registry != null && !ReferenceEquals(registry, builtInRegistry) && !IsBuiltInRegistry(registry))
                    result.Add(registry);
            }

            SortRegistries(result);
            return result;
        }

        private static void SortRegistries(List<ConvaiLipSyncProfileRegistry> registries)
        {
            registries.Sort((a, b) =>
            {
                int priorityCompare = a.Priority.CompareTo(b.Priority);
                if (priorityCompare != 0) return priorityCompare;

                return string.CompareOrdinal(GetRegistrySortKey(a), GetRegistrySortKey(b));
            });
        }

        private static bool IsBuiltInRegistry(ConvaiLipSyncProfileRegistry registry)
        {
            if (registry == null) return false;
            return string.Equals(registry.name, "LipSyncBuiltInProfileRegistry", StringComparison.Ordinal);
        }

        private static string GetRegistrySortKey(ConvaiLipSyncProfileRegistry registry) =>
            registry != null ? registry.name ?? string.Empty : string.Empty;

        private static string GetRegistryDisplayName(ConvaiLipSyncProfileRegistry registry) =>
            registry != null ? registry.name : "(null)";

        private static void ClearCaches()
        {
            ProfilesById.Clear();
            OrderedProfiles.Clear();
            ValidationIssues.Clear();
        }

        private static void AddValidationIssue(string message)
        {
            ValidationIssues.Add(message);
            ConvaiLogger.Warning(message, LogCategory.LipSync);
        }

#if UNITY_EDITOR
        private static ConvaiLipSyncProfileRegistry _builtInRegistryOverrideForTests;
        private static IReadOnlyList<ConvaiLipSyncProfileRegistry> _extensionRegistryOverridesForTests;

        internal static void InvalidateCachesForEditor()
        {
            lock (InitLock)
            {
                ClearCaches();
                Volatile.Write(ref _initialized, false);
            }
        }

        public static void SetRegistryOverridesForTests(
            ConvaiLipSyncProfileRegistry builtInRegistry,
            IReadOnlyList<ConvaiLipSyncProfileRegistry> extensionRegistries)
        {
            lock (InitLock)
            {
                _builtInRegistryOverrideForTests = builtInRegistry;
                _extensionRegistryOverridesForTests = extensionRegistries;
                ClearCaches();
                Volatile.Write(ref _initialized, false);
            }
        }

        public static void ClearCachesForTests()
        {
            lock (InitLock)
            {
                _builtInRegistryOverrideForTests = null;
                _extensionRegistryOverridesForTests = null;
                ClearCaches();
                Volatile.Write(ref _initialized, false);
            }
        }
#endif
    }
}
