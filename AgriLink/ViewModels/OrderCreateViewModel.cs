using System.ComponentModel.DataAnnotations;

namespace AgriLink.ViewModels
{
    public class OrderCreateViewModel
    {
        public int ProduceId { get; set; }

        // Read-only display context, re-fetched server-side on POST too —
        // never trusted from the form for anything that affects price/stock.
        public string ProduceName { get; set; }
        public string Unit { get; set; }
        public decimal Price { get; set; }
        public decimal AvailableQuantity { get; set; }
        public string FarmerName { get; set; }
        public string? ImageUrl { get; set; }

        [Required]
        [Range(0.1, 100000, ErrorMessage = "Quantity must be greater than 0.")]
        [Display(Name = "Quantity to Order")]
        public decimal QuantityOrdered { get; set; }
    }
}
