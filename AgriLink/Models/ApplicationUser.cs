using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace AgriLink.Models
{
    // Extends IdentityUser: Id, UserName, Email, PasswordHash, PhoneNumber
    // are already provided by Identity - only the extra fields go here.
    public class ApplicationUser : IdentityUser
    {
        [Required(ErrorMessage = "Full name is required.")]
        [StringLength(100, MinimumLength = 3,
            ErrorMessage = "Full name must be between 3 and 100 characters.")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Address is required.")]
        [StringLength(200)]
        public string Address { get; set; }

        [Display(Name = "Profile Image")]
        public string? ProfileImageUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        // Farmer side: produce this user has listed
        public ICollection<Produce> ProduceListings { get; set; } = new List<Produce>();

        // Buyer side: orders this user has placed
        public ICollection<Order> Orders { get; set; } = new List<Order>();

        // Admin side: market prices this user has set
        public ICollection<MarketPrice> MarketPricesSet { get; set; } = new List<MarketPrice>();

        // Farmer side: one-to-one profile (null for Buyer/Admin accounts)
        public FarmerProfile? FarmerProfile { get; set; }
    }
}
