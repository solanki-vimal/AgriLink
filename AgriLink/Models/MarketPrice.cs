using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgriLink.Models
{
    public class MarketPrice
    {
        public int MarketPriceId { get; set; }

        // FK: Category
        [Required]
        public int CategoryId { get; set; }

        [ForeignKey("CategoryId")]
        public Category Category { get; set; }

        // FK: User (Admin)
        [Required]
        public string SetByAdminId { get; set; }

        [ForeignKey("SetByAdminId")]
        public ApplicationUser SetByAdmin { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime Date { get; set; }

        [Required]
        [Range(0.01, 100000, ErrorMessage = "Price must be greater than 0.")]
        [Column(TypeName = "decimal(10,2)")]
        [Display(Name = "Price Per Unit")]
        public decimal PricePerUnit { get; set; }

        [Required]
        [StringLength(20)]
        public string Unit { get; set; }
    }
}
