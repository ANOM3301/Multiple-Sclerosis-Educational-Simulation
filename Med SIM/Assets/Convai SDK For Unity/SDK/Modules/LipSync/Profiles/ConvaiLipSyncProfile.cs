using Convai.Domain.Models.LipSync;
using UnityEngine;

namespace Convai.Modules.LipSync.Profiles
{
    [CreateAssetMenu(fileName = "ConvaiLipSyncProfile", menuName = "Convai/Lip Sync/Profile")]
    public sealed class ConvaiLipSyncProfile : ScriptableObject
    {
        [SerializeField] private string _profileId = LipSyncProfileId.ARKitValue;
        [SerializeField] private string _displayName = "ARKit";
        [SerializeField] private string _transportFormat = "arkit";

        public LipSyncProfileId ProfileId => new(_profileId);
        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? ProfileId.Value : _displayName.Trim();
        public string TransportFormat => LipSyncProfileId.Normalize(_transportFormat);

        public bool IsValid =>
            ProfileId.IsValid &&
            !string.IsNullOrWhiteSpace(TransportFormat);

        private void OnValidate()
        {
            _profileId = LipSyncProfileId.Normalize(_profileId);
            _transportFormat = LipSyncProfileId.Normalize(_transportFormat);
            _displayName = string.IsNullOrWhiteSpace(_displayName) ? _profileId : _displayName.Trim();
        }

        public string DescribeValidationIssue()
        {
            if (!ProfileId.IsValid)
                return "ProfileId is empty.";
            if (string.IsNullOrWhiteSpace(TransportFormat))
                return "TransportFormat is empty.";
            return string.Empty;
        }
    }
}
