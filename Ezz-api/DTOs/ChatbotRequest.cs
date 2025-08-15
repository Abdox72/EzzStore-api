using System.Collections.Generic;

namespace Ezz_api.DTOs
{
    public class ChatbotRequest
    {
        public string Message { get; set; } = string.Empty;
        
        // Optional conversation history for context
        public List<ChatMessage>? ConversationHistory { get; set; }
        
        // Optional user identifier for personalized responses
        public string? UserId { get; set; }
    }
    
    public class ChatMessage
    {
        public string Content { get; set; } = string.Empty;
        public bool IsUser { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}