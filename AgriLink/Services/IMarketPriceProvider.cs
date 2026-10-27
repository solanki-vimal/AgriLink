namespace AgriLink.Services
{
    // One normalized rate for a commodity, already converted to rupees per kg.
    public record ProviderPrice(
        string Commodity,
        DateTime Date,
        decimal PricePerKg,
        decimal MinPricePerKg,
        decimal MaxPricePerKg,
        int MarketCount);

    // Raised when the external source itself fails (down, blocked, bad key, bad response).
    // "No data for this commodity" is NOT an error - providers return null for that.
    public class MarketPriceProviderException : Exception
    {
        public MarketPriceProviderException(string message, Exception? inner = null)
            : base(message, inner)
        {
        }
    }

    // Anything that can supply daily market rates. Agmarknet is the first
    // implementation; another feed can be added later without touching
    // Produce, Order, or the Browse pages.
    public interface IMarketPriceProvider
    {
        string SourceName { get; }

        Task<ProviderPrice?> GetLatestPriceAsync(
            string commodity, string state, CancellationToken cancellationToken = default);
    }
}
