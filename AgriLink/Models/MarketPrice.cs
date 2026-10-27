using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgriLink.Models
{
    // A daily reference rate for ONE commodity (e.g. "Tomato"), not a whole category.
    // Rows come from two sources: "Manual" (admin typed it) or "Agmarknet" (synced).
    public class MarketPrice
    {
        public int MarketPriceId { get; set; }

        [Required(ErrorMessage = "Commodity name is required.")]
        [StringLength(100)]
        [Display(Name = "Commodity")]
        public string CommodityName { get; set; }

        // Optional grouping only - the benchmark lookup matches on CommodityName.
        [Display(Name = "Category (optional)")]
        public int? CategoryId { get; set; }

        [ForeignKey("CategoryId")]
        public Category? Category { get; set; }

        // FK: User (Admin who entered it, or who triggered the sync)
        [Required]
        public string SetByAdminId { get; set; }

        [ForeignKey("SetByAdminId")]
        public ApplicationUser? SetByAdmin { get; set; }

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

        // Filled only by the Agmarknet sync (lowest/highest rate seen across mandis)
        [Column(TypeName = "decimal(10,2)")]
        [Display(Name = "Lowest")]
        public decimal? MinPrice { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        [Display(Name = "Highest")]
        public decimal? MaxPrice { get; set; }

        [Display(Name = "Markets Reported")]
        public int? MarketCount { get; set; }

        // "Manual" or "Agmarknet". Editing a synced row flips it to Manual,
        // which stops the next sync from overwriting the admin's correction.
        [Required]
        [StringLength(20)]
        public string Source { get; set; } = "Manual";
    }
}
