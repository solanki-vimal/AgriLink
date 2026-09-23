using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgriLink.Models
{
    public enum OrderStatus
    {
        Pending,
        Accepted,
        Rejected,
        Completed
    }

    public class Order
    {
        public int OrderId { get; set; }

        // FK: Produce
        [Required]
        public int ProduceId { get; set; }

        [ForeignKey("ProduceId")]
        public Produce Produce { get; set; }

        // FK: User (Buyer)
        [Required]
        public string BuyerId { get; set; }

        [ForeignKey("BuyerId")]
        public ApplicationUser Buyer { get; set; }

        [Required]
        [Range(0.1, 100000, ErrorMessage = "Order quantity must be greater than 0.")]
        [Column(TypeName = "decimal(10,2)")]
        [Display(Name = "Quantity Ordered")]
        public decimal QuantityOrdered { get; set; }

        // Stored, not computed live - Produce.Price can change after
        // the order is placed, so we freeze the price at order time.
        [Column(TypeName = "decimal(10,2)")]
        [Display(Name = "Total Price")]
        public decimal TotalPrice { get; set; }

        [Required]
        public OrderStatus Status { get; set; } = OrderStatus.Pending;

        [Display(Name = "Order Date")]
        public DateTime OrderDate { get; set; } = DateTime.Now;
    }
}
