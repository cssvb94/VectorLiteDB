using System;
using System.Collections.Generic;
using System.Linq;
using VectorLiteDB.Models;

namespace VectorLiteDB.Tests.TestData
{
    public class TestDataBuilder
    {
        private readonly List<KnowledgeEntry> _entries = new();

        public TestDataBuilder WithRandomEntries(int count, int dimensions = 384)
        {
            for (int i = 0; i < count; i++)
            {
                _entries.Add(new KnowledgeEntry
                {
                    Id = $"entry_{i}",
                    Content = $"Test content {i}",
                    Embedding = GenerateRandomVector(dimensions),
                    Metadata = new Dictionary<string, object>
                    {
                        ["index"] = i,
                        ["category"] = i % 3 == 0 ? "A" : i % 3 == 1 ? "B" : "C"
                    }
                });
            }
            return this;
        }

        public TestDataBuilder WithClusteredEntries(int clusters, int perCluster, int dimensions = 384)
        {
            var random = new Random(42);
            for (int c = 0; c < clusters; c++)
            {
                var center = GenerateRandomVector(dimensions);
                for (int i = 0; i < perCluster; i++)
                {
                    var vector = AddNoise(center, 0.1, random);
                    _entries.Add(new KnowledgeEntry
                    {
                        Id = $"cluster_{c}_item_{i}",
                        Content = $"Cluster {c} item {i}",
                        Embedding = vector,
                        Metadata = new Dictionary<string, object> { ["cluster"] = c }
                    });
                }
            }
            return this;
        }

        public TestDataBuilder WithEntry(KnowledgeEntry entry)
        {
            _entries.Add(entry);
            return this;
        }

        public List<KnowledgeEntry> Build() => _entries.ToList();

        private static float[] GenerateRandomVector(int dimensions)
        {
            var random = new Random();
            return Enumerable.Range(0, dimensions)
                .Select(_ => (float)random.NextDouble())
                .ToArray();
        }

        private static float[] AddNoise(float[] vector, double noiseLevel, Random random)
        {
            return vector.Select(v => v + (float)(random.NextDouble() * noiseLevel * 2 - noiseLevel)).ToArray();
        }
    }
}