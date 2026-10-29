using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgriLink.Models
{
    public enum OrderStatus
    {
        Pending,
        Accepted,
        Rejected,
        Completed,
        Negotiating
    }

    public class Order
    {
        public int OrderId { get; set; }

        // FK: Produce
        [Required]
        public int ProduceId { get; set; }

        [ForeignKey("ProduceId")]
        public Produce Produce { get; set; } = null!;

        // FK: User (Buyer)
        [Required]
        public string BuyerId { get; set; } = null!;

        [ForeignKey("BuyerId")]
        public ApplicationUser Buyer { get; set; } = null!;

        [Required]
        [Range(0.1, 100000, ErrorMessage = "Order quantity must be greater than 0.")]
        [Column(TypeName = "decimal(10,2)")]
        [Display(Name = "Quantity Ordered")]
        public decimal QuantityOrdered { get; set; }

        // Unit price snapshot frozen at order / agreement time
        [Column(TypeName = "decimal(10,2)")]
        [Display(Name = "Unit Price")]
        public decimal UnitPrice { get; set; }

        // Stored, not computed live - Produce.Price can change after
        // the order is placed, so we freeze the price at order time.
        [Column(TypeName = "decimal(10,2)")]
        [Display(Name = "Total Price")]
        public decimal TotalPrice { get; set; }

        // Snapshots of listing details frozen at order time
        // so receipts and order history remain accurate even if
        // the farmer edits or removes the listing later.
        [MaxLength(100)]
        [Display(Name = "Produce Name")]
        public string ProduceName { get; set; } = string.Empty;

        [MaxLength(20)]
        public string Unit { get; set; } = string.Empty;

        [MaxLength(100)]
        [Display(Name = "Farmer Name")]
        public string FarmerName { get; set; } = string.Empty;

        [Required]
        public OrderStatus Status { get; set; } = OrderStatus.Pending;

        [Display(Name = "Order Date")]
        public DateTime OrderDate { get; set; } = DateTime.Now;

        // Price negotiation offer thread
        public ICollection<PriceOffer> PriceOffers { get; set; } = new List<PriceOffer>();
    }
}
