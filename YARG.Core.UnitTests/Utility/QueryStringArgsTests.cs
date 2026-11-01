using NUnit.Framework;
using YARG.Core.Utility;

namespace YARG.Core.UnitTests.Utility
{
    public class QueryStringArgsTests
    {
        [TestCase("", new string[0])]
        [TestCase("?", new string[0])]
        [TestCase("?offline", new[] { "-offline" })]
        [TestCase("?mp-autotest=offline", new[] { "-mp-autotest", "offline" })]
        [TestCase("mp-autotest=join+127.0.0.1&mp-players=2", new[] { "-mp-autotest", "join", "127.0.0.1", "-mp-players", "2" })]
        [TestCase("?mp-song=My%20Song", new[] { "-mp-song", "My", "Song" })]
        [TestCase("?lang=pt-BR&lang=en-US", new[] { "-lang", "pt-BR", "-lang", "en-US" })]
        [TestCase("?a=&b", new[] { "-a", "-b" })]
        public void Parse_MapsQueryToArgs(string query, string[] expected)
        {
            Assert.That(QueryStringArgs.Parse(query), Is.EqualTo(expected));
        }
    }
}
