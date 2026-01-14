namespace VectorLiteDB.Models
{
    public class SearchResult
    {
        public KnowledgeEntry? Entry { get; set; }
        public float Similarity { get; set; }

        // New: Traversal depth (0=direct, 1+=from relations)
        public int TraversalDepth { get; set; } = 0;

        // New: Entry ID that led to this result (for traversal)
        public string? SourceEntryId { get; set; }

        // New: Full path of relations that led to this result
        public List<string> RelationPath { get; set; } = new List<string>();
    }
}