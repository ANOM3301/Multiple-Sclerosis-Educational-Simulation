using Convai.Editor.Settings.Services;
using Convai.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Convai.Editor.Settings.Views
{
    /// <summary>
    ///     Credentials section: API key entry with validation, and the service
    ///     environment preset (Production/Beta/Custom).
    /// </summary>
    public sealed class CredentialsSectionView : ConvaiSettingsSectionView
    {
        private readonly TextField _apiKeyField;
        private readonly Button _validateButton;
        private readonly VisualElement _badgeDot;
        private readonly Label _badgeText;
        private readonly VisualElement _customGroup;
        private readonly SerializedProperty _environmentProperty;
        private readonly SerializedProperty _serverUrlProperty;
        private readonly SerializedProperty _customRestBaseUrlProperty;
        private bool _showApiKey;

        public CredentialsSectionView(ConvaiSettingsViewContext context)
            : base(context, "Credentials")
        {
            Body.Add(ConvaiSettingsUi.CreateHelpBox(
                "Project-wide credentials shared across scenes. Runtime credential providers can still override them in code. " +
                "The key is stored obfuscated (not encrypted) inside the project's settings asset.",
                HelpBoxMessageType.Info));

            _apiKeyField = new TextField("API Key")
            {
                isPasswordField = true,
                maskChar = '●',
                tooltip = "Your Convai dashboard API key."
            };
            _apiKeyField.AddToClassList("convai-settings-field");
            _apiKeyField.AddToClassList("convai-settings-apikey-field");
            _apiKeyField.RegisterValueChangedCallback(_ => OnApiKeyInputChanged());

            var showHideButton = new Button { text = "Show" };
            showHideButton.AddToClassList("convai-settings-inline-button");
            showHideButton.clicked += () =>
            {
                _showApiKey = !_showApiKey;
                _apiKeyField.isPasswordField = !_showApiKey;
                showHideButton.text = _showApiKey ? "Hide" : "Show";
            };

            VisualElement keyRow = ConvaiSettingsUi.CreateRow(_apiKeyField, showHideButton);
            keyRow.AddToClassList("convai-settings-apikey-row");
            Body.Add(keyRow);

            _validateButton = new Button(OnValidateClicked) { text = "Validate & Save" };
            _validateButton.AddToClassList("convai-settings-primary-button");

            var clearButton = new Button(OnClearClicked)
            {
                text = "Clear",
                tooltip = "Remove the saved API key from this project."
            };
            clearButton.AddToClassList("convai-settings-inline-button");

            VisualElement badge = ConvaiSettingsUi.CreateStatusBadge(out _badgeDot, out _badgeText);
            VisualElement actionRow = ConvaiSettingsUi.CreateRow(_validateButton, clearButton, badge);
            actionRow.AddToClassList("convai-settings-action-row");
            Body.Add(actionRow);

            _environmentProperty = context.Settings.FindProperty("_apiEnvironment");
            _serverUrlProperty = context.Settings.FindProperty("_serverUrl");
            _customRestBaseUrlProperty = context.Settings.FindProperty("_customRestBaseUrl");

            var environmentField = new PropertyField(_environmentProperty, "Environment")
            {
                tooltip = "Convai service environment. Keep Production unless directed otherwise."
            };
            environmentField.AddToClassList("convai-settings-field");
            Body.Add(environmentField);

            _customGroup = new VisualElement();
            _customGroup.AddToClassList("convai-settings-subgroup");
            _customGroup.Add(ConvaiSettingsUi.CreateHelpBox(
                "Custom endpoints are active for this project. Keep this only for staging, enterprise, or support-directed setups.",
                HelpBoxMessageType.Info));
            var serverUrlField = new PropertyField(_serverUrlProperty, "Core Server URL")
            {
                tooltip = "Realtime server used for room connect requests."
            };
            serverUrlField.AddToClassList("convai-settings-field");
            _customGroup.Add(serverUrlField);
            _customGroup.Add(ConvaiSettingsUi.CreateField(context.Settings, "_customRestBaseUrl",
                "REST Base URL", "REST API base URL. Empty uses the production URL."));
            Body.Add(_customGroup);

            this.TrackPropertyValue(_environmentProperty, _ => OnStoredCredentialConfigurationChanged());
            this.TrackPropertyValue(_serverUrlProperty, _ => OnStoredCredentialConfigurationChanged());
            this.TrackPropertyValue(_customRestBaseUrlProperty, _ => OnStoredCredentialConfigurationChanged());
            Context.CredentialsChanged += RefreshFromSettings;
            RefreshEnvironmentGroup();
        }

        public override void Activate()
        {
            if (!HasValidSettings) return;

            RefreshFromSettings();
        }

        public override void Deactivate() => Context.Validation.CancelPending();

        protected override bool ConfirmReset() => EditorUtility.DisplayDialog(
            "Reset Credentials",
            "Clear the saved API key and restore Production endpoint defaults?",
            "Reset",
            "Cancel");

        protected override void ResetToDefaults()
        {
            Context.Validation.CancelPending();
            _validateButton.SetEnabled(true);
            _apiKeyField.SetValueWithoutNotify(string.Empty);
            Context.Settings.FindProperty("_apiKeyObfuscated").stringValue = string.Empty;
            Context.Settings.FindProperty("_apiKey").stringValue = string.Empty;
            _environmentProperty.enumValueIndex = (int)ConvaiApiEnvironment.Production;
            _serverUrlProperty.stringValue = ConvaiSettings.DefaultCoreServerUrl;
            _customRestBaseUrlProperty.stringValue = string.Empty;
        }

        protected override void OnResetApplied()
        {
            Context.NotifyCredentialsChanged();
        }

        private ConvaiApiEnvironment CurrentEnvironment =>
            (ConvaiApiEnvironment)_environmentProperty.enumValueIndex;

        private string CurrentCustomRestBaseUrl => _customRestBaseUrlProperty.stringValue;

        private void RefreshFromSettings()
        {
            if (!HasValidSettings) return;

            Context.Settings.Update();
            var settings = (ConvaiSettings)Context.Settings.targetObject;
            _apiKeyField.SetValueWithoutNotify(settings.ApiKey);
            RefreshEnvironmentGroup();
            RefreshBadgeForCurrentInput();
        }

        private void OnApiKeyInputChanged()
        {
            CancelValidationForEditedCredentials();
            RefreshBadgeForCurrentInput();
        }

        private void OnStoredCredentialConfigurationChanged()
        {
            CancelValidationForEditedCredentials();
            RefreshEnvironmentGroup();
            RefreshBadgeForCurrentInput();
            Context.RequestSave();
            Context.NotifyCredentialsChanged();
        }

        private void CancelValidationForEditedCredentials()
        {
            if (!Context.Validation.IsValidating) return;

            Context.Validation.CancelPending();
            _validateButton.SetEnabled(true);
        }

        private void RefreshEnvironmentGroup()
        {
            if (!HasValidSettings) return;

            bool isCustom = CurrentEnvironment == ConvaiApiEnvironment.Custom;
            _customGroup.style.display = isCustom ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void RefreshBadgeForCurrentInput()
        {
            if (!HasValidSettings) return;

            string key = _apiKeyField.value?.Trim();
            if (string.IsNullOrEmpty(key))
            {
                ConvaiSettingsUi.SetBadgeState(_badgeDot, ConvaiSettingsBadgeState.Warning);
                _badgeText.text = "No API key configured";
                return;
            }

            string customBase = CurrentEnvironment == ConvaiApiEnvironment.Custom
                ? CurrentCustomRestBaseUrl
                : string.Empty;
            if (ApiKeyValidationService.TryGetCachedResult(key, CurrentEnvironment, customBase,
                    out ApiKeyValidationResult cached))
            {
                if (cached.IsValid)
                {
                    ConvaiSettingsUi.SetBadgeState(_badgeDot, ConvaiSettingsBadgeState.Ok);
                    _badgeText.text = "Key valid";
                }
                else
                {
                    ConvaiSettingsUi.SetBadgeState(_badgeDot, ConvaiSettingsBadgeState.Error);
                    _badgeText.text = string.IsNullOrEmpty(cached.Message) ? "Key invalid" : cached.Message;
                }

                return;
            }

            ConvaiSettingsUi.SetBadgeState(_badgeDot, ConvaiSettingsBadgeState.Neutral);
            _badgeText.text = "Not validated";
        }

        private void OnValidateClicked()
        {
            if (!HasValidSettings || Context.Validation.IsValidating) return;

            string key = _apiKeyField.value?.Trim();
            if (string.IsNullOrEmpty(key))
            {
                ConvaiSettingsUi.SetBadgeState(_badgeDot, ConvaiSettingsBadgeState.Warning);
                _badgeText.text = "Enter an API key first";
                return;
            }

            ConvaiApiEnvironment environment = CurrentEnvironment;
            string customBase = environment == ConvaiApiEnvironment.Custom ? CurrentCustomRestBaseUrl : string.Empty;

            _validateButton.SetEnabled(false);
            ConvaiSettingsUi.SetBadgeState(_badgeDot, ConvaiSettingsBadgeState.Pending);
            _badgeText.text = "Validating…";

            Context.Validation.Validate(key, environment, customBase, result =>
            {
                _validateButton.SetEnabled(true);
                if (!HasValidSettings) return;

                if (result.IsValid)
                {
                    var settings = (ConvaiSettings)Context.Settings.targetObject;
                    settings.SetApiKey(key);
                    EditorUtility.SetDirty(settings);
                    Context.Settings.Update();
                    Context.RequestSave();
                    Context.NotifyCredentialsChanged();
                }

                DisplayValidationResult(result);
            });
        }

        private void DisplayValidationResult(ApiKeyValidationResult result)
        {
            ConvaiSettingsUi.SetBadgeState(
                _badgeDot,
                result.IsValid ? ConvaiSettingsBadgeState.Ok : ConvaiSettingsBadgeState.Error);
            _badgeText.text = result.IsValid
                ? "Key valid"
                : string.IsNullOrEmpty(result.Message) ? "Key invalid" : result.Message;
        }

        private void OnClearClicked()
        {
            if (!HasValidSettings) return;

            Context.Validation.CancelPending();
            _validateButton.SetEnabled(true);
            _apiKeyField.SetValueWithoutNotify(string.Empty);

            var settings = (ConvaiSettings)Context.Settings.targetObject;
            settings.SetApiKey(string.Empty);
            EditorUtility.SetDirty(settings);
            Context.Settings.Update();
            Context.RequestSave();
            Context.NotifyCredentialsChanged();
            RefreshBadgeForCurrentInput();
        }
    }
}
