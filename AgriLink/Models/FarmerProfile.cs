using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgriLink.Models
{
    public class FarmerProfile
    {
        [Key]
        [ForeignKey("User")]
        public string UserId { get; set; }

        [Required(ErrorMessage = "Farm name is required.")]
        [StringLength(100)]
        [Display(Name = "Farm Name")]
        public string FarmName { get; set; }

        [StringLength(500)]
        public string? Bio { get; set; }

        [Required(ErrorMessage = "Farm location is required.")]
        [StringLength(150)]
        [Display(Name = "Farm Location")]
        public string FarmLocation { get; set; }

        [Range(0, 10000)]
        [Column(TypeName = "decimal(6,2)")]
        [Display(Name = "Farm Size (Acres)")]
        public decimal? FarmSizeAcres { get; set; }

        [Range(0, 100)]
        [Display(Name = "Years of Experience")]
        public int? YearsOfExperience { get; set; }

        [Display(Name = "Farm Image")]
        public string? FarmImageUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation property back to the owning user
        public ApplicationUser User { get; set; }
    }
}
