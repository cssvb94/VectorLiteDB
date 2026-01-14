using System;
using System.Collections.Generic;
using System.Linq;

namespace VectorLiteDB.Services
{
    /// <summary>
    /// Simplified HNSW (Hierarchical Navigable Small World) implementation for ANN search
    /// </summary>
    public class SimpleHNSWIndex
    {
        private readonly List<float[]> _vectors = new();
        private readonly Dictionary<int, List<int>> _neighbors = new();
        private readonly Random _random = new Random(42);
        private readonly int _maxNeighbors = 32;
        private readonly object _lock = new();

        public int Count => _vectors.Count;

        public void Add(float[] vector)
        {
            lock (_lock)
            {
                var id = _vectors.Count;
                _vectors.Add(vector);
                _neighbors[id] = new List<int>();

                if (id == 0) return; // First vector has no neighbors

                // Simple greedy search for neighbors
                var entryPoint = FindEntryPoint(vector);
                var candidates = new HashSet<int> { entryPoint };

                // Find nearest neighbors
                for (int i = 0; i < _maxNeighbors; i++)
                {
                    var nearest = FindNearestNeighbor(vector, candidates);
                    if (nearest == -1) break;

                    _neighbors[id].Add(nearest);
                    _neighbors[nearest].Add(id);
                    candidates.Add(nearest);
                }
            }
        }

        public List<(float[] Vector, float Distance)> KnnQuery(float[] query, int k)
        {
            if (_vectors.Count == 0) return new List<(float[], float)>();

            var visited = new HashSet<int>();
            var candidates = new SortedSet<(float Distance, int Id)>(Comparer<(float, int)>.Create((a, b) => a.Item1.CompareTo(b.Item1)));

            // Start from a random entry point
            var entryPoint = _random.Next(_vectors.Count);
            candidates.Add((CosineDistance(query, _vectors[entryPoint]), entryPoint));
            visited.Add(entryPoint);

            // Greedy search
            while (candidates.Count > 0)
            {
                var (dist, id) = candidates.Min;
                candidates.Remove(candidates.Min);

                foreach (var neighborId in _neighbors[id])
                {
                    if (!visited.Contains(neighborId))
                    {
                        visited.Add(neighborId);
                        var neighborDist = CosineDistance(query, _vectors[neighborId]);
                        if (candidates.Count < k || neighborDist < candidates.Max.Item1)
                        {
                            candidates.Add((neighborDist, neighborId));
                            if (candidates.Count > k)
                            {
                                candidates.Remove(candidates.Max);
                            }
                        }
                    }
                }
            }

            return candidates.Take(k)
                .Select(c => (_vectors[c.Item2], c.Item1))
                .ToList();
        }

        private int FindEntryPoint(float[] vector)
        {
            // Simple approach: return last added vector
            return _vectors.Count - 1;
        }

        private int FindNearestNeighbor(float[] vector, HashSet<int> candidates)
        {
            int nearest = -1;
            float minDist = float.MaxValue;

            foreach (var id in candidates)
            {
                var dist = CosineDistance(vector, _vectors[id]);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = id;
                }
            }

            return nearest;
        }

        private static float CosineDistance(float[] a, float[] b)
        {
            // Convert to double for Accord.Math compatibility
            var aDouble = a.Select(x => (double)x).ToArray();
            var bDouble = b.Select(x => (double)x).ToArray();

            var distance = new Accord.Math.Distances.Cosine();
            return (float)distance.Distance(aDouble, bDouble);
        }
    }
}