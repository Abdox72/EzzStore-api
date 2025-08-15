using System.Collections.Generic;
using Ezz_api.Models;

namespace Ezz_api.DTOs
{
    public class ChatbotResponse
    {
        public string Reply { get; set; } = string.Empty;
        
        // Related products that match the query
        public List<Product>? RelatedProducts { get; set; }
        
        // Suggested categories based on the query
        public List<Category>? SuggestedCategories { get; set; }
        
        // Indicates if this is a system message (error, notification, etc.)
        public bool IsSystemMessage { get; set; }
        
        // Additional context that might be helpful for the frontend
        public Dictionary<string, string>? AdditionalContext { get; set; }
    }
}