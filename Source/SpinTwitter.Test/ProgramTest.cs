using NUnit.Framework;
using SpinTwitter.Models;
using System;

namespace SpinTwitter.Test
{
    public class ProgramTest
    {
        [TestFixture]
        public class PurgeLastPublished: ProgramTest
        {
            [Test]
            public void When_PersistenceIsEmpty_And_NoItems_PersistenceIsEmpty()
            {
                var persistence = new Persistence();
                var values = new Value[0];

                Program.PurgeLastPublished(persistence, values);

                Assert.That(persistence.Dates.Count, Is.Zero);
            }

            [Test]
            public void When_PersistenceIsEmpty_And_ItemsContainsDate_PersistenceIsEmpty()
            {
                var persistence = new Persistence();
                var values = new Value[] { new Value { ReportDate = new DateTimeOffset(new DateTime(2019, 1, 2)) } };

                Program.PurgeLastPublished(persistence, values);

                Assert.That(persistence.Dates.Count, Is.Zero);
            }
            [Test]
            public void When_PersistenceContainsDate_And_ItemsContainsDifferentDate_PersistenceIsEmpty()
            {
                var persistence = new Persistence();
                persistence.Dates.Add(new DateTimeOffset(new DateTime(2019, 8, 2)));
                var values = new Value[] { new Value { ReportDate = new DateTimeOffset(new DateTime(2019, 1, 2)) } };

                Program.PurgeLastPublished(persistence, values);

                Assert.That(persistence.Dates.Count, Is.Zero);
            }
            [Test]
            public void When_PersistenceContainsDate_And_ItemsContainsEqualDate_PersistenceDoesNotChange()
            {
                var persistence = new Persistence();
                persistence.Dates.Add(new DateTimeOffset(new DateTime(2019, 1, 2)));
                var values = new Value[] { new Value { ReportDate = new DateTimeOffset(new DateTime(2019, 1, 2)) } };

                Program.PurgeLastPublished(persistence, values);

                Assert.That(persistence.Dates.Count, Is.EqualTo(1));
            }
        }
    }
}
