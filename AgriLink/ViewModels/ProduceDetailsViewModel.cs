using AgriLink.Models;

namespace AgriLink.ViewModels
{
    public class ProduceDetailsViewModel
    {
        public Produce Produce { get; set; } = null!;
        public FarmerProfile? FarmerProfile { get; set; }
        public IEnumerable<PriceHistory> PriceHistories { get; set; } = new List<PriceHistory>();
        public MarketPrice? MarketPriceBenchmark { get; set; }

        // Data arrays for Chart.js
        public List<string> ChartLabels { get; set; } = new List<string>();
        public List<decimal> ChartPrices { get; set; } = new List<decimal>();
    }
}
