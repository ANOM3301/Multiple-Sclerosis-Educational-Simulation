using Convai.RestAPI;
using Convai.Runtime;
using NUnit.Framework;

namespace Convai.Tests.EditMode.Settings
{
    public class ConvaiRestOptionsFactoryTests
    {
        private const string DefaultProductionBaseUrl = "https://api.convai.com/";

        [Test]
        public void Create_Production_UsesProductionEnvironment()
        {
            ConvaiRestClientOptions options =
                ConvaiRestOptionsFactory.Create("key", ConvaiApiEnvironment.Production, null);

            Assert.AreEqual("key", options.ApiKey);
            Assert.AreEqual(ConvaiEnvironment.Production, options.Environment);
            Assert.AreEqual(DefaultProductionBaseUrl, options.ProductionBaseUrl);
        }

        [Test]
        public void Create_Beta_UsesBetaEnvironment()
        {
            ConvaiRestClientOptions options =
                ConvaiRestOptionsFactory.Create("key", ConvaiApiEnvironment.Beta, null);

            Assert.AreEqual(ConvaiEnvironment.Beta, options.Environment);
        }

        [Test]
        public void Create_Custom_OverridesProductionBaseUrl()
        {
            ConvaiRestClientOptions options =
                ConvaiRestOptionsFactory.Create("key", ConvaiApiEnvironment.Custom, " https://rest.example.com/ ");

            Assert.AreEqual(ConvaiEnvironment.Production, options.Environment);
            Assert.AreEqual("https://rest.example.com/", options.ProductionBaseUrl);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Create_Custom_WithEmptyOverride_KeepsProductionBaseUrl(string overrideUrl)
        {
            ConvaiRestClientOptions options =
                ConvaiRestOptionsFactory.Create("key", ConvaiApiEnvironment.Custom, overrideUrl);

            Assert.AreEqual(DefaultProductionBaseUrl, options.ProductionBaseUrl);
        }
    }
}
