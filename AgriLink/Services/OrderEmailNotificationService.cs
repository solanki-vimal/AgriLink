using System.Text.Encodings.Web;
using AgriLink.Data;
using AgriLink.Models;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;

namespace AgriLink.Services
{
    public class OrderEmailNotificationService : IOrderEmailNotificationService
    {
        private readonly IEmailSender _emailSender;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<OrderEmailNotificationService> _logger;
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public OrderEmailNotificationService(
            IEmailSender emailSender,
            IHttpContextAccessor httpContextAccessor,
            ILogger<OrderEmailNotificationService> logger,
            ApplicationDbContext context,
            IConfiguration configuration)
        {
            _emailSender = emailSender;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _context = context;
            _configuration = configuration;
        }

        private string GetBaseUrl()
        {
            var configUrl = _configuration["App:BaseUrl"];
            if (!string.IsNullOrWhiteSpace(configUrl))
            {
                return configUrl.TrimEnd('/');
            }

            var request = _httpContextAccessor.HttpContext?.Request;
            if (request == null) return "https://agrilink-ky96.onrender.com";
            return $"{request.Scheme}://{request.Host}";
        }

        public async Task SendOrderPlacedAsync(Order order)
        {
            try
            {
                // Ensure buyer and produce farmer are loaded
                var buyer = order.Buyer ?? await _context.Users.FindAsync(order.BuyerId);
                var produce = order.Produce ?? await _context.ProduceListings
                    .Include(p => p.Farmer)
                    .FirstOrDefaultAsync(p => p.ProduceId == order.ProduceId);

                var farmer = produce?.Farmer;
                var baseUrl = GetBaseUrl();
                var produceName = !string.IsNullOrWhiteSpace(order.ProduceName) ? order.ProduceName : (produce?.Name ?? "Produce Item");
                var unit = !string.IsNullOrWhiteSpace(order.Unit) ? order.Unit : (produce?.Unit ?? "units");

                // 1. Send alert to Farmer
                if (farmer != null && !string.IsNullOrWhiteSpace(farmer.Email))
                {
                    var isNegotiating = order.Status == OrderStatus.Negotiating;
                    var actionUrl = isNegotiating 
                        ? $"{baseUrl}/Order/Negotiate/{order.OrderId}" 
                        : $"{baseUrl}/Order/Manage";

                    var farmerSubject = isNegotiating
                        ? $"[AgriLink] New Price Offer for {produceName} (Order #{order.OrderId})"
                        : $"[AgriLink] New Order #{order.OrderId} Received for {produceName}";

                    var farmerBody = $@"
<div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e0e0e0; border-radius: 8px;'>
    <div style='background-color: #2e7d32; color: #ffffff; padding: 16px 20px; border-radius: 6px 6px 0 0; margin: -24px -24px 20px -24px;'>
        <h2 style='margin: 0; font-size: 20px;'>AgriLink · New Order Received</h2>
    </div>
    <p>Hello <strong>{farmer.FullName}</strong>,</p>
    <p>{(isNegotiating ? "A buyer has placed an order with a <strong>custom price offer</strong>:" : "You have received a new order on your listing:")}</p>
    
    <div style='background-color: #f9f9f9; border-left: 4px solid #2e7d32; padding: 16px; margin: 20px 0;'>
        <p style='margin: 0 0 8px 0;'><strong>Order #:</strong> #{order.OrderId}</p>
        <p style='margin: 0 0 8px 0;'><strong>Produce:</strong> {produceName}</p>
        <p style='margin: 0 0 8px 0;'><strong>Quantity:</strong> {order.QuantityOrdered} {unit}</p>
        <p style='margin: 0 0 8px 0;'><strong>{(isNegotiating ? "Proposed Unit Price" : "Unit Price")}:</strong> ₹{order.UnitPrice:N2} / {unit}</p>
        <p style='margin: 0 0 8px 0;'><strong>Total Amount:</strong> ₹{order.TotalPrice:N2}</p>
        <p style='margin: 0;'><strong>Buyer:</strong> {buyer?.FullName ?? "Buyer"} ({(string.IsNullOrWhiteSpace(buyer?.PhoneNumber) ? buyer?.Email : buyer?.PhoneNumber)})</p>
    </div>

    <div style='text-align: center; margin: 30px 0;'>
        <a href='{actionUrl}' style='background-color: #2e7d32; color: #ffffff; padding: 12px 28px; text-decoration: none; border-radius: 6px; font-weight: bold; display: inline-block;'>
            {(isNegotiating ? "Review Price Offer" : "View in Manage Orders")}
        </a>
    </div>

    <p style='color: #666; font-size: 13px;'>Please log in to your AgriLink account to confirm the order or respond to the offer.</p>
</div>";

                    await _emailSender.SendEmailAsync(farmer.Email, farmerSubject, farmerBody);
                }

                // 2. Send confirmation to Buyer
                if (buyer != null && !string.IsNullOrWhiteSpace(buyer.Email))
                {
                    var buyerActionUrl = $"{baseUrl}/Order/Confirmation/{order.OrderId}";
                    var buyerSubject = $"[AgriLink] Order #{order.OrderId} Placed Successfully";

                    var buyerBody = $@"
<div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e0e0e0; border-radius: 8px;'>
    <div style='background-color: #2e7d32; color: #ffffff; padding: 16px 20px; border-radius: 6px 6px 0 0; margin: -24px -24px 20px -24px;'>
        <h2 style='margin: 0; font-size: 20px;'>AgriLink · Order Placed</h2>
    </div>
    <p>Hello <strong>{buyer.FullName}</strong>,</p>
    <p>Thank you for ordering with AgriLink! Your order has been placed with <strong>{order.FarmerName}</strong>.</p>
    
    <div style='background-color: #f9f9f9; border-left: 4px solid #2e7d32; padding: 16px; margin: 20px 0;'>
        <p style='margin: 0 0 8px 0;'><strong>Order #:</strong> #{order.OrderId}</p>
        <p style='margin: 0 0 8px 0;'><strong>Produce:</strong> {produceName}</p>
        <p style='margin: 0 0 8px 0;'><strong>Quantity:</strong> {order.QuantityOrdered} {unit}</p>
        <p style='margin: 0 0 8px 0;'><strong>Unit Price:</strong> ₹{order.UnitPrice:N2} / {unit}</p>
        <p style='margin: 0 0 8px 0;'><strong>Total Amount:</strong> ₹{order.TotalPrice:N2}</p>
        <p style='margin: 0;'><strong>Status:</strong> {order.Status}</p>
    </div>

    <div style='text-align: center; margin: 30px 0;'>
        <a href='{buyerActionUrl}' style='background-color: #2e7d32; color: #ffffff; padding: 12px 28px; text-decoration: none; border-radius: 6px; font-weight: bold; display: inline-block;'>
            View Order Confirmation
        </a>
    </div>

    <p style='color: #666; font-size: 13px;'>You will receive an update once the farmer reviews your order.</p>
</div>";

                    await _emailSender.SendEmailAsync(buyer.Email, buyerSubject, buyerBody);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed sending order placed notification for Order #{OrderId}", order.OrderId);
            }
        }

        public async Task SendOrderStatusChangedAsync(Order order, OrderStatus newStatus)
        {
            try
            {
                var buyer = order.Buyer ?? await _context.Users.FindAsync(order.BuyerId);
                if (buyer == null || string.IsNullOrWhiteSpace(buyer.Email)) return;

                var baseUrl = GetBaseUrl();
                var produceName = !string.IsNullOrWhiteSpace(order.ProduceName) ? order.ProduceName : (order.Produce?.Name ?? "Produce Item");
                var farmerName = !string.IsNullOrWhiteSpace(order.FarmerName) ? order.FarmerName : (order.Produce?.Farmer?.FullName ?? "The Farmer");

                string subject;
                string statusHeadline;
                string statusColor;
                string statusMessage;
                string buttonText;
                string buttonUrl;

                switch (newStatus)
                {
                    case OrderStatus.Accepted:
                        subject = $"[AgriLink] Good News! Order #{order.OrderId} Has Been Accepted";
                        statusHeadline = "Order Confirmed by Farmer!";
                        statusColor = "#2e7d32";
                        statusMessage = $"<strong>{farmerName}</strong> has confirmed and accepted your order for <strong>{produceName}</strong>. Your produce is now reserved.";
                        buttonText = "View Order & Receipt";
                        buttonUrl = $"{baseUrl}/Order/Confirmation/{order.OrderId}";
                        break;

                    case OrderStatus.Completed:
                        subject = $"[AgriLink] Order #{order.OrderId} Completed - Thank You!";
                        statusHeadline = "Order Fulfilled & Completed";
                        statusColor = "#1976d2";
                        statusMessage = $"Your order for <strong>{produceName}</strong> with <strong>{farmerName}</strong> has been completed. Thank you for supporting local farmers!";
                        buttonText = "Download Sales Receipt";
                        buttonUrl = $"{baseUrl}/Order/Receipt/{order.OrderId}";
                        break;

                    case OrderStatus.Rejected:
                        subject = $"[AgriLink] Update on Order #{order.OrderId}";
                        statusHeadline = "Order Declined";
                        statusColor = "#d32f2f";
                        statusMessage = $"Unfortunately, <strong>{farmerName}</strong> was unable to fulfill your order for <strong>{produceName}</strong> (e.g., harvest inventory unavailable). No payment is due.";
                        buttonText = "Browse Other Listings";
                        buttonUrl = $"{baseUrl}/Browse";
                        break;

                    default:
                        return;
                }

                var htmlBody = $@"
<div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e0e0e0; border-radius: 8px;'>
    <div style='background-color: {statusColor}; color: #ffffff; padding: 16px 20px; border-radius: 6px 6px 0 0; margin: -24px -24px 20px -24px;'>
        <h2 style='margin: 0; font-size: 20px;'>AgriLink · {statusHeadline}</h2>
    </div>
    <p>Hello <strong>{buyer.FullName}</strong>,</p>
    <p>{statusMessage}</p>

    <div style='background-color: #f9f9f9; border-left: 4px solid {statusColor}; padding: 16px; margin: 20px 0;'>
        <p style='margin: 0 0 8px 0;'><strong>Order #:</strong> #{order.OrderId}</p>
        <p style='margin: 0 0 8px 0;'><strong>Produce:</strong> {produceName}</p>
        <p style='margin: 0 0 8px 0;'><strong>Quantity:</strong> {order.QuantityOrdered} {order.Unit}</p>
        <p style='margin: 0 0 8px 0;'><strong>Total:</strong> ₹{order.TotalPrice:N2}</p>
        <p style='margin: 0;'><strong>Farmer:</strong> {farmerName}</p>
    </div>

    <div style='text-align: center; margin: 30px 0;'>
        <a href='{buttonUrl}' style='background-color: {statusColor}; color: #ffffff; padding: 12px 28px; text-decoration: none; border-radius: 6px; font-weight: bold; display: inline-block;'>
            {buttonText}
        </a>
    </div>

    <p style='color: #666; font-size: 13px;'>Payment settlement is handled directly between buyer and farmer via cash, UPI, or your agreed method.</p>
</div>";

                await _emailSender.SendEmailAsync(buyer.Email, subject, htmlBody);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed sending status update email for Order #{OrderId} to {Status}", order.OrderId, newStatus);
            }
        }

        public async Task SendNegotiationOfferAsync(Order order, PriceOffer offer, string recipientEmail, string recipientName, string proposerName, bool isCounterOffer)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(recipientEmail)) return;

                var baseUrl = GetBaseUrl();
                var actionUrl = $"{baseUrl}/Order/Negotiate/{order.OrderId}";
                var produceName = !string.IsNullOrWhiteSpace(order.ProduceName) ? order.ProduceName : (order.Produce?.Name ?? "Produce Item");
                var unit = !string.IsNullOrWhiteSpace(order.Unit) ? order.Unit : (order.Produce?.Unit ?? "units");

                var subject = isCounterOffer
                    ? $"[AgriLink] Counter-Offer (Round {offer.RoundNumber}) on Order #{order.OrderId}"
                    : $"[AgriLink] Price Offer on Order #{order.OrderId} for {produceName}";

                var headline = isCounterOffer
                    ? $"Round {offer.RoundNumber} Counter-Offer Received"
                    : $"Price Offer Received (Round {offer.RoundNumber})";

                var htmlBody = $@"
<div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e0e0e0; border-radius: 8px;'>
    <div style='background-color: #f57c00; color: #ffffff; padding: 16px 20px; border-radius: 6px 6px 0 0; margin: -24px -24px 20px -24px;'>
        <h2 style='margin: 0; font-size: 20px;'>AgriLink · {headline}</h2>
    </div>
    <p>Hello <strong>{recipientName}</strong>,</p>
    <p><strong>{proposerName}</strong> has submitted a {(isCounterOffer ? "counter-offer" : "price offer")} for Order #{order.OrderId}:</p>

    <div style='background-color: #fff8e1; border-left: 4px solid #f57c00; padding: 16px; margin: 20px 0;'>
        <p style='margin: 0 0 8px 0;'><strong>Produce:</strong> {produceName}</p>
        <p style='margin: 0 0 8px 0;'><strong>Quantity:</strong> {order.QuantityOrdered} {unit}</p>
        <p style='margin: 0 0 8px 0;'><strong>Offered Price:</strong> <span style='font-size: 18px; color: #2e7d32; font-weight: bold;'>₹{offer.OfferedUnitPrice:N2}</span> / {unit}</p>
        <p style='margin: 0 0 8px 0;'><strong>Estimated Total:</strong> ₹{Math.Round(order.QuantityOrdered * offer.OfferedUnitPrice, 2):N2}</p>
        {(string.IsNullOrWhiteSpace(offer.Message) ? "" : $"<p style='margin: 0 0 8px 0;'><strong>Note:</strong> <em>\"{offer.Message}\"</em></p>")}
        <p style='margin: 0; color: #d32f2f; font-size: 13px;'><strong>Expires:</strong> {offer.ExpiresAt:dd MMM yyyy, hh:mm tt} (48 hours)</p>
    </div>

    <div style='text-align: center; margin: 30px 0;'>
        <a href='{actionUrl}' style='background-color: #f57c00; color: #ffffff; padding: 12px 28px; text-decoration: none; border-radius: 6px; font-weight: bold; display: inline-block;'>
            Review &amp; Respond to Offer
        </a>
    </div>

    <p style='color: #666; font-size: 13px;'>You can accept, counter-offer (up to 3 rounds), or decline this proposal.</p>
</div>";

                await _emailSender.SendEmailAsync(recipientEmail, subject, htmlBody);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed sending negotiation offer email for Order #{OrderId}", order.OrderId);
            }
        }

        public async Task SendNegotiationAcceptedAsync(Order order, PriceOffer agreedOffer)
        {
            try
            {
                var buyer = order.Buyer ?? await _context.Users.FindAsync(order.BuyerId);
                var produce = order.Produce ?? await _context.ProduceListings
                    .Include(p => p.Farmer)
                    .FirstOrDefaultAsync(p => p.ProduceId == order.ProduceId);
                var farmer = produce?.Farmer;

                var baseUrl = GetBaseUrl();
                var produceName = !string.IsNullOrWhiteSpace(order.ProduceName) ? order.ProduceName : (produce?.Name ?? "Produce Item");
                var unit = !string.IsNullOrWhiteSpace(order.Unit) ? order.Unit : (produce?.Unit ?? "units");
                var subject = $"[AgriLink] Price Agreed! Order #{order.OrderId} Confirmed";

                var commonBody = $@"
<div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e0e0e0; border-radius: 8px;'>
    <div style='background-color: #2e7d32; color: #ffffff; padding: 16px 20px; border-radius: 6px 6px 0 0; margin: -24px -24px 20px -24px;'>
        <h2 style='margin: 0; font-size: 20px;'>AgriLink · Negotiation Successful</h2>
    </div>
    <p>Price negotiation concluded successfully! Both parties have agreed on the price and the order is now <strong>Confirmed (Accepted)</strong>.</p>

    <div style='background-color: #e8f5e9; border-left: 4px solid #2e7d32; padding: 16px; margin: 20px 0;'>
        <p style='margin: 0 0 8px 0;'><strong>Order #:</strong> #{order.OrderId}</p>
        <p style='margin: 0 0 8px 0;'><strong>Produce:</strong> {produceName}</p>
        <p style='margin: 0 0 8px 0;'><strong>Agreed Unit Price:</strong> ₹{order.UnitPrice:N2} / {unit}</p>
        <p style='margin: 0 0 8px 0;'><strong>Quantity:</strong> {order.QuantityOrdered} {unit}</p>
        <p style='margin: 0;'><strong>Total Price:</strong> ₹{order.TotalPrice:N2}</p>
    </div>

    <div style='text-align: center; margin: 30px 0;'>
        <a href='{baseUrl}/Order/Confirmation/{order.OrderId}' style='background-color: #2e7d32; color: #ffffff; padding: 12px 28px; text-decoration: none; border-radius: 6px; font-weight: bold; display: inline-block;'>
            View Order Confirmation
        </a>
    </div>
</div>";

                if (buyer != null && !string.IsNullOrWhiteSpace(buyer.Email))
                {
                    await _emailSender.SendEmailAsync(buyer.Email, subject, commonBody);
                }

                if (farmer != null && !string.IsNullOrWhiteSpace(farmer.Email))
                {
                    await _emailSender.SendEmailAsync(farmer.Email, subject, commonBody);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed sending negotiation acceptance email for Order #{OrderId}", order.OrderId);
            }
        }
    }
}
