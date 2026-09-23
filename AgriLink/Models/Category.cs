using System.ComponentModel.DataAnnotations;

namespace AgriLink.Models
{
    public class Category
    {
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "Category name is required.")]
        [StringLength(50, MinimumLength = 2,
            ErrorMessage = "Category name must be between 2 and 50 characters.")]
        public string Name { get; set; }

        [StringLength(250)]
        public string? Description { get; set; }

        // Navigation properties
        public ICollection<Produce> ProduceListings { get; set; } = new List<Produce>();
        public ICollection<MarketPrice> MarketPrices { get; set; } = new List<MarketPrice>();
    }
}
