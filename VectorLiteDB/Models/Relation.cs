namespace VectorLiteDB.Models
{
    public class Relation
    {
        public string TargetId { get; set; } = "";
        public float Weight { get; set; } = 1.0f;  // 0.1 (weak) to 2.0 (strong)
        public string? Type { get; set; }  // "parent_of", "depends_on", "related_to"
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}