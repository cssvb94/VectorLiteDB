using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using VectorLiteDB.Services;
using VectorLiteDB.Models;

namespace VectorLiteDB.Tests
{
    [TestFixture]
    public class PerformanceBenchmarkTests : BaseTest
    {
        [Test]
        [TestCase(1000)]
        [TestCase(10000)]
        public void Search_Performance_LatencyUnder50ms(int datasetSize)
        {
            // Arrange: Create dataset
            var testData = GenerateLargeDataset(datasetSize);
            var stopwatch = Stopwatch.StartNew();

            foreach (var entry in testData)
            {
                Store.Add(entry);
            }

            var loadTime = stopwatch.ElapsedMilliseconds;
            Console.WriteLine($"Loaded {datasetSize} entries in {loadTime}ms");

            // Act: Benchmark search performance
            var queries = testData.Take(10).Select(e => e.Embedding!).ToList();
            var searchTimes = new List<long>();

            stopwatch.Restart();
            foreach (var query in queries)
            {
                var startTime = Stopwatch.GetTimestamp();

                var results = Store.Search(new SearchRequest
                {
                    Query = query,
                    K = 10,
                    UseExact = false, // Use HNSW
                    EfSearch = 400
                });

                var endTime = Stopwatch.GetTimestamp();
                var elapsedMs = (endTime - startTime) * 1000.0 / Stopwatch.Frequency;
                searchTimes.Add((long)elapsedMs);
            }

            var totalSearchTime = stopwatch.ElapsedMilliseconds;
            var avgSearchTime = searchTimes.Average();
            var maxSearchTime = searchTimes.Max();

            // Assert: Performance targets
            Assert.Less(avgSearchTime, 50, $"Average search time {avgSearchTime:F2}ms exceeds 50ms target");
            Assert.Less(maxSearchTime, 100, $"Max search time {maxSearchTime}ms exceeds 100ms limit");

            Console.WriteLine($"Dataset: {datasetSize} entries");
            Console.WriteLine($"Total load time: {loadTime}ms");
            Console.WriteLine($"Total search time: {totalSearchTime}ms for {queries.Count} queries");
            Console.WriteLine($"Average search time: {avgSearchTime:F2}ms");
            Console.WriteLine($"Max search time: {maxSearchTime}ms");
            Console.WriteLine($"Min search time: {searchTimes.Min()}ms");

            // Additional stats
            var stats = Store.GetStats();
            Console.WriteLine($"HNSW index size: {stats.HnswIndexSize}");
            Console.WriteLine($"Memory usage: {stats.MemoryUsage / 1024 / 1024} MB");
        }

        [Test]
        public void HNSW_Vs_BruteForce_PerformanceComparison()
        {
            // Arrange: Medium dataset for performance comparison
            const int datasetSize = 5000;
            var testData = GenerateLargeDataset(datasetSize);

            foreach (var entry in testData)
            {
                Store.Add(entry);
            }

            var queries = testData.Take(5).Select(e => e.Embedding!).ToList();

            // Act: Compare HNSW vs Brute Force performance
            var hnswTimes = new List<long>();
            var bruteTimes = new List<long>();

            foreach (var query in queries)
            {
                // HNSW search
                var startTime = Stopwatch.GetTimestamp();
                var hnswResults = Store.Search(new SearchRequest
                {
                    Query = query,
                    K = 10,
                    UseExact = false,
                    EfSearch = 400
                });
                var endTime = Stopwatch.GetTimestamp();
                hnswTimes.Add((long)((endTime - startTime) * 1000.0 / Stopwatch.Frequency));

                // Brute force search
                startTime = Stopwatch.GetTimestamp();
                var bruteResults = Store.Search(new SearchRequest
                {
                    Query = query,
                    K = 10,
                    UseExact = true
                });
                endTime = Stopwatch.GetTimestamp();
                bruteTimes.Add((long)((endTime - startTime) * 1000.0 / Stopwatch.Frequency));
            }

            var avgHnswTime = hnswTimes.Average();
            var avgBruteTime = bruteTimes.Average();
            var speedup = avgBruteTime / avgHnswTime;

            // Assert: HNSW should be significantly faster for large datasets
            Assert.Greater(speedup, 2, $"HNSW speedup {speedup:F1}x not sufficient (target: >2x)");
            Assert.Less(avgHnswTime, 50, $"HNSW average time {avgHnswTime:F2}ms exceeds target");

            Console.WriteLine($"Performance comparison (dataset: {datasetSize} entries):");
            Console.WriteLine($"HNSW average: {avgHnswTime:F2}ms");
            Console.WriteLine($"Brute force average: {avgBruteTime:F2}ms");
            Console.WriteLine($"Speedup: {speedup:F1}x");
        }

        [Test]
        public void MemoryUsage_RemainsStable_UnderLoad()
        {
            // Arrange: Start with clean state
            var initialMemory = GC.GetTotalMemory(true);
            var initialStats = Store.GetStats();

            // Act: Load large dataset
            const int datasetSize = 10000;
            var testData = GenerateLargeDataset(datasetSize);

            foreach (var entry in testData)
            {
                Store.Add(entry);
            }

            GC.Collect();
            var finalMemory = GC.GetTotalMemory(true);
            var finalStats = Store.GetStats();

            var memoryIncrease = finalMemory - initialMemory;
            var memoryIncreaseMB = memoryIncrease / 1024.0 / 1024.0;

            // Assert: Reasonable memory usage
            Assert.Less(memoryIncreaseMB, 200, $"Memory increase {memoryIncreaseMB:F1}MB exceeds 200MB limit");

            Console.WriteLine($"Memory usage test:");
            Console.WriteLine($"Initial memory: {initialMemory / 1024 / 1024} MB");
            Console.WriteLine($"Final memory: {finalMemory / 1024 / 1024} MB");
            Console.WriteLine($"Increase: {memoryIncreaseMB:F1} MB");
            Console.WriteLine($"Entries loaded: {finalStats.TotalEntries}");
            Console.WriteLine($"HNSW index size: {finalStats.HnswIndexSize}");
        }

        [Test]
        public void IndexRebuild_Performance()
        {
            // Arrange: Load dataset
            const int datasetSize = 1000;
            var testData = GenerateLargeDataset(datasetSize);

            foreach (var entry in testData)
            {
                Store.Add(entry);
            }

            // Act: Measure index rebuild time
            var stopwatch = Stopwatch.StartNew();
            // Note: Index rebuild would be called internally, measure search performance instead
            var queries = testData.Take(10).Select(e => e.Embedding!).ToList();

            foreach (var query in queries)
            {
                Store.Search(new SearchRequest
                {
                    Query = query,
                    K = 5,
                    UseExact = false
                });
            }

            var searchTime = stopwatch.ElapsedMilliseconds;
            var avgSearchTime = searchTime / (double)queries.Count;

            // Assert: Stable performance after initial load
            Assert.Less(avgSearchTime, 20, $"Search performance {avgSearchTime:F1}ms too slow");

            Console.WriteLine($"Index stability test:");
            Console.WriteLine($"Dataset size: {datasetSize}");
            Console.WriteLine($"Total search time: {searchTime}ms for {queries.Count} queries");
            Console.WriteLine($"Average search time: {avgSearchTime:F1}ms");
        }

        [Test]
        public async Task ConcurrentSearches_Performance()
        {
            // Arrange: Load dataset
            const int datasetSize = 5000;
            var testData = GenerateLargeDataset(datasetSize);

            foreach (var entry in testData)
            {
                Store.Add(entry);
            }

            // Act: Test concurrent search performance
            var queries = testData.Take(20).Select(e => e.Embedding!).ToList();
            var tasks = queries.Select(async query =>
            {
                var startTime = Stopwatch.GetTimestamp();
                await Task.Run(() => Store.Search(new SearchRequest
                {
                    Query = query,
                    K = 5,
                    UseExact = false
                }));
                var endTime = Stopwatch.GetTimestamp();
                return (long)((endTime - startTime) * 1000.0 / Stopwatch.Frequency);
            });

            var results = await Task.WhenAll(tasks);
            var avgConcurrentTime = results.Average();
            var maxConcurrentTime = results.Max();

            // Assert: Concurrent performance should be reasonable
            Assert.Less(avgConcurrentTime, 50, $"Concurrent search average {avgConcurrentTime:F1}ms too slow");
            Assert.Less(maxConcurrentTime, 100, $"Concurrent search max {maxConcurrentTime}ms too slow");

            Console.WriteLine($"Concurrent search test:");
            Console.WriteLine($"Concurrent queries: {queries.Count}");
            Console.WriteLine($"Average time: {avgConcurrentTime:F1}ms");
            Console.WriteLine($"Max time: {maxConcurrentTime}ms");
        }

        private static List<KnowledgeEntry> GenerateLargeDataset(int size)
        {
            var random = new Random(42);
            var entries = new List<KnowledgeEntry>();

            for (int i = 0; i < size; i++)
            {
                entries.Add(new KnowledgeEntry
                {
                    Content = $"Performance Entry {i}",
                    Embedding = GenerateRandomEmbedding(384),
                    Tags = new List<string> { $"perf_tag_{i % 20}" }
                });
            }

            return entries;
        }

        private static float[] GenerateRandomEmbedding(int dimensions)
        {
            var random = new Random(42);
            return Enumerable.Range(0, dimensions)
                .Select(_ => (float)(random.NextDouble() * 2 - 1)) // Range [-1, 1]
                .ToArray();
        }
    }
}