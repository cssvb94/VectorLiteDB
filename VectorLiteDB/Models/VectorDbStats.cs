using System;

namespace VectorLiteDB.Models
{
    public class VectorDbStats
    {
        public long TotalEntries { get; set; }
        public int IndexSize { get; set; }
        public long MemoryUsage { get; set; }
        public DateTime LastUpdated { get; set; }
        public TimeSpan Uptime { get; set; }
        public int TotalSearches { get; set; }
        public double AverageSearchTimeMs { get; set; }
        public long DatabaseSizeBytes { get; set; }
        public int ActiveConnections { get; set; }
        public Dictionary<string, int> MetadataCategoryCounts { get; set; } = new Dictionary<string, int>();

        // New: HNSW index metrics
        public long HnswIndexSize { get; set; }
        public DateTime? LastIndexRebuild { get; set; }
        public double AverageRecall { get; set; }

        // New: Tag distribution
        public Dictionary<string, int> TagDistribution { get; set; } = new Dictionary<string, int>();
    }
}