using System.IO;
using System.Reflection;
using Convai.Editor.Inspectors;
using Convai.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Convai.Tests.EditMode.Runtime
{
    public class ConfigurationNamingAndEditorTests
    {
        [Test]
        public void RoomManagerProfileAssetMenu_UsesUpdatedUserFacingName()
        {
            var attribute = typeof(ConvaiRoomManagerProfile).GetCustomAttribute<CreateAssetMenuAttribute>();
            Assert.IsNotNull(attribute);
            Assert.AreEqual("Convai/Room Manager Profile", attribute.menuName);
            Assert.AreEqual("ConvaiRoomManagerProfile", attribute.fileName);
        }

        [Test]
        public void CharacterConfigAssetMenu_UsesUpdatedUserFacingName()
        {
            var attribute = typeof(ConvaiCharacterProfile).GetCustomAttribute<CreateAssetMenuAttribute>();
            Assert.IsNotNull(attribute);
            Assert.AreEqual("Convai/Character Profile", attribute.menuName);
            Assert.AreEqual("ConvaiCharacterProfile", attribute.fileName);
        }

        [Test]
        public void CharacterEditor_UsesTrimmedPrimarySections()
        {
            FieldInfo field = typeof(ConvaiCharacterEditor).GetField(
                "PrimarySectionTitles",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(field);

            var titles = (string[])field.GetValue(null);
            CollectionAssert.AreEqual(
                new[] { "Configuration", "Identity", "Audio", "Session", "Validation" },
                titles);
        }

        [Test]
        public void ProjectSettingsDoc_UsesRoomManagerProfileAndCharacterProfileTerminology()
        {
            string path = Path.GetFullPath(Path.Combine(
                UnityEngine.Application.dataPath,
                "..",
                "Packages",
                "com.convai.convai-sdk-for-unity",
                "Documentation~",
                "PROJECT-SETTINGS.md"));
            Assert.IsTrue(File.Exists(path), $"Expected PROJECT-SETTINGS.md at {path}");
            string text = File.ReadAllText(path);

            StringAssert.Contains("## Room Manager Profile and Character Profile", text);
            StringAssert.Contains("### Room Manager Profile", text);
            StringAssert.Contains("### Character Profile", text);
        }
    }
}
