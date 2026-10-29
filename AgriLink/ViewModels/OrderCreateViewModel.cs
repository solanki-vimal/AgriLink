using System.ComponentModel.DataAnnotations;

namespace AgriLink.ViewModels
{
    public class OrderCreateViewModel
    {
        public int ProduceId { get; set; }

        // Read-only display context, re-fetched server-side on POST too —
        // never trusted from the form for anything that affects price/stock.
        public string ProduceName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal AvailableQuantity { get; set; }
        public string FarmerName { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }

        // Agmarknet benchmark reference price (if available for this commodity)
        public decimal? MarketBenchmarkPrice { get; set; }

        [Required]
        [Range(0.1, 100000, ErrorMessage = "Quantity must be greater than 0.")]
        [Display(Name = "Quantity to Order")]
        public decimal QuantityOrdered { get; set; }

        // Optional price negotiation
        [Display(Name = "Make a Price Offer")]
        public bool WantToNegotiate { get; set; }

        [Display(Name = "Your Offered Unit Price")]
        [Range(0.01, 1000000, ErrorMessage = "Offer price must be greater than zero.")]
        public decimal? OfferedUnitPrice { get; set; }

        [MaxLength(500)]
        [Display(Name = "Note to Farmer (Optional)")]
        public string? OfferMessage { get; set; }
    }
}
