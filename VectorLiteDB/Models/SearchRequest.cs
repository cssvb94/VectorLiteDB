using System;
using System.Collections.Generic;

namespace VectorLiteDB.Models
{
    public class SearchRequest
    {
        public float[] Query { get; set; } = Array.Empty<float>();
        public int K { get; set; } = 10;

        // New: Traverse relations up to this depth (0 = no traversal)
        public int TraversalDepth { get; set; } = 0;

        // Filtering
        public Dictionary<string, object>? Filters { get; set; }

        // Tag filtering - exact match
        public List<string>? Tags { get; set; }

        // Hierarchical tag filtering - prefix match
        public List<string>? TagPrefixes { get; set; }

        // HNSW tuning
        public bool UseExact { get; set; } = false;
        public int? EfSearch { get; set; } = 400;

        // Traversal limits (to prevent explosion)
        public int MaxTraversalResults { get; set; } = 1000;
        public int MaxDepth { get; set; } = 5;
    }
}