using AgriLink.Models;

namespace AgriLink.Services
{
    public interface IOrderEmailNotificationService
    {
        Task SendOrderPlacedAsync(Order order);
        Task SendOrderStatusChangedAsync(Order order, OrderStatus newStatus);
        Task SendNegotiationOfferAsync(Order order, PriceOffer offer, string recipientEmail, string recipientName, string proposerName, bool isCounterOffer);
        Task SendNegotiationAcceptedAsync(Order order, PriceOffer agreedOffer);
    }
}
