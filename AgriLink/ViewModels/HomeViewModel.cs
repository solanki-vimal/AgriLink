using AgriLink.Models;

namespace AgriLink.ViewModels
{
    public class HomeViewModel
    {
        public IEnumerable<Category> Categories { get; set; } = new List<Category>();
        public IEnumerable<Produce> RecentListings { get; set; } = new List<Produce>();
        public int TotalListingsCount { get; set; }
        public int TotalFarmersCount { get; set; }
    }
}
