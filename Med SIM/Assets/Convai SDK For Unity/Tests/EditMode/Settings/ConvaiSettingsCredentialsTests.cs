using System.Reflection;
using Convai.Editor.Settings;
using Convai.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Convai.Tests.EditMode.Settings
{
    public class ConvaiSettingsCredentialsTests
    {
        private ConvaiSettings _settings;

        [SetUp]
        public void SetUp() => _settings = ScriptableObject.CreateInstance<ConvaiSettings>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_settings);

        [Test]
        public void SetApiKey_StoresObfuscated_AndApiKeyReturnsPlain()
        {
            _settings.SetApiKey("my-api-key");

            Assert.AreEqual("my-api-key", _settings.ApiKey);
            Assert.IsTrue(_settings.HasApiKey);

            var obfuscated = (string)GetField("_apiKeyObfuscated");
            StringAssert.StartsWith(ConvaiApiKeyObfuscation.Prefix, obfuscated);
            StringAssert.DoesNotContain("my-api-key", obfuscated);
            Assert.AreEqual(string.Empty, (string)GetField("_apiKey"));
        }

        [Test]
        public void SetApiKey_TrimsInput_AndEmptyClears()
        {
            _settings.SetApiKey("  padded-key  ");
            Assert.AreEqual("padded-key", _settings.ApiKey);

            _settings.SetApiKey(string.Empty);
            Assert.IsFalse(_settings.HasApiKey);
            Assert.AreEqual(string.Empty, _settings.ApiKey);
        }

        [Test]
        public void MigrateLegacyDataIfNeeded_ObfuscatesPlaintextKey_AndClearsLegacyField()
        {
            SetField("_apiKey", "legacy-plaintext-key");

            _settings.MigrateLegacyDataIfNeeded();

            Assert.AreEqual("legacy-plaintext-key", _settings.ApiKey);
            Assert.AreEqual(string.Empty, (string)GetField("_apiKey"));
            StringAssert.StartsWith(ConvaiApiKeyObfuscation.Prefix, (string)GetField("_apiKeyObfuscated"));
        }

        [Test]
        public void MigrateLegacyDataIfNeeded_IsIdempotent()
        {
            SetField("_apiKey", "legacy-key");
            _settings.MigrateLegacyDataIfNeeded();
            string firstPayload = (string)GetField("_apiKeyObfuscated");

            _settings.MigrateLegacyDataIfNeeded();

            Assert.AreEqual(firstPayload, (string)GetField("_apiKeyObfuscated"));
            Assert.AreEqual("legacy-key", _settings.ApiKey);
        }

        [Test]
        public void MigrateLegacyDataIfNeeded_ClearsPlaintext_WhenObfuscatedValueAlreadyExists()
        {
            SetField("_apiKey", "stale-plaintext-key");
            SetField("_apiKeyObfuscated", ConvaiApiKeyObfuscation.Obfuscate("current-key"));

            _settings.MigrateLegacyDataIfNeeded();

            Assert.AreEqual("current-key", _settings.ApiKey);
            Assert.AreEqual(string.Empty, (string)GetField("_apiKey"));
        }

        [Test]
        public void ApiKey_FallsBackToLegacyPlaintext_WhenNotMigrated()
        {
            SetField("_apiKey", "unmigrated-key");
            Assert.AreEqual("unmigrated-key", _settings.ApiKey);
            Assert.IsTrue(_settings.HasApiKey);
        }

        [TestCase(ConvaiApiEnvironment.Production)]
        [TestCase(ConvaiApiEnvironment.Beta)]
        public void ServerUrl_IgnoresSerializedUrl_ForManagedEnvironments(ConvaiApiEnvironment environment)
        {
            SetField("_apiEnvironment", environment);
            SetField("_serverUrl", "https://custom.example.com");

            Assert.AreEqual(ConvaiSettings.DefaultCoreServerUrl, _settings.ServerUrl);
            Assert.AreEqual(string.Empty, _settings.RestBaseUrlOverride);
        }

        [Test]
        public void ServerUrl_HonorsSerializedUrl_OnlyWhenCustom()
        {
            SetField("_apiEnvironment", ConvaiApiEnvironment.Custom);
            SetField("_serverUrl", "https://custom.example.com");
            SetField("_customRestBaseUrl", "https://rest.example.com/");

            Assert.AreEqual("https://custom.example.com", _settings.ServerUrl);
            Assert.AreEqual("https://rest.example.com/", _settings.RestBaseUrlOverride);
        }

        [Test]
        public void ServerUrl_Custom_WithEmptyUrl_FallsBackToDefault()
        {
            SetField("_apiEnvironment", ConvaiApiEnvironment.Custom);
            SetField("_serverUrl", "   ");

            Assert.AreEqual(ConvaiSettings.DefaultCoreServerUrl, _settings.ServerUrl);
        }

        [Test]
        public void CredentialsChanged_PropagatesAcrossMountedHosts()
        {
            using var projectSettings = new ConvaiSettingsViewContext(
                new SerializedObject(_settings), ConvaiSettingsHostKind.ProjectSettings);
            using var configurationWindow = new ConvaiSettingsViewContext(
                new SerializedObject(_settings), ConvaiSettingsHostKind.ConfigurationWindow);
            int projectNotifications = 0;
            int windowNotifications = 0;
            projectSettings.CredentialsChanged += () => projectNotifications++;
            configurationWindow.CredentialsChanged += () => windowNotifications++;

            projectSettings.NotifyCredentialsChanged();

            Assert.AreEqual(1, projectNotifications);
            Assert.AreEqual(1, windowNotifications);
        }

        private object GetField(string name) => Field(name).GetValue(_settings);

        private void SetField(string name, object value) => Field(name).SetValue(_settings, value);

        private static FieldInfo Field(string name) =>
            typeof(ConvaiSettings).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
    }
}
