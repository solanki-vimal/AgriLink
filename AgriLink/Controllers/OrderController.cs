using AgriLink.Data;
using AgriLink.Models;
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

        public OrderController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
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

            var vm = new OrderCreateViewModel
            {
                ProduceId = produce.ProduceId,
                ProduceName = produce.Name,
                Unit = produce.Unit,
                Price = produce.Price,
                AvailableQuantity = produce.Quantity,
                FarmerName = produce.Farmer?.FullName ?? "Unknown Farmer",
                ImageUrl = produce.ImageUrl
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

            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var buyerId = _userManager.GetUserId(User);

            var order = new Order
            {
                ProduceId = produce.ProduceId,
                BuyerId = buyerId,
                QuantityOrdered = vm.QuantityOrdered,
                // Frozen at order time — produce.Price may change later via Edit
                TotalPrice = vm.QuantityOrdered * produce.Price,
                Status = OrderStatus.Pending,
                OrderDate = DateTime.Now
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            TempData["StatusMessage"] = "Order placed successfully! The farmer will review it soon.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Order  ("My Orders" — Buyer's own order history)
        [Authorize(Roles = "Buyer")]
        public async Task<IActionResult> Index()
        {
            var buyerId = _userManager.GetUserId(User);

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
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
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

            TempData["StatusMessage"] = $"Order #{order.OrderId} marked as {newStatus}.";
            return RedirectToAction(nameof(Manage));
        }
    }
}
