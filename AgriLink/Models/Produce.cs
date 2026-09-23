using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgriLink.Models
{
    public enum ProduceStatus
    {
        Available,
        SoldOut
    }

    public class Produce
    {
        public int ProduceId { get; set; }

        // FK: User (Farmer)
        [Required]
        public string FarmerId { get; set; }


        [ForeignKey("FarmerId")]
        public ApplicationUser Farmer { get; set; }


        // FK: Category
        [Required]
        public int CategoryId { get; set; }


        [ForeignKey("CategoryId")]
        public Category Category { get; set; }


        [Required(ErrorMessage = "Produce name is required.")]
        [StringLength(100, MinimumLength = 2)]
        public string Name { get; set; }


        [StringLength(500)]
        public string? Description { get; set; }


        [Required]
        [Range(0.1, 100000, ErrorMessage = "Quantity must be greater than 0.")]
        [Column(TypeName = "decimal(10,2)")]
        public decimal Quantity { get; set; }


        [Required(ErrorMessage = "Unit is required (e.g. kg, quintal, dozen).")]
        [StringLength(20)]
        public string Unit { get; set; }


        [Required]
        [Range(0.01, 100000, ErrorMessage = "Price must be greater than 0.")]
        [Column(TypeName = "decimal(10,2)")]
        [Display(Name = "Asking Price")]
        public decimal Price { get; set; }


        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Harvest Date")]
        public DateTime HarvestDate { get; set; }


        [Required]
        [StringLength(100)]
        public string Location { get; set; }


        [Display(Name = "Produce Image")]
        public string? ImageUrl { get; set; }


        [Required]
        public ProduceStatus Status { get; set; } = ProduceStatus.Available;


        public DateTime CreatedAt { get; set; } = DateTime.Now;


        // Navigation properties
        public ICollection<Order> Orders { get; set; } = new List<Order>();
        public ICollection<PriceHistory> PriceHistories { get; set; } = new List<PriceHistory>();
    }
}
