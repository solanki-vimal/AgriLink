using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgriLink.Models
{
    public enum OfferStatus
    {
        Pending,
        Accepted,
        Rejected,
        Countered,
        Expired
    }

    public class PriceOffer
    {
        public int PriceOfferId { get; set; }

        // FK: Order
        [Required]
        public int OrderId { get; set; }

        [ForeignKey("OrderId")]
        public Order Order { get; set; } = null!;

        // FK: Proposer (Buyer or Farmer)
        [Required]
        public string ProposerId { get; set; } = null!;

        [ForeignKey("ProposerId")]
        public ApplicationUser Proposer { get; set; } = null!;

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        [Display(Name = "Offered Unit Price")]
        [Range(0.01, 1000000, ErrorMessage = "Price must be greater than zero.")]
        public decimal OfferedUnitPrice { get; set; }

        [MaxLength(500)]
        [Display(Name = "Negotiation Note")]
        public string? Message { get; set; }

        [Required]
        public OfferStatus Status { get; set; } = OfferStatus.Pending;

        [Range(1, 3)]
        [Display(Name = "Negotiation Round")]
        public int RoundNumber { get; set; } = 1;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Offers expire after 48 hours if unresponded
        public DateTime ExpiresAt { get; set; } = DateTime.Now.AddHours(48);
    }
}
