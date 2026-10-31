using Newtonsoft.Json.Linq;
using NUnit.Framework;
using YARG.Core.IO;

namespace YARG.Core.UnitTests.IO
{
    public class ConfigBundleTests
    {
        [Test]
        public void RoundTrip_KeepsSettingsAndProfiles()
        {
            string bundle = ConfigBundle.Create("{\"A\":1}", "[{\"Name\":\"Ação 🎸\"}]");

            Assert.That(ConfigBundle.TryParse(bundle, out var settings, out var profiles, out var error), Is.True);
            Assert.That(JToken.DeepEquals(JToken.Parse(settings!), new JObject { ["A"] = 1 }), Is.True);
            Assert.That(JArray.Parse(profiles!)[0]["Name"]!.ToString(), Is.EqualTo("Ação 🎸"));
            Assert.That(error, Is.Null);
        }

        [Test]
        public void Create_OmitsNullParts()
        {
            string bundle = ConfigBundle.Create(null, "[]");

            Assert.That(bundle, Does.Not.Contain("\"settings\""));
            Assert.That(ConfigBundle.TryParse(bundle, out var settings, out var profiles, out _), Is.True);
            Assert.That(settings, Is.Null);
            Assert.That(profiles, Is.EqualTo("[]"));
        }

        [TestCase("not json")]
        [TestCase("[]")]
        [TestCase("{\"format\":\"other\",\"version\":1,\"settings\":{}}")]
        [TestCase("{\"format\":\"yarg-web-config\",\"version\":2,\"settings\":{}}")]
        [TestCase("{\"format\":\"yarg-web-config\",\"version\":1}")]
        [TestCase("{\"format\":\"yarg-web-config\",\"version\":1,\"settings\":[]}")]
        [TestCase("{\"format\":\"yarg-web-config\",\"version\":1,\"profiles\":{}}")]
        public void TryParse_RejectsInvalid(string json)
        {
            Assert.That(ConfigBundle.TryParse(json, out var settings, out var profiles, out var error), Is.False);
            Assert.That(settings, Is.Null);
            Assert.That(profiles, Is.Null);
            Assert.That(error, Is.Not.Empty);
        }
    }
}
