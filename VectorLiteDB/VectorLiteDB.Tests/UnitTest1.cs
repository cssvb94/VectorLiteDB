using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using VectorLiteDB.Services;
using VectorLiteDB.Models;

namespace VectorLiteDB.Tests
{
    [TestFixture]
    public class VectorDbStoreTests
    {
        private VectorDbStore _store = null!;
        private const string TestDb = "test_vectorlitedb.db";

        [SetUp]
        public void Setup()
        {
            if (System.IO.File.Exists(TestDb))
                System.IO.File.Delete(TestDb);
            _store = new VectorDbStore(TestDb);
        }

        [TearDown]
        public void TearDown()
        {
            _store.Dispose();
        }

        [Test]
        public void ShouldInstantiateWithoutErrors()
        {
            Assert.IsNotNull(_store);
        }

        [Test]
        public void ShouldAddAndRetrieveEntry()
        {
            var entry = new KnowledgeEntry
            {
                Content = "Test content",
                Embedding = GenerateRandomEmbedding(384),
                Metadata = new Dictionary<string, object> { ["key"] = "value" }
            };

            _store.Add(entry);

            var results = _store.Search(entry.Embedding, 1);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(entry.Content!, results[0].Entry!.Content!);
        }

        [Test]
        public void Search_ReturnsCorrectResults()
        {
            // Generate 10 test vectors
            var vectors = GenerateTestVectors(10);
            foreach (var v in vectors)
                _store.Add(new KnowledgeEntry { Embedding = v });

            var query = vectors[0];
            var results = _store.Search(query, 5);

            Assert.AreEqual(5, results.Count);
            Assert.AreEqual(1.0f, results[0].Similarity); // Exact match should be first
        }

        [Test]
        public void HybridSearch_WithMetadataFilters()
        {
            // Add entries with different metadata
            _store.Add(new KnowledgeEntry
            {
                Content = "AI content",
                Embedding = GenerateRandomEmbedding(384),
                Metadata = new Dictionary<string, object> { ["category"] = "AI" }
            });

            _store.Add(new KnowledgeEntry
            {
                Content = "ML content",
                Embedding = GenerateRandomEmbedding(384),
                Metadata = new Dictionary<string, object> { ["category"] = "ML" }
            });

            var query = GenerateRandomEmbedding(384);
            var filters = new Dictionary<string, object> { ["category"] = "AI" };
            var results = _store.Search(query, 10, filters);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("AI content", results[0].Entry!.Content!);
        }

        [Test]
        public void BatchAdd_IncreasesEntryCount()
        {
            var initialCount = _store.GetStats().TotalEntries;

            var batchEntries = new List<KnowledgeEntry>
            {
                new KnowledgeEntry { Content = "Batch 1", Embedding = GenerateRandomEmbedding(384) },
                new KnowledgeEntry { Content = "Batch 2", Embedding = GenerateRandomEmbedding(384) }
            };

            _store.AddBatch(batchEntries);

            var finalCount = _store.GetStats().TotalEntries;
            Assert.AreEqual(initialCount + 2, finalCount);
        }

        [Test]
        public void GetStats_IncludesComprehensiveMetrics()
        {
            // Add some entries with categories
            _store.Add(new KnowledgeEntry
            {
                Content = "AI content",
                Embedding = GenerateRandomEmbedding(384),
                Metadata = new Dictionary<string, object> { ["category"] = "AI" }
            });

            _store.Add(new KnowledgeEntry
            {
                Content = "ML content",
                Embedding = GenerateRandomEmbedding(384),
                Metadata = new Dictionary<string, object> { ["category"] = "ML" }
            });

            // Perform a search to generate metrics
            _store.Search(GenerateRandomEmbedding(384), 1);

            var stats = _store.GetStats();

            Assert.Greater(stats.TotalEntries, 0);
            Assert.Greater(stats.Uptime, TimeSpan.Zero);
            Assert.GreaterOrEqual(stats.TotalSearches, 1);
            Assert.GreaterOrEqual(stats.AverageSearchTimeMs, 0);
            Assert.GreaterOrEqual(stats.DatabaseSizeBytes, 0);
            Assert.Contains("AI", stats.MetadataCategoryCounts.Keys);
            Assert.Contains("ML", stats.MetadataCategoryCounts.Keys);
        }

        [Test]
        public void ShardedStore_DistributesEntries()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "VectorLiteDBTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            var basePath = Path.Combine(tempDir, "shard");

            try
            {
                using (var shardedStore = new ShardedVectorDbStore(2, basePath))
                {
                    // Add entries
                    for (int i = 0; i < 10; i++)
                    {
                        shardedStore.Add(new KnowledgeEntry
                        {
                            Content = $"Entry {i}",
                            Embedding = GenerateRandomEmbedding(384)
                        });
                    }

                    var stats = shardedStore.GetStats();
                    Assert.AreEqual(10, stats.TotalEntries);
                    Assert.AreEqual(2, stats.ActiveConnections); // 2 shards
                }
            }
            finally
            {
                // Cleanup
                Directory.Delete(tempDir, true);
            }
        }

        [Test]
        public void ShardedStore_HybridSearchWorks()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "VectorLiteDBTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            var basePath = Path.Combine(tempDir, "shard");

            try
            {
                using (var shardedStore = new ShardedVectorDbStore(2, basePath))
                {
                    shardedStore.Add(new KnowledgeEntry
                    {
                        Content = "AI content",
                        Embedding = GenerateRandomEmbedding(384),
                        Metadata = new Dictionary<string, object> { ["category"] = "AI" }
                    });

                    var query = GenerateRandomEmbedding(384);
                    var filters = new Dictionary<string, object> { ["category"] = "AI" };
                    var results = shardedStore.Search(query, 5, filters);

                    Assert.GreaterOrEqual(results.Count, 0); // May find matches
                    foreach (var result in results)
                    {
                        Assert.AreEqual("AI", result.Entry!.Metadata["category"]);
                    }
                }
            }
            finally
            {
                // Cleanup
                Directory.Delete(tempDir, true);
            }
        }

        [Test]
        public void Add_HandlesNullEmbedding()
        {
            var entry = new KnowledgeEntry { Content = "test", Embedding = null };
            _store.Add(entry);
            var results = _store.Search(new float[384], 1);
            Assert.AreEqual(0, results.Count); // Null embeddings are ignored in search
        }

        [Test]
        public void Search_ReturnsEmptyForNoMatches()
        {
            var results = _store.Search(new float[384], 1);
            Assert.IsEmpty(results);
        }

        [Test]
        public void GetStats_ReturnsCorrectCounts()
        {
            // Add 5 entries
            for (int i = 0; i < 5; i++)
            {
                _store.Add(new KnowledgeEntry
                {
                    Content = $"Entry {i}",
                    Embedding = GenerateRandomEmbedding(384)
                });
            }

            var stats = _store.GetStats();
            Assert.AreEqual(5, stats.TotalEntries);
            Assert.Greater(stats.MemoryUsage, 0);
        }

        [Test]
        public void Add_HandlesLargeEmbeddings()
        {
            var largeEmbedding = GenerateRandomEmbedding(1536); // Production size
            var entry = new KnowledgeEntry
            {
                Content = "Large embedding test",
                Embedding = largeEmbedding
            };

            _store.Add(entry);
            var results = _store.Search(largeEmbedding, 1);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(1.0f, results[0].Similarity);
        }

        // Helper methods
        private static float[] GenerateRandomEmbedding(int dimensions)
        {
            var random = new Random(42);
            return Enumerable.Range(0, dimensions)
                .Select(_ => (float)random.NextDouble())
                .ToArray();
        }

        private static List<float[]> GenerateTestVectors(int count)
        {
            var random = new Random(42);
            return Enumerable.Range(0, count)
                .Select(_ => GenerateRandomEmbedding(384))
                .ToList();
        }

        private static List<SearchResult> BruteForceSearch(float[] query, List<float[]> vectors, int k)
        {
            return vectors
                .Select(v => new SearchResult
                {
                    Entry = new KnowledgeEntry { Embedding = v },
                    Similarity = CosineSimilarity(query, v)
                })
                .OrderByDescending(r => r.Similarity)
                .Take(k)
                .ToList();
        }

        private static float CosineSimilarity(float[] a, float[] b)
        {
            var dotProduct = a.Zip(b, (x, y) => x * y).Sum();
            var magnitudeA = Math.Sqrt(a.Sum(x => x * x));
            var magnitudeB = Math.Sqrt(b.Sum(x => x * x));
            return (float)(dotProduct / (magnitudeA * magnitudeB));
        }

        private static double CalculateRecall(List<SearchResult> annResults, List<SearchResult> bruteResults)
        {
            var annIds = new HashSet<string>(annResults.Select(r => r.Entry!.Id!));
            var bruteIds = new HashSet<string>(bruteResults.Select(r => r.Entry!.Id!));
            return (double)annIds.Intersect(bruteIds).Count() / bruteIds.Count;
        }
    }
}
