using NUnit.Framework;
using System.Diagnostics;
using VectorLiteDB.Tests.TestData;

namespace VectorLiteDB.Tests
{
    [TestFixture]
    public class PerformanceTests : BaseTest
    {
        [Test]
        [TestCase(100)]
        [TestCase(1000)]
        [TestCase(10000)]
        public void Search_Performance_ScalesLinearly(int entryCount)
        {
            // Arrange
            var testData = new TestDataBuilder()
                .WithRandomEntries(entryCount)
                .Build();

            foreach (var entry in testData)
                Store.Add(entry);

            var query = testData.First().Embedding!;

            // Act
            var stopwatch = Stopwatch.StartNew();
            var results = Store.Search(query, 10);
            stopwatch.Stop();

            // Assert
            Assert.AreEqual(10, results.Count);
            var expectedMaxTime = entryCount * 3.0; // Allow 3 seconds per 1000 entries for brute force
            Assert.Less(stopwatch.ElapsedMilliseconds, expectedMaxTime,
                $"Search took too long: {stopwatch.ElapsedMilliseconds}ms for {entryCount} entries");
        }

        [Test]
        public void MemoryUsage_RemainsStable()
        {
            var initialMemory = GC.GetTotalMemory(true);

            var testData = new TestDataBuilder()
                .WithRandomEntries(1000, 1536) // Large vectors
                .Build();

            foreach (var entry in testData)
                Store.Add(entry);

            GC.Collect();
            var finalMemory = GC.GetTotalMemory(true);
            var memoryIncrease = finalMemory - initialMemory;

            // Allow reasonable memory increase for data
            Assert.Less(memoryIncrease, 50 * 1024 * 1024, "Memory usage increased too much"); // 50MB limit
        }

        [Test]
        public void Stats_TrackingAccurate()
        {
            var initialStats = Store.GetStats();

            var entries = new TestDataBuilder().WithRandomEntries(100).Build();
            foreach (var entry in entries)
                Store.Add(entry);

            // Perform searches
            for (int i = 0; i < 5; i++)
            {
                Store.Search(entries[i].Embedding!, 5);
            }

            var finalStats = Store.GetStats();

            Assert.AreEqual(initialStats.TotalEntries + 100, finalStats.TotalEntries);
            Assert.Greater(finalStats.Uptime, initialStats.Uptime);
            Assert.GreaterOrEqual(finalStats.TotalSearches, initialStats.TotalSearches + 5);
            Assert.Greater(finalStats.AverageSearchTimeMs, 0);
        }
    }
}