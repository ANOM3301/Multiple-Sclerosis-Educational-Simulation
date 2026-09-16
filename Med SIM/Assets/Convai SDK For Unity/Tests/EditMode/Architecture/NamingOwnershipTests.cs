using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Convai.Domain.Errors;
using NUnit.Framework;

namespace Convai.Tests.EditMode.Architecture
{
    /// <summary>
    ///     Architectural guardrails for naming and ownership conventions.
    /// </summary>
    public class NamingOwnershipTests
    {
        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }

            return null;
        }

        private static Assembly FindAssembly(string name)
        {
            return AppDomain.CurrentDomain
                .GetAssemblies()
                .FirstOrDefault(a => string.Equals(a.GetName().Name, name, StringComparison.Ordinal));
        }

        #region Canonical Entrypoints

        [Test]
        [Category("Architecture")]
        public void CanonicalEntrypoints_ExistInCorrectAssemblies()
        {
            // ConvaiManager - Scene entrypoint (Runtime)
            Type convaiManager = FindType("Convai.Runtime.Components.ConvaiManager");
            Assert.IsNotNull(convaiManager, "ConvaiManager should exist in Convai.Runtime.Components");


            // ConvaiSDK - Metadata API (Application)
            Type convaiSDK = FindType("Convai.Application.ConvaiSDK");
            Assert.IsNotNull(convaiSDK, "ConvaiSDK should exist in Convai.Application");
        }

        #endregion

        #region Error Code Conventions

        [Test]
        [Category("Architecture")]
        public void SessionErrorCodes_UseDotNotationFormat()
        {
            // All error codes should use dot.notation format (category.subcategory_detail)
            FieldInfo[] fields = typeof(SessionErrorCodes).GetFields(BindingFlags.Public | BindingFlags.Static);

            foreach (FieldInfo field in fields.Where(f => f.FieldType == typeof(string)))
            {
                string value = (string)field.GetValue(null);
                if (string.IsNullOrEmpty(value))
                    continue;

                Assert.IsTrue(value.Contains("."),
                    $"SessionErrorCodes.{field.Name} = \"{value}\" should use dot.notation format (e.g., 'category.detail')");

                Assert.IsFalse(value.Any(char.IsUpper),
                    $"SessionErrorCodes.{field.Name} = \"{value}\" should be lowercase dot.notation, not SCREAMING_CASE");
            }
        }

        [Test]
        [Category("Architecture")]
        public void SessionErrorCodes_IsCanonicalSource()
        {
            // SessionErrorCodes should exist and have vision error codes
            Assert.IsNotNull(FindType("Convai.Domain.Errors.SessionErrorCodes"),
                "SessionErrorCodes should be the canonical error codes source");

            // Verify key vision codes exist
            Assert.IsNotNull(SessionErrorCodes.VisionCameraLost);
            Assert.IsNotNull(SessionErrorCodes.VisionDeviceNotFound);
            Assert.IsNotNull(SessionErrorCodes.ConnectionTimeout);
        }

        #endregion

        #region Legacy API Deprecation

        [Test]
        [Category("Architecture")]
        public void DeprecatedNetworkManagerLikeApis_AreNotPresent()
        {
            Assert.IsNull(FindType("Convai.Runtime.Adapters.Networking.ConvaiNetworkManager"));
            Assert.IsNull(FindType("Convai.Runtime.Utilities.NetworkCheck"));
        }

        [Test]
        [Category("Architecture")]
        public void EditorNamespaces_DoNotContainConfigurationWindowUnderscore()
        {
            Assembly editorAssembly = FindAssembly("Convai.Editor");
            if (editorAssembly == null)
            {
                Assert.Inconclusive("Convai.Editor assembly is not loaded.");
                return;
            }

            string[] invalidNamespaces = editorAssembly
                .GetTypes()
                .Where(t => !string.IsNullOrEmpty(t.Namespace) &&
                            t.Namespace.IndexOf("Configuration_Window", StringComparison.Ordinal) >= 0)
                .Select(t => t.Namespace)
                .Distinct()
                .ToArray();

            Assert.IsEmpty(invalidNamespaces,
                $"Found legacy editor namespaces: [{string.Join(", ", invalidNamespaces)}]");
        }

        [Test]
        [Category("Architecture")]
        public void TransportTypes_UseExplicitTransportPrefixes()
        {
            Assert.IsNull(FindType("Convai.Infrastructure.Networking.Transport.SessionInfo"));
            Assert.IsNull(FindType("Convai.Infrastructure.Networking.Transport.ParticipantInfo"));

            Assert.IsNotNull(FindType("Convai.Infrastructure.Networking.Transport.TransportSessionInfo"));
            Assert.IsNotNull(FindType("Convai.Infrastructure.Networking.Transport.TransportParticipantInfo"));
        }

        [Test]
        [Category("Architecture")]
        public void NotificationBridge_DoesNotExposeLocalErrorCodeConstants() =>
            Assert.IsNull(FindType("Convai.Runtime.Presentation.Services.ConvaiNotificationEventBridge+ErrorCodes"));

        [Test]
        [Category("Architecture")]
        public void LegacyMismatchedFileNames_AreRemoved()
        {
            string projectRoot = Directory.GetCurrentDirectory();
            string packageRoot = Path.Combine(projectRoot, "Packages", "com.convai.convai-sdk-for-unity");

            string legacyPersistencePath = Path.Combine(packageRoot, "SDK", "Infrastructure", "Persistence",
                "PlayerPrefsSessionPersistence.cs");
            string legacyNotificationPath = Path.Combine(packageRoot, "SDK", "Runtime", "Presentation", "Views",
                "Notifications", "NotificationUI.cs");

            Assert.IsFalse(File.Exists(legacyPersistencePath), $"Legacy file still exists: {legacyPersistencePath}");
            Assert.IsFalse(File.Exists(legacyNotificationPath), $"Legacy file still exists: {legacyNotificationPath}");
        }

        #endregion
    }
}
