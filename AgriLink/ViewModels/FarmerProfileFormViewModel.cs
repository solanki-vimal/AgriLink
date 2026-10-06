using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace AgriLink.ViewModels
{
    public class FarmerProfileFormViewModel
    {
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
        [Display(Name = "Farm Size (Acres)")]
        public decimal? FarmSizeAcres { get; set; }

        [Range(0, 100)]
        [Display(Name = "Years of Experience")]
        public int? YearsOfExperience { get; set; }

        [Display(Name = "Farm Image")]
        public IFormFile? FarmImageFile { get; set; }

        // Used only when editing, to show/keep the current image if no new file is chosen
        public string? ExistingFarmImageUrl { get; set; }
    }
}
