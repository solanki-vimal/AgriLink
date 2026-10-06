using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace AgriLink.ViewModels
{
    public class ProduceFormViewModel
    {
        public int ProduceId { get; set; }

        [Required(ErrorMessage = "Please select a category.")]
        [Display(Name = "Category")]
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "Produce name is required.")]
        [StringLength(100, MinimumLength = 2)]
        public string Name { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        [Range(0.1, 100000, ErrorMessage = "Quantity must be greater than 0.")]
        public decimal Quantity { get; set; }

        [Required(ErrorMessage = "Unit is required (e.g. kg, quintal, dozen).")]
        [StringLength(20)]
        public string Unit { get; set; }

        [Required]
        [Range(0.01, 100000, ErrorMessage = "Price must be greater than 0.")]
        [Display(Name = "Asking Price")]
        public decimal Price { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Harvest Date")]
        public DateTime HarvestDate { get; set; } = DateTime.Today;

        [Required]
        [StringLength(100)]
        public string Location { get; set; }

        [Display(Name = "Produce Image")]
        public IFormFile? ImageFile { get; set; }

        // Used only on Edit, to show/keep the current image if no new file is chosen
        public string? ExistingImageUrl { get; set; }
    }
}