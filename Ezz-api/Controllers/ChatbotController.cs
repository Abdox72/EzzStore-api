using Ezz_api.DTOs;
using Ezz_api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ezz_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatbotController : ControllerBase
    {
        private readonly IChatbotService _chatbotService;
        private readonly ILogger<ChatbotController> _logger;

        public ChatbotController(IChatbotService chatbotService, ILogger<ChatbotController> logger)
        {
            _chatbotService = chatbotService;
            _logger = logger;
        }

        /// <summary>
        /// Process a chatbot message and return an AI-generated response
        /// </summary>
        /// <param name="request">The chatbot request containing the user's message</param>
        /// <returns>A chatbot response with the AI-generated reply</returns>
        [HttpPost]
        public async Task<ActionResult<ChatbotResponse>> ProcessMessage([FromBody] ChatbotRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new { error = "Message cannot be empty" });
            }

            try
            {
                var response = await _chatbotService.ProcessMessageAsync(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing chatbot message");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }
}