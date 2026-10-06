using AgriLink.Models;

namespace AgriLink.ViewModels
{
    public class BrowseViewModel
    {
        public IEnumerable<Produce> ProduceListings { get; set; } = new List<Produce>();
        public IEnumerable<Category> Categories { get; set; } = new List<Category>();

        // Filter Inputs
        public string? SearchTerm { get; set; }
        public int? CategoryId { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public string? Location { get; set; }
        public string SortBy { get; set; } = "newest";

        // Stats
        public int TotalResultsCount { get; set; }
    }
}
