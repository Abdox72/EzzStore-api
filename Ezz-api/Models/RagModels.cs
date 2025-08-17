using System.ComponentModel.DataAnnotations;

namespace Ezz_api.Models
{
    public enum RagSourceType
    {
        Product = 0,
        Category = 1,
        Document = 2
    }

    public class RagDocument
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public RagSourceType SourceType { get; set; }
        public string? SourceId { get; set; }
        public string? Url { get; set; }
        public Dictionary<string, string>? Metadata { get; set; }
    }

    public class RagChunk
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string DocumentId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public RagSourceType SourceType { get; set; }
        public string? Url { get; set; }
        public float[] Embedding { get; set; } = Array.Empty<float>();
    }

    public class RagCitation
    {
        public string Title { get; set; } = string.Empty;
        public string? Url { get; set; }
        public string Snippet { get; set; } = string.Empty;
        public double Score { get; set; }
    }
}