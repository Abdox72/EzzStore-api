using Ezz_api.Models;

namespace Ezz_api.Services
{
    public interface IRagService
    {
        // Build or rebuild the vector index from DB and docs
        Task<int> RebuildIndexAsync(CancellationToken ct = default);
        // Search top K chunks by similarity
        Task<(List<RagChunk> chunks, List<RagCitation> citations)> SearchAsync(string query, int topK = 5, CancellationToken ct = default);
        // Answer with RAG (retrieval + LLM), returns text + citations
        Task<(string answer, List<RagCitation> citations)> AnswerAsync(string query, List<Ezz_api.DTOs.ChatMessage>? history, string? userId, bool stream = false, CancellationToken ct = default);
    }
}