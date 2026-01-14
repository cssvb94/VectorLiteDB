using System;
using System.Collections.Generic;
using System.Linq;
using LiteDB;
using VectorLiteDB.Models;

namespace VectorLiteDB.Services
{
    public class ShardedVectorDbStore : IDisposable
    {
        private readonly List<VectorDbStore> _shards;
        private readonly int _shardCount;

        public ShardedVectorDbStore(int shardCount = 4, string basePath = "vectorlitedb_shard")
        {
            _shardCount = shardCount;
            _shards = new List<VectorDbStore>();

            for (int i = 0; i < shardCount; i++)
            {
                _shards.Add(new VectorDbStore($"{basePath}_{i}.db"));
            }
        }

        private int GetShardIndex(string id)
        {
            return Math.Abs(id.GetHashCode()) % _shardCount;
        }

        public void Add(KnowledgeEntry entry)
        {
            if (string.IsNullOrEmpty(entry.Id))
                entry.Id = ObjectId.NewObjectId().ToString();

            var shardIndex = GetShardIndex(entry.Id);
            _shards[shardIndex].Add(entry);
        }

        public List<SearchResult> Search(float[] query, int k = 10, Dictionary<string, object>? filters = null)
        {
            // Search all shards and combine results
            var allResults = new List<SearchResult>();

            foreach (var shard in _shards)
            {
                var shardResults = shard.Search(query, k, filters);
                allResults.AddRange(shardResults);
            }

            // Re-rank across all shards
            return allResults
                .OrderByDescending(r => r.Similarity)
                .Take(k)
                .ToList();
        }

        public VectorDbStats GetStats()
        {
            // Aggregate stats from all shards
            var totalEntries = 0L;
            var totalIndexSize = 0;
            var totalMemory = 0L;
            var totalSearches = 0;
            var totalSearchTime = 0.0;
            var maxUptime = TimeSpan.Zero;
            var totalDbSize = 0L;
            var categoryCounts = new Dictionary<string, int>();

            foreach (var shard in _shards)
            {
                var stats = shard.GetStats();
                totalEntries += stats.TotalEntries;
                totalIndexSize += stats.IndexSize;
                totalMemory += stats.MemoryUsage;
                totalSearches += stats.TotalSearches;
                totalSearchTime += stats.AverageSearchTimeMs * stats.TotalSearches;
                if (stats.Uptime > maxUptime) maxUptime = stats.Uptime;
                totalDbSize += stats.DatabaseSizeBytes;

                foreach (var kvp in stats.MetadataCategoryCounts)
                {
                    if (categoryCounts.ContainsKey(kvp.Key))
                        categoryCounts[kvp.Key] += kvp.Value;
                    else
                        categoryCounts[kvp.Key] = kvp.Value;
                }
            }

            var avgSearchTime = totalSearches > 0 ? totalSearchTime / totalSearches : 0;

            return new VectorDbStats
            {
                TotalEntries = totalEntries,
                IndexSize = totalIndexSize,
                MemoryUsage = totalMemory,
                LastUpdated = DateTime.UtcNow,
                Uptime = maxUptime,
                TotalSearches = totalSearches,
                AverageSearchTimeMs = avgSearchTime,
                DatabaseSizeBytes = totalDbSize,
                ActiveConnections = _shardCount,
                MetadataCategoryCounts = categoryCounts
            };
        }

        public void Dispose()
        {
            foreach (var shard in _shards)
            {
                shard.Dispose();
            }
            GC.SuppressFinalize(this);
        }
    }
}