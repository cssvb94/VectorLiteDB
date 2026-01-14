using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using HNSWIndex;
using Accord.Math.Distances;
using VectorLiteDB.Models;

namespace VectorLiteDB.Services
{
    /// <summary>
    /// Wrapper around HNSWIndex NuGet package for vector similarity search
    /// </summary>
    public class HnswVectorIndex : IDisposable
    {
        private HNSWIndex<float[], float> _index;
        private readonly Dictionary<string, float[]> _vectors = new();
        private readonly Dictionary<float[], string> _reverseLookup = new();
        private readonly HNSWParameters<float> _parameters;
        private readonly object _lock = new();

        public int Count => _vectors.Count;

        public HnswVectorIndex(int dimension, HNSWParameters<float>? config = null)
        {
            _parameters = config ?? new HNSWParameters<float>
            {
                MaxEdges = 32,           // M - neighbors per node
                MaxCandidates = 200,     // efConstruction
                MinNN = 400,             // efSearch (for 99% recall)
                CollectionSize = 100000, // Expected max size
                RandomSeed = 42,
                DistributionRate = 1.0 / Math.Log(32)
            };

            // Custom cosine distance metric: distance = 1 - similarity
            _index = new HNSWIndex<float[], float>(
                (a, b) => 1 - CosineSimilarity(a, b),
                _parameters
            );
        }

        public void Add(float[] vector, string id)
        {
            lock (_lock)
            {
                if (_vectors.TryGetValue(id, out float[]? value))
                {
                    // Update existing
                    var oldVector = value;
                    _vectors[id] = vector;
                    _reverseLookup.Remove(oldVector);
                    _reverseLookup[vector] = id;
                    // Note: HNSWIndex doesn't support updates, would need rebuild
                }
                else
                {
                    // Add new
                    _vectors[id] = vector;
                    _reverseLookup[vector] = id;
                    _index.Add(vector);
                }
            }
        }

        public void AddBatch(IEnumerable<(float[] Vector, string Id)> items)
        {
            lock (_lock)
            {
                foreach (var (vector, id) in items)
                {
                    if (!_vectors.ContainsKey(id))
                    {
                        _vectors[id] = vector;
                        _reverseLookup[vector] = id;
                        _index.Add(vector);
                    }
                }
            }
        }

        public List<(string Id, float Distance)> KnnQuery(float[] query, int k, int efSearch = 400)
        {
            if (_vectors.Count == 0) return new List<(string, float)>();

            lock (_lock)
            {
                var results = _index.KnnQuery(query, k);

                return results
                    .Where(r => _reverseLookup.ContainsKey(r.Label))
                    .Select(r => (_reverseLookup[r.Label], r.Distance))
                    .ToList();
            }
        }

        public bool Contains(string id)
        {
            lock (_lock)
            {
                return _vectors.ContainsKey(id);
            }
        }

        public void Remove(string id)
        {
            lock (_lock)
            {
                if (_vectors.TryGetValue(id, out var vector))
                {
                    _vectors.Remove(id);
                    _reverseLookup.Remove(vector);
                    // Note: HNSWIndex doesn't support removal, would need rebuild
                }
            }
        }

        public void Rebuild()
        {
            lock (_lock)
            {
                // Clear existing index
                var newIndex = new HNSWIndex<float[], float>(
                    (a, b) => 1 - CosineSimilarity(a, b),
                    _parameters
                );

                // Rebuild from scratch
                foreach (var vector in _vectors.Values)
                {
                    newIndex.Add(vector);
                }

                _index = newIndex;
            }
        }

        public void Serialize(string path)
        {
            lock (_lock)
            {
                _index.Serialize(path);
            }
        }

        public static HnswVectorIndex Deserialize(string path, int dimension)
        {
            var index = HNSWIndex<float[], float>.Deserialize(
                (a, b) => 1 - CosineSimilarity(a, b),
                path
            );

            // Note: We can't extract parameters from deserialized index
            // Using default parameters for now
            var wrapper = new HnswVectorIndex(dimension);
            wrapper._index = index;
            // Note: Deserialize doesn't restore _vectors and _reverseLookup mappings
            // These would need to be rebuilt from the database
            return wrapper;
        }

        public void Dispose()
        {
            // HNSWIndex implements IDisposable if needed
            (_index as IDisposable)?.Dispose();
            GC.SuppressFinalize(this);
        }

        // Cosine similarity implementation
        private static float CosineSimilarity(float[] a, float[] b)
        {
            if (a.Length != b.Length) return 0;

            float dotProduct = 0, normA = 0, normB = 0;
            for (int i = 0; i < a.Length; i++)
            {
                dotProduct += a[i] * b[i];
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }

            if (normA == 0 || normB == 0) return 0;
            return dotProduct / (float)(Math.Sqrt(normA) * Math.Sqrt(normB));
        }
    }
}