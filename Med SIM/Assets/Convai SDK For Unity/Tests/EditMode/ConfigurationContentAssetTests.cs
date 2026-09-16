using Convai.Editor.ConfigurationWindow.Content;
using NUnit.Framework;

namespace Convai.Tests.EditMode
{
    public class ConfigurationContentAssetTests
    {
        [Test]
        public void ConfigurationContent_InstanceHasDefaultWelcomeText()
        {
            var content = ConvaiConfigurationContent.Instance;

            Assert.IsNotNull(content);
            Assert.IsNotEmpty(content.WelcomeHeader);
            Assert.IsNotEmpty(content.WelcomeSubheader);
            Assert.IsNotEmpty(content.QuickStartInstructions);
        }

        [Test]
        public void ReleaseNotesAsset_InstanceHasAtLeastOneEntry()
        {
            var notes = ConvaiReleaseNotesAsset.Instance;

            Assert.IsNotNull(notes);
            Assert.IsNotNull(notes.Entries);
            Assert.Greater(notes.Entries.Count, 0);
            Assert.IsNotEmpty(notes.Entries[0].Version);
        }
    }
}
