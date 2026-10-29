using AgriLink.Data;
using AgriLink.Models;
using AgriLink.Services;
using AgriLink.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriLink.Controllers
{
    [Authorize]
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IOrderEmailNotificationService _emailNotificationService;

        public OrderController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IOrderEmailNotificationService emailNotificationService)
        {
            _context = context;
            _userManager = userManager;
            _emailNotificationService = emailNotificationService;
        }

        // GET: Order/Create?produceId=5
        [Authorize(Roles = "Buyer")]
        public async Task<IActionResult> Create(int produceId)
        {
            var produce = await _context.ProduceListings
                .Include(p => p.Farmer)
                .FirstOrDefaultAsync(p => p.ProduceId == produceId);

            if (produce == null)
            {
                return NotFound();
            }

            if (produce.Status != ProduceStatus.Available || produce.Quantity <= 0)
            {
                TempData["StatusMessage"] = "Sorry, this listing is no longer available.";
                return RedirectToAction("Details", "Browse", new { id = produceId });
            }

            // Look up live Agmarknet benchmark price if available for this commodity
            var benchmark = await _context.MarketPrices
                .Where(mp => produce.Name.Contains(mp.CommodityName) || mp.CommodityName.Contains(produce.Name) || mp.CategoryId == produce.CategoryId)
                .OrderByDescending(mp => mp.Date)
                .FirstOrDefaultAsync();

            var vm = new OrderCreateViewModel
            {
                ProduceId = produce.ProduceId,
                ProduceName = produce.Name,
                Unit = produce.Unit,
                Price = produce.Price,
                AvailableQuantity = produce.Quantity,
                FarmerName = produce.Farmer?.FullName ?? "Unknown Farmer",
                ImageUrl = produce.ImageUrl,
                MarketBenchmarkPrice = benchmark?.PricePerUnit
            };

            return View(vm);
        }

        // POST: Order/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Buyer")]
        public async Task<IActionResult> Create(OrderCreateViewModel vm)
        {
            // Re-fetch fresh from the database — never trust Price/AvailableQuantity
            // posted back from the form, only ProduceId and QuantityOrdered.
            var produce = await _context.ProduceListings
                .Include(p => p.Farmer)
                .FirstOrDefaultAsync(p => p.ProduceId == vm.ProduceId);

            if (produce == null)
            {
                return NotFound();
            }

            // Repopulate read-only display fields for a possible return-to-view
            vm.ProduceName = produce.Name;
            vm.Unit = produce.Unit;
            vm.Price = produce.Price;
            vm.AvailableQuantity = produce.Quantity;
            vm.FarmerName = produce.Farmer?.FullName ?? "Unknown Farmer";
            vm.ImageUrl = produce.ImageUrl;

            var benchmark = await _context.MarketPrices
                .Where(mp => produce.Name.Contains(mp.CommodityName) || mp.CommodityName.Contains(produce.Name) || mp.CategoryId == produce.CategoryId)
                .OrderByDescending(mp => mp.Date)
                .FirstOrDefaultAsync();
            vm.MarketBenchmarkPrice = benchmark?.PricePerUnit;

            if (produce.Status != ProduceStatus.Available || produce.Quantity <= 0)
            {
                ModelState.AddModelError(string.Empty, "This listing is no longer available.");
                return View(vm);
            }

            if (vm.QuantityOrdered > produce.Quantity)
            {
                ModelState.AddModelError(nameof(vm.QuantityOrdered),
                    $"Only {produce.Quantity} {produce.Unit} available.");
                return View(vm);
            }

            // Validate price negotiation rules if buyer made an offer
            bool isNegotiating = vm.WantToNegotiate && vm.OfferedUnitPrice.HasValue && vm.OfferedUnitPrice.Value > 0;
            if (isNegotiating)
            {
                var minAllowedOffer = Math.Round(produce.Price * 0.5m, 2);
                if (vm.OfferedUnitPrice!.Value < minAllowedOffer)
                {
                    ModelState.AddModelError(nameof(vm.OfferedUnitPrice),
                        $"Offer cannot be lower than 50% of the listing price (minimum ₹{minAllowedOffer:N2}).");
                }

                if (vm.OfferedUnitPrice!.Value >= produce.Price)
                {
                    ModelState.AddModelError(nameof(vm.OfferedUnitPrice),
                        $"Offer should be lower than the listing price (₹{produce.Price:N2}).");
                }
            }

            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var buyerId = _userManager.GetUserId(User)!;
            var agreedUnitPrice = isNegotiating ? vm.OfferedUnitPrice!.Value : produce.Price;

            var order = new Order
            {
                ProduceId = produce.ProduceId,
                BuyerId = buyerId,
                QuantityOrdered = vm.QuantityOrdered,
                UnitPrice = agreedUnitPrice,
                // Frozen at order time — produce.Price may change later via Edit
                TotalPrice = vm.QuantityOrdered * agreedUnitPrice,
                ProduceName = produce.Name,
                Unit = produce.Unit,
                FarmerName = produce.Farmer?.FullName ?? "Unknown Farmer",
                Status = isNegotiating ? OrderStatus.Negotiating : OrderStatus.Pending,
                OrderDate = DateTime.Now
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            if (isNegotiating)
            {
                var initialOffer = new PriceOffer
                {
                    OrderId = order.OrderId,
                    ProposerId = buyerId,
                    OfferedUnitPrice = vm.OfferedUnitPrice!.Value,
                    Message = vm.OfferMessage,
                    RoundNumber = 1,
                    Status = OfferStatus.Pending,
                    CreatedAt = DateTime.Now,
                    ExpiresAt = DateTime.Now.AddHours(48)
                };

                _context.PriceOffers.Add(initialOffer);
                await _context.SaveChangesAsync();

                // Send email notification to farmer (with price offer) and buyer (confirmation)
                await _emailNotificationService.SendOrderPlacedAsync(order);

                TempData["StatusMessage"] = "Order placed with your price offer! The farmer has 48 hours to accept, reject, or counter.";
                return RedirectToAction(nameof(Negotiate), new { id = order.OrderId });
            }

            // Send email notification to farmer (new order) and buyer (confirmation)
            await _emailNotificationService.SendOrderPlacedAsync(order);

            TempData["StatusMessage"] = "Order placed successfully! The farmer will review it soon.";
            return RedirectToAction(nameof(Confirmation), new { id = order.OrderId });
        }

        // GET: Order/Confirmation/5
        public async Task<IActionResult> Confirmation(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Produce)
                    .ThenInclude(p => p.Category)
                .Include(o => o.Produce)
                    .ThenInclude(p => p.Farmer)
                .Include(o => o.Buyer)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
            {
                return NotFound();
            }

            var currentUserId = _userManager.GetUserId(User);
            bool isBuyer = order.BuyerId == currentUserId;
            bool isFarmer = order.Produce?.FarmerId == currentUserId;
            bool isAdmin = User.IsInRole("Admin");

            // The buyer who placed the order, the farmer who owns the listing, or an Admin can view
            if (!isBuyer && !isFarmer && !isAdmin)
            {
                return Forbid();
            }

            return View(order);
        }

        // GET: Order/Receipt/5
        public async Task<IActionResult> Receipt(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Produce)
                    .ThenInclude(p => p.Category)
                .Include(o => o.Produce)
                    .ThenInclude(p => p.Farmer)
                .Include(o => o.Buyer)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
            {
                return NotFound();
            }

            var currentUserId = _userManager.GetUserId(User);
            bool isBuyer = order.BuyerId == currentUserId;
            bool isFarmer = order.Produce?.FarmerId == currentUserId;
            bool isAdmin = User.IsInRole("Admin");

            // Access rules: buyer sees their own, farmer sees receipts for their own produce, and admins see any
            if (!isBuyer && !isFarmer && !isAdmin)
            {
                return Forbid();
            }

            // A rejected order cannot have a sales receipt or summary
            if (order.Status == OrderStatus.Rejected)
            {
                TempData["StatusMessage"] = $"Receipt is not available because Order #{order.OrderId} was rejected.";
                return RedirectToAction(nameof(Confirmation), new { id = order.OrderId });
            }

            return View(order);
        }

        // GET: Order/Negotiate/5
        public async Task<IActionResult> Negotiate(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Produce)
                    .ThenInclude(p => p.Category)
                .Include(o => o.Produce)
                    .ThenInclude(p => p.Farmer)
                .Include(o => o.Buyer)
                .Include(o => o.PriceOffers)
                    .ThenInclude(po => po.Proposer)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
            {
                return NotFound();
            }

            var currentUserId = _userManager.GetUserId(User);
            bool isBuyer = order.BuyerId == currentUserId;
            bool isFarmer = order.Produce?.FarmerId == currentUserId;
            bool isAdmin = User.IsInRole("Admin");

            if (!isBuyer && !isFarmer && !isAdmin)
            {
                return Forbid();
            }

            // Check if active pending offer has expired (48-hour expiration rule)
            var activeOffer = order.PriceOffers
                .OrderByDescending(po => po.CreatedAt)
                .FirstOrDefault(po => po.Status == OfferStatus.Pending);

            if (activeOffer != null && DateTime.Now > activeOffer.ExpiresAt)
            {
                activeOffer.Status = OfferStatus.Expired;
                order.Status = OrderStatus.Rejected;
                await _context.SaveChangesAsync();
                TempData["StatusMessage"] = "The latest price offer has expired (48-hour limit). Order is now Rejected.";
            }

            // Agmarknet benchmark reference rate
            var benchmark = order.Produce != null ? await _context.MarketPrices
                .Where(mp => order.Produce.Name.Contains(mp.CommodityName) || mp.CommodityName.Contains(order.Produce.Name) || mp.CategoryId == order.Produce.CategoryId)
                .OrderByDescending(mp => mp.Date)
                .FirstOrDefaultAsync() : null;

            ViewBag.MarketBenchmark = benchmark;
            ViewBag.CurrentUserId = currentUserId;
            ViewBag.IsFarmer = isFarmer;
            ViewBag.IsBuyer = isBuyer;

            return View(order);
        }

        // POST: Order/AcceptOffer
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptOffer(int orderId, int offerId)
        {
            var order = await _context.Orders
                .Include(o => o.Produce)
                .Include(o => o.PriceOffers)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null || order.Produce == null) return NotFound();

            var offer = order.PriceOffers.FirstOrDefault(po => po.PriceOfferId == offerId);
            if (offer == null || offer.Status != OfferStatus.Pending)
            {
                TempData["StatusMessage"] = "This offer is no longer active.";
                return RedirectToAction(nameof(Negotiate), new { id = orderId });
            }

            var currentUserId = _userManager.GetUserId(User);
            bool isBuyer = order.BuyerId == currentUserId;
            bool isFarmer = order.Produce?.FarmerId == currentUserId;

            // Only the recipient (other party) can accept the offer
            if (offer.ProposerId == currentUserId || (!isBuyer && !isFarmer))
            {
                return Forbid();
            }

            // Check if expired
            if (DateTime.Now > offer.ExpiresAt)
            {
                offer.Status = OfferStatus.Expired;
                order.Status = OrderStatus.Rejected;
                await _context.SaveChangesAsync();
                TempData["StatusMessage"] = "This offer has expired.";
                return RedirectToAction(nameof(Negotiate), new { id = orderId });
            }

            // Re-check inventory stock at final acceptance
            if (order.QuantityOrdered > order.Produce!.Quantity)
            {
                TempData["StatusMessage"] = $"Cannot accept — only {order.Produce.Quantity} {order.Produce.Unit} remain in stock.";
                return RedirectToAction(nameof(Negotiate), new { id = orderId });
            }

            // Decrement stock upon final agreement
            order.Produce.Quantity -= order.QuantityOrdered;
            if (order.Produce.Quantity <= 0)
            {
                order.Produce.Status = ProduceStatus.SoldOut;
            }

            // Lock agreed price & recalculate total
            order.UnitPrice = offer.OfferedUnitPrice;
            order.TotalPrice = Math.Round(order.QuantityOrdered * order.UnitPrice, 2);

            offer.Status = OfferStatus.Accepted;
            order.Status = OrderStatus.Accepted;

            await _context.SaveChangesAsync();

            // Send notification to both parties that negotiation was accepted & order confirmed
            await _emailNotificationService.SendNegotiationAcceptedAsync(order, offer);

            TempData["StatusMessage"] = $"Price offer of ₹{offer.OfferedUnitPrice:N2} accepted! Order #{order.OrderId} is now Confirmed.";
            return RedirectToAction(nameof(Confirmation), new { id = order.OrderId });
        }

        // POST: Order/CounterOffer
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CounterOffer(int orderId, int previousOfferId, decimal counterPrice, string? message)
        {
            var order = await _context.Orders
                .Include(o => o.Produce)
                    .ThenInclude(p => p.Farmer)
                .Include(o => o.Buyer)
                .Include(o => o.PriceOffers)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null || order.Produce == null) return NotFound();

            var prevOffer = order.PriceOffers.FirstOrDefault(po => po.PriceOfferId == previousOfferId);
            if (prevOffer == null || prevOffer.Status != OfferStatus.Pending)
            {
                TempData["StatusMessage"] = "The previous offer is no longer active.";
                return RedirectToAction(nameof(Negotiate), new { id = orderId });
            }

            var currentUserId = _userManager.GetUserId(User);
            bool isBuyer = order.BuyerId == currentUserId;
            bool isFarmer = order.Produce?.FarmerId == currentUserId;

            if (prevOffer.ProposerId == currentUserId || (!isBuyer && !isFarmer) || string.IsNullOrEmpty(currentUserId))
            {
                return Forbid();
            }

            // Maximum 3 rounds rule
            if (prevOffer.RoundNumber >= 3)
            {
                TempData["StatusMessage"] = "Negotiation reached the maximum limit of 3 rounds. Please accept or decline.";
                return RedirectToAction(nameof(Negotiate), new { id = orderId });
            }

            // 50% price floor rule
            var minFloor = Math.Round(order.Produce!.Price * 0.5m, 2);
            if (counterPrice < minFloor)
            {
                TempData["StatusMessage"] = $"Counter price cannot be lower than 50% of the listing price (minimum ₹{minFloor:N2}).";
                return RedirectToAction(nameof(Negotiate), new { id = orderId });
            }

            // Mark previous offer as countered
            prevOffer.Status = OfferStatus.Countered;

            // Create new round offer
            var newOffer = new PriceOffer
            {
                OrderId = orderId,
                ProposerId = currentUserId,
                OfferedUnitPrice = counterPrice,
                Message = message,
                RoundNumber = prevOffer.RoundNumber + 1,
                Status = OfferStatus.Pending,
                CreatedAt = DateTime.Now,
                ExpiresAt = DateTime.Now.AddHours(48)
            };

            // Update tentative order unit price
            order.UnitPrice = counterPrice;
            order.TotalPrice = Math.Round(order.QuantityOrdered * counterPrice, 2);

            _context.PriceOffers.Add(newOffer);
            await _context.SaveChangesAsync();

            // Send notification to the other party about the counter-offer
            var recipient = isBuyer ? order.Produce.Farmer : order.Buyer;
            var proposer = isBuyer ? order.Buyer : order.Produce.Farmer;
            if (recipient != null && !string.IsNullOrWhiteSpace(recipient.Email))
            {
                await _emailNotificationService.SendNegotiationOfferAsync(
                    order,
                    newOffer,
                    recipient.Email,
                    recipient.FullName,
                    proposer?.FullName ?? (isBuyer ? "Buyer" : "Farmer"),
                    isCounterOffer: true);
            }

            TempData["StatusMessage"] = $"Round {newOffer.RoundNumber} counter-offer of ₹{counterPrice:N2} sent!";
            return RedirectToAction(nameof(Negotiate), new { id = orderId });
        }

        // POST: Order/RejectOffer
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectOffer(int orderId, int offerId)
        {
            var order = await _context.Orders
                .Include(o => o.Produce)
                    .ThenInclude(p => p.Farmer)
                .Include(o => o.Buyer)
                .Include(o => o.PriceOffers)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null) return NotFound();

            var offer = order.PriceOffers.FirstOrDefault(po => po.PriceOfferId == offerId);
            if (offer != null)
            {
                offer.Status = OfferStatus.Rejected;
            }

            order.Status = OrderStatus.Rejected;
            await _context.SaveChangesAsync();

            // Notify buyer that negotiation was declined
            await _emailNotificationService.SendOrderStatusChangedAsync(order, OrderStatus.Rejected);

            TempData["StatusMessage"] = $"Negotiation declined. Order #{order.OrderId} is marked as Rejected.";
            return RedirectToAction(nameof(Confirmation), new { id = order.OrderId });
        }

        // GET: Order  ("My Orders" — Buyer's own order history)
        [Authorize(Roles = "Buyer")]
        public async Task<IActionResult> Index()
        {
            var buyerId = _userManager.GetUserId(User)!;

            var orders = await _context.Orders
                .Include(o => o.Produce)
                    .ThenInclude(p => p.Category)
                .Where(o => o.BuyerId == buyerId)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View(orders);
        }

        // GET: Order/Manage  (orders placed against the Farmer's own listings)
        [Authorize(Roles = "Farmer")]
        public async Task<IActionResult> Manage()
        {
            var farmerId = _userManager.GetUserId(User);

            var orders = await _context.Orders
                .Include(o => o.Produce)
                .Include(o => o.Buyer)
                .Where(o => o.Produce.FarmerId == farmerId)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View(orders);
        }

        // POST: Order/UpdateStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Farmer")]
        public async Task<IActionResult> UpdateStatus(int orderId, OrderStatus newStatus)
        {
            var order = await _context.Orders
                .Include(o => o.Produce)
                    .ThenInclude(p => p.Farmer)
                .Include(o => o.Buyer)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null || order.Produce == null)
            {
                return NotFound();
            }

            var farmerId = _userManager.GetUserId(User);
            if (order.Produce.FarmerId != farmerId)
            {
                return Forbid();
            }

            // Only these transitions are allowed — anything else is rejected,
            // even if someone tampers with the posted newStatus value.
            bool isValidTransition = (order.Status, newStatus) switch
            {
                (OrderStatus.Pending, OrderStatus.Accepted) => true,
                (OrderStatus.Pending, OrderStatus.Rejected) => true,
                (OrderStatus.Negotiating, OrderStatus.Accepted) => true,
                (OrderStatus.Negotiating, OrderStatus.Rejected) => true,
                (OrderStatus.Accepted, OrderStatus.Completed) => true,
                (OrderStatus.Accepted, OrderStatus.Rejected) => true, // rare: cancel after accept
                _ => false
            };

            if (!isValidTransition)
            {
                TempData["StatusMessage"] = $"Cannot change order from {order.Status} to {newStatus}.";
                return RedirectToAction(nameof(Manage));
            }

            if (newStatus == OrderStatus.Accepted)
            {
                // Re-check stock at commit time, not just at placement time —
                // other orders may have been accepted in the meantime.
                if (order.QuantityOrdered > order.Produce.Quantity)
                {
                    TempData["StatusMessage"] =
                        $"Cannot accept — only {order.Produce.Quantity} {order.Produce.Unit} remain for \"{order.Produce.Name}\".";
                    return RedirectToAction(nameof(Manage));
                }

                order.Produce.Quantity -= order.QuantityOrdered;
                if (order.Produce.Quantity <= 0)
                {
                    order.Produce.Status = ProduceStatus.SoldOut;
                }
            }
            else if (newStatus == OrderStatus.Rejected && order.Status == OrderStatus.Accepted)
            {
                // Restore stock since it was already decremented on Accept
                order.Produce.Quantity += order.QuantityOrdered;
                if (order.Produce.Status == ProduceStatus.SoldOut && order.Produce.Quantity > 0)
                {
                    order.Produce.Status = ProduceStatus.Available;
                }
            }

            order.Status = newStatus;
            await _context.SaveChangesAsync();

            // Send email notification to buyer about the order status change
            await _emailNotificationService.SendOrderStatusChangedAsync(order, newStatus);

            TempData["StatusMessage"] = $"Order #{order.OrderId} marked as {newStatus}.";
            return RedirectToAction(nameof(Manage));
        }
    }
}
