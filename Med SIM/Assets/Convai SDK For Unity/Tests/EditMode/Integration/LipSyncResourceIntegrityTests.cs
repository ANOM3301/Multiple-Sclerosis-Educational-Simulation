using System;
using System.Collections.Generic;
using Convai.Domain.Models.LipSync;
using Convai.Modules.LipSync;
using Convai.Modules.LipSync.Profiles;
using NUnit.Framework;
using UnityEditor;

namespace Convai.Tests.EditMode.Integration
{
    [TestFixture]
    public sealed class LipSyncResourceIntegrityTests
    {
        private const string Root =
            "Packages/com.convai.convai-sdk-for-unity/SamplesShared/Resources/LipSync";

        [Test]
        public void ShippedMetaHumanAssets_UseSemanticProfileIdAndMhaWireFormat()
        {
            ConvaiLipSyncProfile profile = AssetDatabase.LoadAssetAtPath<ConvaiLipSyncProfile>(
                $"{Root}/Profiles/ConvaiLipSyncProfile_MetaHuman.asset");
            ConvaiLipSyncMapAsset map = AssetDatabase.LoadAssetAtPath<ConvaiLipSyncMapAsset>(
                $"{Root}/DefaultMaps/ConvaiLipSyncDefaultMap_MetaHuman.asset");
            ConvaiLipSyncDefaultMapRegistry registry =
                AssetDatabase.LoadAssetAtPath<ConvaiLipSyncDefaultMapRegistry>(
                    $"{Root}/DefaultMaps/LipSyncDefaultMapRegistry.asset");

            Assert.NotNull(profile);
            Assert.NotNull(map);
            Assert.NotNull(registry);
            Assert.AreEqual(LipSyncProfileId.MetaHuman, profile.ProfileId);
            Assert.AreEqual("mha", profile.TransportFormat);
            Assert.AreEqual(LipSyncProfileId.MetaHuman, map.TargetProfileId);
            Assert.AreSame(map, registry.GetForProfile(LipSyncProfileId.MetaHuman));

            Assert.Throws<NotSupportedException>(() =>
                ((IList<ConvaiLipSyncMapAsset.BlendshapeMappingEntry>)map.Mappings).Clear());
            Assert.Throws<NotSupportedException>(() =>
                ((IList<ConvaiLipSyncProfile>)LipSyncProfileCatalog.GetProfiles()).Clear());
        }

        [Test]
        public void LegacyMhaProfileId_FallsBackToMetaHumanOnlyWhenExactProfileIsAbsent()
        {
            LipSyncProfileCatalog.ClearCachesForTests();
            Assert.IsTrue(LipSyncProfileCatalog.TryGetProfile("mha", out ConvaiLipSyncProfile profile));
            Assert.AreEqual(LipSyncProfileId.MetaHuman, profile.ProfileId);
        }
    }
}
