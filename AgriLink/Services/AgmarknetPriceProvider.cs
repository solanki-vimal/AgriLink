using System.Globalization;
using System.Net;
using System.Text.Json;

namespace AgriLink.Services
{
    public class AgmarknetPriceProvider : IMarketPriceProvider
    {
        // "Current Daily Price of Various Commodities from Various Markets (Mandi)"
        private const string ResourceUrl =
            "https://api.data.gov.in/resource/9ef84268-d588-465a-a308-a864a43d0070";

        // Mandi rates are quoted in rupees per quintal; 1 quintal = 100 kg.
        private const decimal KgPerQuintal = 100m;

        private static readonly string[] DateFormats = { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd" };

        private readonly HttpClient _http;
        private readonly IConfiguration _config;
        private readonly ILogger<AgmarknetPriceProvider> _logger;

        public AgmarknetPriceProvider(
            HttpClient http,
            IConfiguration config,
            ILogger<AgmarknetPriceProvider> logger)
        {
            _http = http;
            _config = config;
            _logger = logger;
        }

        public string SourceName => "Agmarknet";

        public async Task<ProviderPrice?> GetLatestPriceAsync(
            string commodity, string state, CancellationToken cancellationToken = default)
        {
            var apiKey = _config["DataGovIn:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new MarketPriceProviderException(
                    "No data.gov.in API key configured (add DataGovIn:ApiKey via Manage User Secrets).");
            }

            var url = $"{ResourceUrl}?api-key={Uri.EscapeDataString(apiKey)}&format=json&limit=100" +
                      $"&filters[state]={Uri.EscapeDataString(state)}" +
                      $"&filters[commodity]={Uri.EscapeDataString(commodity)}";

            try
            {
                using var response = await SendWithOneRetryAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                if (!doc.RootElement.TryGetProperty("records", out var records) ||
                    records.ValueKind != JsonValueKind.Array)
                {
                    return null;
                }

                var rows = new List<(DateTime Date, string Market, decimal Min, decimal Max, decimal Modal)>();

                foreach (var record in records.EnumerateArray())
                {
                    if (!TryReadDate(record, "arrival_date", out var date)) continue;

                    var modal = ReadDecimal(record, "modal_price");
                    if (modal == null || modal <= 0) continue; // skip blank/zero rows

                    var min = ReadDecimal(record, "min_price") ?? modal.Value;
                    var max = ReadDecimal(record, "max_price") ?? modal.Value;
                    var market = record.TryGetProperty("market", out var m) ? m.GetString() ?? "" : "";

                    rows.Add((date, market, min, max, modal.Value));
                }

                if (rows.Count == 0) return null;

                // Use only the most recent arrival date, so we never mix days.
                var latest = rows.Max(r => r.Date);
                var latestRows = rows.Where(r => r.Date == latest).ToList();

                // Median (not mean) of modal prices: one outlier mandi shouldn't skew the benchmark.
                var medianPerQuintal = Median(latestRows.Select(r => r.Modal).ToList());

                return new ProviderPrice(
                    Commodity: commodity,
                    Date: latest,
                    PricePerKg: ToKg(medianPerQuintal),
                    MinPricePerKg: ToKg(latestRows.Min(r => r.Min)),
                    MaxPricePerKg: ToKg(latestRows.Max(r => r.Max)),
                    MarketCount: latestRows.Select(r => r.Market).Distinct().Count());
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Agmarknet request failed for {Commodity}", commodity);

                var reason = ex.StatusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                        "data.gov.in rejected the request (HTTP " + (int)ex.StatusCode + ") - check the API key, or the portal may be blocking requests",
                    not null => "data.gov.in returned HTTP " + (int)ex.StatusCode,
                    _ => "could not reach data.gov.in"
                };
                throw new MarketPriceProviderException(reason, ex);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning(ex, "Agmarknet request timed out for {Commodity}", commodity);
                throw new MarketPriceProviderException("data.gov.in timed out", ex);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Agmarknet returned a non-JSON response for {Commodity}", commodity);
                throw new MarketPriceProviderException(
                    "data.gov.in returned an unexpected (non-JSON) response - it may be down or blocking requests", ex);
            }
        }

        // data.gov.in fails intermittently, so retry once on a server error (5xx).
        private async Task<HttpResponseMessage> SendWithOneRetryAsync(string url, CancellationToken ct)
        {
            var response = await SendAsync(url, ct);

            if ((int)response.StatusCode >= 500)
            {
                response.Dispose();
                await Task.Delay(TimeSpan.FromSeconds(1), ct);
                response = await SendAsync(url, ct);
            }

            return response;
        }

        private async Task<HttpResponseMessage> SendAsync(string url, CancellationToken ct)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.ParseAdd("application/json");
            request.Headers.UserAgent.ParseAdd("AgriLink/1.0 (college project)");
            return await _http.SendAsync(request, ct);
        }

        private static decimal ToKg(decimal perQuintal) =>
            Math.Round(perQuintal / KgPerQuintal, 2);

        private static decimal Median(List<decimal> values)
        {
            values.Sort();
            var n = values.Count;
            return n % 2 == 1
                ? values[n / 2]
                : (values[n / 2 - 1] + values[n / 2]) / 2m;
        }

        // The API returns numbers, but be tolerant of numeric strings too.
        private static decimal? ReadDecimal(JsonElement record, string name)
        {
            if (!record.TryGetProperty(name, out var prop)) return null;

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDecimal(out var number))
            {
                return number;
            }

            if (prop.ValueKind == JsonValueKind.String &&
                decimal.TryParse(prop.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            return null;
        }

        private static bool TryReadDate(JsonElement record, string name, out DateTime date)
        {
            date = default;
            return record.TryGetProperty(name, out var prop)
                && prop.ValueKind == JsonValueKind.String
                && DateTime.TryParseExact(prop.GetString(), DateFormats,
                       CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }
    }
}