using NUnit.Framework;
using SpinTwitter.Services.Implementation;
using System;
using System.IO;

namespace SpinTwitter.Test.Services.Implementation
{
    public class SpinFeedTest
    {
        static string RootPath => TestContext.CurrentContext.TestDirectory;
        [TestFixture]
        public class ConvertFromString: SpinFeedTest
        {
            [Test]
            public void CorrectlyParsesSampleData()
            {
                string text = File.ReadAllText(Path.Combine(RootPath, "sample.json"));

                var actual = SpinFeed.ConvertFromString(text);

                Assert.That(actual.Value.Length, Is.EqualTo(30));
                var seven = actual.Value[6];
                Assert.That(seven.Caption, Is.EqualTo("Reševanje obolelih in helikopterski prevozi med bolnišnicami"));
                Assert.That(seven.Text, Is.EqualTo("Ob 12.21 je posadka helikopterja SV, v spremstvu ekipe HENMP iz Ljutomera prepeljala obolelo osebo v UKC Maribor. Kraj pristanka helikopterja so zavarovali gasilci PGD Ljutomer."));
                Assert.That(seven.WgsLat, Is.EqualTo(46.525m));
                Assert.That(seven.WgsLon, Is.EqualTo(16.191m));
                Assert.That(seven.ReportDate, Is.EqualTo(new DateTimeOffset(2019, 04, 08, 12,50, 15, 163, TimeSpan.FromHours(2))));
            }
        }
    }
}
