using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using VectorLiteDB.Services;
using VectorLiteDB.Models;

namespace VectorLiteDB.Tests
{
    [TestFixture]
    public class RecallPrecisionTests : BaseTest
    {
        [Test]
        [TestCase(100)]
        [TestCase(1000)]
        [TestCase(10000)]
        public void HNSW_Recall_GreaterThan99Percent(int datasetSize)
        {
            // Arrange: Create test dataset
            var testData = GenerateTestDataset(datasetSize);
            foreach (var entry in testData)
            {
                Store.Add(entry);
            }

            // Act: Test recall for multiple queries
            const int numQueries = 10;
            var recalls = new List<double>();

            for (int i = 0; i < numQueries; i++)
            {
                // Pick a random query from the dataset
                var queryEntry = testData[i % testData.Count];
                var query = queryEntry.Embedding!;

                // Search with HNSW (exact=false to use ANN)
                var hnswResults = Store.Search(new SearchRequest
                {
                    Query = query,
                    K = 10,
                    UseExact = false,
                    EfSearch = 400
                });

                // Get brute force ground truth
                var bruteForceResults = Store.Search(new SearchRequest
                {
                    Query = query,
                    K = 10,
                    UseExact = true // Force exact search
                });

                // Calculate recall
                var hnswIds = new HashSet<string>(hnswResults.Select(r => r.Entry!.Id));
                var bruteIds = new HashSet<string>(bruteForceResults.Select(r => r.Entry!.Id));

                // Recall = |relevant ∩ retrieved| / |relevant|
                // For k-NN, relevant = brute force results
                var recall = (double)hnswIds.Intersect(bruteIds).Count() / bruteIds.Count;
                recalls.Add(recall);

                // Each individual query should have high recall
                Assert.GreaterOrEqual(recall, 0.95, $"Query {i} recall {recall:F3} below 95% threshold");
            }

            // Overall average recall should be >= 99%
            var averageRecall = recalls.Average();
            Assert.GreaterOrEqual(averageRecall, 0.99,
                $"Average recall {averageRecall:F3} below 99% target. Individual recalls: {string.Join(", ", recalls.Select(r => r.ToString("F3")))}");

            Console.WriteLine($"Dataset size: {datasetSize}, Average recall: {averageRecall:F3}, Min recall: {recalls.Min():F3}");
        }

        [Test]
        public void HNSW_Vs_BruteForce_AccuracyComparison()
        {
            // Arrange: Small controlled dataset for accuracy comparison
            var testData = GenerateControlledDataset(100);
            foreach (var entry in testData)
            {
                Store.Add(entry);
            }

            // Act: Compare HNSW vs brute force on same queries
            var queries = testData.Take(5).Select(e => e.Embedding!).ToList();
            var results = new List<(List<SearchResult> Hnsw, List<SearchResult> Brute, double Similarity)>();

            foreach (var query in queries)
            {
                var hnswResults = Store.Search(new SearchRequest
                {
                    Query = query,
                    K = 10,
                    UseExact = false,
                    EfSearch = 400
                });

                var bruteResults = Store.Search(new SearchRequest
                {
                    Query = query,
                    K = 10,
                    UseExact = true
                });

                // Calculate average similarity between results
                var hnswSimilarities = hnswResults.Select(r => r.Similarity).ToList();
                var bruteSimilarities = bruteResults.Select(r => r.Similarity).ToList();

                var querySimilarity = hnswSimilarities.Zip(bruteSimilarities, (h, b) => Math.Min(h, b)).Average();
                results.Add((hnswResults, bruteResults, querySimilarity));
            }

            // Assert: HNSW results should be very close to brute force
            var overallSimilarity = results.Average(r => r.Item3);
            Assert.GreaterOrEqual(overallSimilarity, 0.95, $"Average similarity {overallSimilarity:F3} too low");

            Console.WriteLine($"HNSW vs Brute Force comparison - Average similarity: {overallSimilarity:F3}");
        }

        [Test]
        public void TagPrefix_HierarchicalMatching()
        {
            // Arrange: Create entries with hierarchical tags
            var entries = new[]
            {
                new KnowledgeEntry { Content = "ML Basics", Tags = new List<string> { "AI/ML" }, Embedding = GenerateRandomEmbedding(384) },
                new KnowledgeEntry { Content = "Neural Networks", Tags = new List<string> { "AI/ML/NeuralNetworks" }, Embedding = GenerateRandomEmbedding(384) },
                new KnowledgeEntry { Content = "Deep Learning", Tags = new List<string> { "AI/ML/DeepLearning" }, Embedding = GenerateRandomEmbedding(384) },
                new KnowledgeEntry { Content = "Python Basics", Tags = new List<string> { "Programming/Python" }, Embedding = GenerateRandomEmbedding(384) }
            };

            foreach (var entry in entries)
            {
                Store.Add(entry);
            }

            // Act: Search with tag prefix
            var results = Store.Search(new SearchRequest
            {
                Query = entries[0].Embedding, // Query doesn't matter for tag filtering
                K = 10,
                TagPrefixes = new List<string> { "AI/ML" }
            });

            // Assert: Should find AI/ML and its hierarchical children
            var foundTags = results.SelectMany(r => r.Entry!.Tags).Distinct().ToList();
            Assert.Contains("AI/ML", foundTags);
            Assert.Contains("AI/ML/NeuralNetworks", foundTags);
            Assert.Contains("AI/ML/DeepLearning", foundTags);
            Assert.IsFalse(foundTags.Contains("Programming/Python"));

            Console.WriteLine($"Tag prefix 'AI/ML' found {results.Count} entries with tags: {string.Join(", ", foundTags)}");
        }

        [Test]
        public void RelationTraversal_DepthControl()
        {
            // Arrange: Create two related entries
            // Use completely different embeddings to ensure they're not identical
            var entry1 = new KnowledgeEntry
            {
                Content = "Entry 1",
                Embedding = new float[384], // All zeros
                Relations = new List<Relation>() // Will be set after entry2 is added
            };
            // Set first 192 values to 1.0f
            for (int i = 0; i < 192; i++) entry1.Embedding[i] = 1.0f;

            var entry2 = new KnowledgeEntry
            {
                Content = "Entry 2",
                Embedding = new float[384], // All zeros
            };
            // Set last 192 values to 1.0f (completely different from entry1)
            for (int i = 192; i < 384; i++) entry2.Embedding[i] = 1.0f;

            Store.Add(entry1);
            Store.Add(entry2);

            // Create relation: entry1 -> entry2 (should become bidirectional)
            var entryWithRelation = new KnowledgeEntry
            {
                Id = entry1.Id, // Same ID to update
                Content = "Entry 1",
                Embedding = entry1.Embedding,
                Relations = new List<Relation> { new Relation { TargetId = entry2.Id, Weight = 1.0f, Type = "related_to" } }
            };
            Store.Add(entryWithRelation); // This should update and make bidirectional

            // Act: Test traversal - search with entry2's embedding should find entry1 via relation
            var results = Store.Search(new SearchRequest
            {
                Query = entry2.Embedding,
                K = 10,
                TraversalDepth = 1,
                UseExact = true
            });

            // Debug: Show all results
            Console.WriteLine($"Found {results.Count} total results:");
            foreach (var result in results)
            {
                Console.WriteLine($"  Depth {result.TraversalDepth}: {result.Entry!.Content} (ID: {result.Entry.Id})");
            }

            // Assert: Should find both entries (entry2 direct + entry1 via relation)
            Assert.GreaterOrEqual(results.Count, 2, "Should find at least 2 entries with traversal");
            var traversalResults = results.Where(r => r.TraversalDepth > 0).ToList();
            Assert.GreaterOrEqual(traversalResults.Count, 1, "Should find at least 1 entry via traversal");

            Console.WriteLine($"Traversal results: {traversalResults.Count}");
        }

        private static List<KnowledgeEntry> GenerateTestDataset(int size)
        {
            var random = new Random(42);
            var entries = new List<KnowledgeEntry>();

            for (int i = 0; i < size; i++)
            {
                entries.Add(new KnowledgeEntry
                {
                    Content = $"Entry {i}",
                    Embedding = GenerateRandomEmbedding(384),
                    Tags = new List<string> { $"tag_{i % 10}" }
                });
            }

            return entries;
        }

        private static List<KnowledgeEntry> GenerateControlledDataset(int size)
        {
            // Generate embeddings that are more spread out for better testing
            var random = new Random(42);
            var entries = new List<KnowledgeEntry>();

            for (int i = 0; i < size; i++)
            {
                // Create more distinct embeddings
                var embedding = new float[384];
                for (int j = 0; j < 384; j++)
                {
                    embedding[j] = (float)(random.NextDouble() * 2 - 1); // Range [-1, 1]
                }

                entries.Add(new KnowledgeEntry
                {
                    Content = $"Controlled Entry {i}",
                    Embedding = embedding,
                    Tags = new List<string> { $"controlled_tag_{i % 5}" }
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

        private static float[] GenerateUniqueEmbedding(int seed, int dimensions)
        {
            var random = new Random(seed);
            return Enumerable.Range(0, dimensions)
                .Select(_ => (float)(random.NextDouble() * 2 - 1)) // Range [-1, 1]
                .ToArray();
        }
    }
}