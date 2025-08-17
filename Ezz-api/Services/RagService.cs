using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ezz_api.Models;
using Microsoft.EntityFrameworkCore;

namespace Ezz_api.Services
{
    // Simple in-memory vector store using cosine similarity
    public class RagService : IRagService
    {
        private readonly ApplicationDbContext _db;
        private readonly HttpClient _http;
        private readonly IConfiguration _config;
        private readonly ILogger<RagService> _logger;

        // Shared in-memory cache across requests
        private static List<RagChunk> _chunks = new();
        private static readonly SemaphoreSlim _buildLock = new(1, 1);

        private string OpenAiApiKey => _config["OpenAI:ApiKey"] ?? string.Empty;
        private string OpenAiEmbeddingModel => _config["OpenAI:EmbeddingModel"] ?? "text-embedding-3-small";
        private string OpenAiChatModel => _config["OpenAI:ChatModel"] ?? "gpt-4o-mini";
        private string GrokApiKey => _config["chatpot_apikey"] ?? string.Empty;

        public RagService(ApplicationDbContext db, HttpClient http, IConfiguration config, ILogger<RagService> logger)
        {
            _db = db;
            _http = http;
            _config = config;
            _logger = logger;
        }

        public async Task<int> RebuildIndexAsync(CancellationToken ct = default)
        {
            await _buildLock.WaitAsync(ct);
            try
            {
                if (_chunks.Count > 0)
                {
                    return _chunks.Count; // already built
                }

                var localChunks = new List<RagChunk>();

                // 1) Ingest products
                var products = await _db.Products.Include(p => p.Category).ToListAsync(ct);
                foreach (var p in products)
                {
                    var doc = new RagDocument
                    {
                        Title = p.Title,
                        Content = $"{p.Title}\nالفئة: {p.Category?.Name}\nالسعر: {p.Price}\nالوصف: {p.Description}\nالمخزن: {p.Stock}",
                        SourceType = RagSourceType.Product,
                        SourceId = p.Id.ToString(),
                        Url = $"/product/{p.Id}",
                    };
                    foreach (var chunk in SplitIntoChunks(doc))
                        localChunks.Add(chunk);
                }

                // 2) Ingest repo docs (if exist)
                var docs = new[]
                {
                    (Title: "SETUP_INSTRUCTIONS", Path: @"f:\\CS\\Projects\\Ezz\\Ezz-api\\SETUP_INSTRUCTIONS.md"),
                    (Title: "STRIPE_INTEGRATION_GUIDE", Path: @"f:\\CS\\Projects\\Ezz\\Ezz-api\\STRIPE_INTEGRATION_GUIDE.md"),
                    (Title: "products_with_occasions", Path: @"f:\\CS\\Projects\\Ezz\\Ezz-api\\products_with_occasions.txt"),
                    (Title: "RETURNS_PAYMENTS_POLICY", Path: @"f:\\CS\\Projects\\Ezz\\Ezz-api\\RETURNS_PAYMENTS_POLICY.txt"),
                };
                foreach (var d in docs)
                {
                    try
                    {
                        if (File.Exists(d.Path))
                        {
                            var content = await File.ReadAllTextAsync(d.Path, ct);
                            var doc = new RagDocument
                            {
                                Title = d.Title,
                                Content = content,
                                SourceType = RagSourceType.Document,
                                Url = $"/docs/{Path.GetFileName(d.Path)}"
                            };
                            foreach (var chunk in SplitIntoChunks(doc, 1200))
                                localChunks.Add(chunk);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to ingest doc: {Path}", d.Path);
                    }
                }

                // 3) Embed all chunks
                foreach (var batch in Batch(localChunks, 64))
                {
                    var texts = batch.Select(c => c.Content).ToList();
                    var embeddings = await EmbedAsync(texts, ct);
                    for (int i = 0; i < batch.Count; i++)
                        batch[i].Embedding = embeddings[i];
                    await Task.Delay(50, ct);
                }

                _chunks = localChunks; // publish built index atomically
                return _chunks.Count;
            }
            finally
            {
                _buildLock.Release();
            }
        }

        public async Task<(List<RagChunk> chunks, List<RagCitation> citations)> SearchAsync(string query, int topK = 5, CancellationToken ct = default)
        {
            var qEmb = (await EmbedAsync(new List<string> { query }, ct)).First();
            const double minScore = 0.20; // filter weak matches
            var scored = _chunks
                .Select(c => new { c, score = CosineSim(qEmb, c.Embedding) })
                .OrderByDescending(x => x.score)
                .Where(x => x.score >= minScore)
                .Take(topK)
                .ToList();

            var top = scored.Select(x => x.c).ToList();
            var cits = scored.Select(x => new RagCitation
            {
                Title = x.c.Title,
                Url = x.c.Url,
                Snippet = Truncate(x.c.Content, 240),
                Score = x.score
            }).ToList();
            return (top, cits);
        }

        public async Task<(string answer, List<RagCitation> citations)> AnswerAsync(string query, List<Ezz_api.DTOs.ChatMessage>? history, string? userId, bool stream = false, CancellationToken ct = default)
        {
            // Ensure index is built at least once
            if (_chunks.Count == 0)
                await RebuildIndexAsync(ct);

            var (topChunks, citations) = await SearchAsync(query, 7, ct);
            var context = string.Join("\n---\n", topChunks.Select(c => c.Content));

            // Build messages with strict grounding
            var messages = new List<object>
            {
                new { role = "system", content = "أنت مساعد متجر ذكي. أجب اعتماداً على (السياق المزوَّد) فقط. إذا لم تجد الإجابة في السياق أو كانت غير كافية، قل بوضوح: لا توجد معلومات كافية في البيانات. التزم بالدقة واذكر المراجع المستخدمة في النهاية كنقاط." },
                new { role = "user", content = $"السؤال: {query}\n\nالسياق:\n{context}\n\nالتعليمات: أجب فقط بالاعتماد على السياق أعلاه. إذا لم يتوفر الجواب، قل: لا توجد معلومات كافية في البيانات. ثم اذكر المراجع بنهاية الرد." }
            };

            // Append brief history
            if (history != null && history.Count > 0)
            {
                foreach (var h in history.TakeLast(4))
                {
                    messages.Add(new { role = h.IsUser ? "user" : "assistant", content = h.Content });
                }
            }

            // Use Ollama local chat (/api/chat)
            var ollamaChatPayload = new {
                model = "llama3.2",
                messages = messages,
                stream = false,
                options = new { temperature = 0.3 }
            };
            using var content = new StringContent(JsonSerializer.Serialize(ollamaChatPayload), Encoding.UTF8, "application/json");
            using var reqMsg = new HttpRequestMessage(HttpMethod.Post, "http://localhost:11434/api/chat");
            reqMsg.Content = content;

            var resp = await _http.SendAsync(reqMsg, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Ollama chat failed: {Status}. Body: {Body}", resp.StatusCode, body);
                return ("عذراً، تعذر توليد الإجابة الآن.", citations);
            }

            // Ollama sometimes streams or returns newline-delimited JSON; ensure we pick the last full JSON object
            var trimmed = body.Trim();
            if (trimmed.Contains("}\n{"))
            {
                // take last JSON object if multiple
                var lastIdx = trimmed.LastIndexOf("{", StringComparison.Ordinal);
                trimmed = trimmed.Substring(lastIdx);
            }
            var parsed = JsonSerializer.Deserialize<OllamaChatResponse>(trimmed);
            var answer = parsed?.message?.content ?? parsed?.choices?.FirstOrDefault()?.message?.content ?? "";
            // Append citations block
            if (citations.Any())
            {
                var citesBlock = new StringBuilder();
                citesBlock.AppendLine("\n\nالمراجع:");
                foreach (var c in citations)
                {
                    var line = $"- {c.Title}{(string.IsNullOrEmpty(c.Url) ? "" : $" ({c.Url})")}";
                    citesBlock.AppendLine(line);
                }
                answer += citesBlock.ToString();
            }
            return (answer, citations);
        }

        // Helpers
        private IEnumerable<List<T>> Batch<T>(IEnumerable<T> src, int size)
        {
            var bucket = new List<T>(size);
            foreach (var item in src)
            {
                bucket.Add(item);
                if (bucket.Count == size)
                {
                    yield return bucket;
                    bucket = new List<T>(size);
                }
            }
            if (bucket.Count > 0) yield return bucket;
        }

        private IEnumerable<RagChunk> SplitIntoChunks(RagDocument doc, int maxLen = 800)
        {
            // Simple splitter by paragraphs
            var paragraphs = (doc.Content ?? string.Empty)
                .Replace("\r", "")
                .Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
            var current = new StringBuilder();

            foreach (var p in paragraphs)
            {
                var add = p.Trim();
                if (current.Length + add.Length + 2 > maxLen && current.Length > 0)
                {
                    yield return MakeChunk(doc, current.ToString());
                    current.Clear();
                }
                if (add.Length > maxLen)
                {
                    // hard split long paragraph
                    for (int i = 0; i < add.Length; i += maxLen)
                    {
                        var piece = add.Substring(i, Math.Min(maxLen, add.Length - i));
                        yield return MakeChunk(doc, piece);
                    }
                }
                else
                {
                    if (current.Length > 0) current.AppendLine();
                    current.Append(add);
                }
            }
            if (current.Length > 0)
                yield return MakeChunk(doc, current.ToString());
        }

        private RagChunk MakeChunk(RagDocument doc, string content)
        {
            return new RagChunk
            {
                DocumentId = doc.Id,
                Title = doc.Title,
                Content = content,
                SourceType = doc.SourceType,
                Url = doc.Url,
            };
        }

        private async Task<List<float[]>> EmbedAsync(List<string> texts, CancellationToken ct)
        {
            // Use Ollama local embeddings to avoid external quotas
            var results = new List<float[]>(texts.Count);
            foreach (var t in texts)
            {
                var payload = new { model = "nomic-embed-text", prompt = t };
                using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                using var reqMsg = new HttpRequestMessage(HttpMethod.Post, "http://localhost:11434/api/embeddings");
                reqMsg.Content = content;

                var resp = await _http.SendAsync(reqMsg, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Ollama embeddings failed: {Status}", resp.StatusCode);
                    results.Add(new float[768]); // fallback vector size for nomic-embed-text (768)
                    continue;
                }
                var body = await resp.Content.ReadAsStringAsync(ct);
                var parsed = JsonSerializer.Deserialize<OllamaEmbeddingResponse>(body);
                var emb = parsed?.embedding?.Select(v => (float)v).ToArray() ?? new float[768];
                results.Add(emb);
                // light delay to avoid overwhelming local server
                await Task.Delay(50, ct);
            }
            return results;
        }

        private static double CosineSim(float[] a, float[] b)
        {
            if (a.Length == 0 || b.Length == 0 || a.Length != b.Length) return 0;
            double dot = 0, na = 0, nb = 0;
            for (int i = 0; i < a.Length; i++)
            {
                dot += a[i] * b[i];
                na += a[i] * a[i];
                nb += b[i] * b[i];
            }
            if (na == 0 || nb == 0) return 0;
            return dot / (Math.Sqrt(na) * Math.Sqrt(nb));
        }

        private static string Truncate(string s, int len)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length <= len ? s : s.Substring(0, len) + "...";
        }

        private async Task<HttpResponseMessage> SendWithRetryAsync(HttpRequestMessage req, CancellationToken ct)
        {
            // Simple exponential backoff for 429/5xx
            var delays = new[] { 0, 500, 1000, 2000, 4000 };
            HttpResponseMessage? last = null;
            for (int attempt = 0; attempt < delays.Length; attempt++)
            {
                if (delays[attempt] > 0)
                    await Task.Delay(delays[attempt], ct);

                var resp = await _http.SendAsync(req.Clone(), ct);
                last = resp;
                if ((int)resp.StatusCode == 429 || (int)resp.StatusCode >= 500)
                {
                    // capture body for diagnostics before disposing
                    string body = string.Empty;
                    try { body = await resp.Content.ReadAsStringAsync(ct); } catch { }
                    _logger.LogWarning("Upstream retryable error: {Status}. Body: {Body}", resp.StatusCode, body);
                    if (attempt == delays.Length - 1) return resp;
                    continue;
                }
                return resp;
            }
            return last ?? new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest) { ReasonPhrase = "No response" };
        }

        // DTOs for OpenAI
        private class OpenAiEmbeddingResponse
        {
            public List<EmbData>? data { get; set; }
        }
        private class EmbData
        {
            public List<double>? embedding { get; set; }
        }

        // DTOs for Ollama embeddings
        private class OllamaEmbeddingResponse
        {
            public List<double>? embedding { get; set; }
        }

        // DTOs for Ollama chat
        private class OllamaChatResponse
        {
            public OllamaMessage? message { get; set; }
            public List<OllamaChoice>? choices { get; set; }
        }
        private class OllamaChoice
        {
            public OllamaMessage? message { get; set; }
        }
        private class OllamaMessage
        {
            public string? role { get; set; }
            public string? content { get; set; }
        }
    }

    // Helper to clone HttpRequestMessage (since it can't be sent twice)
    internal static class HttpRequestMessageExtensions
    {
        public static HttpRequestMessage Clone(this HttpRequestMessage req)
        {
            var clone = new HttpRequestMessage(req.Method, req.RequestUri);
            // Copy content
            if (req.Content != null)
            {
                var ms = new MemoryStream();
                req.Content.CopyToAsync(ms).GetAwaiter().GetResult();
                ms.Position = 0;
                clone.Content = new StreamContent(ms);
                // Copy headers of original content
                foreach (var h in req.Content.Headers)
                    clone.Content.Headers.TryAddWithoutValidation(h.Key, h.Value);
            }
            // Copy headers
            foreach (var h in req.Headers)
                clone.Headers.TryAddWithoutValidation(h.Key, h.Value);
            clone.Version = req.Version;
            return clone;
        }
    }
}