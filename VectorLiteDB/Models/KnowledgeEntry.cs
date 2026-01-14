using System;
using System.Collections.Generic;

namespace VectorLiteDB.Models
{
    public class KnowledgeEntry
    {
        private static readonly Random _random = new Random(42); // Pre-seeded for randomness
        public string Id { get; set; } = GenerateSeededGuid().ToString();

        private static Guid GenerateSeededGuid()
        {
            // Generate GUID with pre-seeded randomness for better distribution
            var bytes = new byte[16];
            _random.NextBytes(bytes);
            return new Guid(bytes);
        }
        public string? Content { get; set; }
        public float[]? Embedding { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

        // New: Hierarchical tags (e.g., "AI/ML/NeuralNetworks")
        public List<string> Tags { get; set; } = new List<string>();

        // New: Weighted relations to other entries
        public List<Relation> Relations { get; set; } = new List<Relation>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // New: Soft delete support
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    }
}