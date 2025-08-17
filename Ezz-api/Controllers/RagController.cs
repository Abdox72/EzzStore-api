using Ezz_api.DTOs;
using Ezz_api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ezz_api.Controllers
{
    [ApiController]
    [Route("api/rag")]
    public class RagController : ControllerBase
    {
        private readonly IRagService _rag;
        private readonly IChatbotService _chatbot; // still used for product/category suggestions if needed
        private readonly ILogger<RagController> _logger;

        public RagController(IRagService rag, IChatbotService chatbot, ILogger<RagController> logger)
        {
            _rag = rag;
            _chatbot = chatbot;
            _logger = logger;
        }

        [HttpPost("reindex")]
        public async Task<IActionResult> Reindex()
        {
            var count = await _rag.RebuildIndexAsync();
            return Ok(new { indexed = count });
        }

        [HttpGet("status")]
        public IActionResult Status()
        {
            // Simple status endpoint for frontend warmup
            return Ok(new { status = "ok" });
        }

        [HttpPost("chat")] // public endpoint
        public async Task<IActionResult> Chat([FromBody] ChatbotRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
                return BadRequest(new { error = "Message cannot be empty" });

            var (answer, citations) = await _rag.AnswerAsync(request.Message, request.ConversationHistory, request.UserId);

            // Enrich with related products/categories via existing service (optional)
            var relatedProducts = await _chatbot.GetSuggestedProductsAsync(request.Message, 5);
            var categories = await _chatbot.GetRelevantCategoriesAsync(request.Message, 3);

            return Ok(new ChatbotResponse
            {
                Reply = answer,
                RelatedProducts = relatedProducts,
                SuggestedCategories = categories,
                IsSystemMessage = false,
                AdditionalContext = new Dictionary<string, string> { { "citations", System.Text.Json.JsonSerializer.Serialize(citations) } }
            });
        }
    }
}