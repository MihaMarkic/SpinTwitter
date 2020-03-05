using NUnit.Framework;
using SpinTwitter.Services.Implementation;
using System.Net.Http;

namespace SpinTwitter.Test.Services.Implementation
{
    public class SpinRssTest
    {
        SpinRss target;

        [SetUp]
        public void Setup()
        {
            target = new SpinRss(new HttpClient());
        }
        [TestFixture]
        public class ExtractId: SpinRssTest
        {
            [TestCase("https://spin3.sos112.si/javno/zemljevid/272006", ExpectedResult = 272006)]
            [TestCase("https://spin3.sos112.si/javno/zemljevid/272005", ExpectedResult = 272005)]
            public uint? GivenCorrectGuidContent_ParsesId(string source)
            {
                return target.ExtractId(source);
            }
        }
    }
}
