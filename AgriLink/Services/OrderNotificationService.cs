using AgriLink.Data;
using AgriLink.Models;
using Microsoft.EntityFrameworkCore;

namespace AgriLink.Services
{
    public interface IOrderNotificationService
    {
        Task<int> GetPendingFarmerNegotiationsCountAsync(string farmerId);
        Task<int> GetPendingBuyerNegotiationsCountAsync(string buyerId);
    }

    public class OrderNotificationService : IOrderNotificationService
    {
        private readonly ApplicationDbContext _context;

        public OrderNotificationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<int> GetPendingFarmerNegotiationsCountAsync(string farmerId)
        {
            if (string.IsNullOrEmpty(farmerId)) return 0;

            return await _context.Orders
                .Where(o => o.Produce.FarmerId == farmerId && o.Status == OrderStatus.Negotiating)
                .Where(o => o.PriceOffers
                    .OrderByDescending(po => po.CreatedAt)
                    .Take(1)
                    .Any(po => po.Status == OfferStatus.Pending && po.ProposerId != farmerId && po.ExpiresAt > DateTime.Now))
                .CountAsync();
        }

        public async Task<int> GetPendingBuyerNegotiationsCountAsync(string buyerId)
        {
            if (string.IsNullOrEmpty(buyerId)) return 0;

            return await _context.Orders
                .Where(o => o.BuyerId == buyerId && o.Status == OrderStatus.Negotiating)
                .Where(o => o.PriceOffers
                    .OrderByDescending(po => po.CreatedAt)
                    .Take(1)
                    .Any(po => po.Status == OfferStatus.Pending && po.ProposerId != buyerId && po.ExpiresAt > DateTime.Now))
                .CountAsync();
        }
    }
}
