using System.ComponentModel.DataAnnotations;

namespace Ezz_api.Models
{
    public class Category
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Name { get; set; }
        
        public string? Description { get; set; }

        public string? ImageUrl { get; set; }

        // Navigation property for products in this category
        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}