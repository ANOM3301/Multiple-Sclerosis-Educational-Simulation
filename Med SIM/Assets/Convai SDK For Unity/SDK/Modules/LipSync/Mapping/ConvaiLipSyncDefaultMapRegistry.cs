using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Convai.Domain.Models.LipSync;
using Convai.Modules.LipSync.Profiles;
using UnityEngine;

namespace Convai.Modules.LipSync
{
    [CreateAssetMenu(
        fileName = "LipSyncDefaultMapRegistry",
        menuName = "Convai/Lip Sync/Default Map Registry")]
    public class ConvaiLipSyncDefaultMapRegistry : ScriptableObject
    {
        [SerializeField] private List<ProfileDefaultMapEntry> _entries = new();

        private readonly Dictionary<string, ConvaiLipSyncMapAsset> _cache = new(StringComparer.Ordinal);
        private readonly List<string> _validationIssues = new();
        private int _cacheFingerprint;
        private bool _isCacheValid;
        private ReadOnlyCollection<ProfileDefaultMapEntry> _readOnlyEntries;

        public IReadOnlyList<ProfileDefaultMapEntry> Entries =>
            _readOnlyEntries ??= (_entries ??= new List<ProfileDefaultMapEntry>()).AsReadOnly();

        public IReadOnlyList<string> ValidationIssues
        {
            get
            {
                EnsureCache();
                return _validationIssues;
            }
        }

        private void OnValidate()
        {
            if (_entries != null)
            {
                for (int i = 0; i < _entries.Count; i++)
                {
                    ProfileDefaultMapEntry entry = _entries[i];
                    if (entry == null) continue;

                    entry.NormalizeProfileId();
                }
            }

            _readOnlyEntries = null;
            InvalidateCache();
        }

        public ConvaiLipSyncMapAsset GetForProfile(LipSyncProfileId profileId)
        {
            EnsureCache();
            if (_cache.TryGetValue(profileId.Value, out ConvaiLipSyncMapAsset exact)) return exact;

            LipSyncProfileId canonical = LipSyncProfileCatalog.CanonicalizeProfileId(profileId);
            return canonical != profileId && _cache.TryGetValue(canonical.Value, out ConvaiLipSyncMapAsset aliased)
                ? aliased
                : null;
        }

        private void EnsureCache()
        {
            int nextFingerprint = ComputeCacheFingerprint();
            if (_isCacheValid && nextFingerprint == _cacheFingerprint) return;

            RebuildCache(nextFingerprint);
        }

        private void RebuildCache(int nextFingerprint)
        {
            _cache.Clear();
            _validationIssues.Clear();

            if (_entries == null)
            {
                _cacheFingerprint = nextFingerprint;
                _isCacheValid = true;
                return;
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                ProfileDefaultMapEntry entry = _entries[i];
                if (entry == null)
                {
                    _validationIssues.Add($"Entry #{i + 1} is null and was ignored.");
                    continue;
                }

                ConvaiLipSyncMapAsset map = entry.DefaultMap;
                if (map == null)
                {
                    LipSyncProfileId fallbackProfile = entry.ResolveProfileId();
                    string fallbackName = fallbackProfile.IsValid ? fallbackProfile.Value : "(empty)";
                    _validationIssues.Add($"Entry #{i + 1} has no map assigned (profile: {fallbackName}).");
                    continue;
                }

                LipSyncProfileId resolvedProfileId = entry.ResolveProfileId();
                if (!resolvedProfileId.IsValid)
                {
                    _validationIssues.Add($"Entry #{i + 1} map '{map.name}' has no valid target profile id.");
                    continue;
                }

                if (_cache.TryGetValue(resolvedProfileId.Value, out ConvaiLipSyncMapAsset existing))
                {
                    string existingName = existing != null ? existing.name : "(null)";
                    _validationIssues.Add(
                        $"Duplicate default map for profile '{resolvedProfileId.Value}': '{existingName}' was overridden by '{map.name}'.");
                }

                if (!entry.UsesMapTargetProfile)
                {
                    _validationIssues.Add(
                        $"Entry #{i + 1} map '{map.name}' has invalid target profile; using fallback entry id '{resolvedProfileId.Value}'.");
                }

                _cache[resolvedProfileId.Value] = map;
            }

            _cacheFingerprint = nextFingerprint;
            _isCacheValid = true;
        }

        private int ComputeCacheFingerprint()
        {
            unchecked
            {
                int hash = 17;
                int count = _entries != null ? _entries.Count : 0;
                hash = (hash * 31) + count;

                if (_entries == null) return hash;

                for (int i = 0; i < _entries.Count; i++)
                {
                    ProfileDefaultMapEntry entry = _entries[i];
                    hash = (hash * 31) + (entry != null ? entry.ComputeFingerprint() : 0);
                }

                return hash;
            }
        }

        private void InvalidateCache() => _isCacheValid = false;

        [Serializable]
        public sealed class ProfileDefaultMapEntry
        {
            [SerializeField] private string _profileId = string.Empty;
            [SerializeField] private ConvaiLipSyncMapAsset _defaultMap;

            public LipSyncProfileId ProfileId => ResolveProfileId();
            public ConvaiLipSyncMapAsset DefaultMap => _defaultMap;
            public bool UsesMapTargetProfile => ResolveMapTargetProfileId().IsValid;

            public void NormalizeProfileId()
            {
                _profileId = LipSyncProfileId.Normalize(_profileId);
                LipSyncProfileId mapTargetProfile = ResolveMapTargetProfileId();
                if (mapTargetProfile.IsValid) _profileId = mapTargetProfile.Value;
            }

            public LipSyncProfileId ResolveProfileId()
            {
                LipSyncProfileId mapTargetProfile = ResolveMapTargetProfileId();
                if (mapTargetProfile.IsValid) return mapTargetProfile;

                return new LipSyncProfileId(_profileId);
            }

            internal int ComputeFingerprint()
            {
                unchecked
                {
                    int hash = 17;
                    hash = (hash * 31) + (_defaultMap != null ? _defaultMap.GetEntityId().GetHashCode() : 0);
                    hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(ResolveProfileId().Value);
                    hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(LipSyncProfileId.Normalize(_profileId));
                    return hash;
                }
            }

            private LipSyncProfileId ResolveMapTargetProfileId() =>
                _defaultMap != null ? _defaultMap.TargetProfileId : default;
        }
    }
}
