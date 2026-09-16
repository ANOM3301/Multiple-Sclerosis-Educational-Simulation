using Convai.Editor.ConfigurationWindow.Components;
using NUnit.Framework;

namespace Convai.Tests.EditMode
{
    public class ConfigurationWindowContextTests
    {
        [Test]
        public void ApiKeyAvailabilitySubscribers_CountTracksSubscriptions()
        {
            var context = new ConfigurationWindowContext();
            void Handler(bool _) { }

            int initialCount = context.ApiKeyAvailabilitySubscriberCount;
            context.ApiKeyAvailabilityChanged += Handler;
            Assert.AreEqual(initialCount + 1, context.ApiKeyAvailabilitySubscriberCount);

            context.ApiKeyAvailabilityChanged -= Handler;
            Assert.AreEqual(initialCount, context.ApiKeyAvailabilitySubscriberCount);
        }

        [Test]
        public void NotifyApiKeyUpdated_DoesNotThrow()
        {
            var context = new ConfigurationWindowContext();
            Assert.DoesNotThrow(() => context.NotifyApiKeyUpdated());
        }

        [Test]
        public void NotifyApiKeyUpdated_AlwaysNotifiesSubscribers()
        {
            var context = new ConfigurationWindowContext();
            int notificationCount = 0;
            context.ApiKeyAvailabilityChanged += _ => notificationCount++;

            context.NotifyApiKeyUpdated();
            context.NotifyApiKeyUpdated();

            Assert.AreEqual(2, notificationCount);
        }
    }
}
