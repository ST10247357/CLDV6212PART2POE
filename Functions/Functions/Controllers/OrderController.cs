using Functions.Models;
using Functions.Services;
using Microsoft.AspNetCore.Mvc;

namespace Functions.Controllers
{
    public class OrderController : Controller
    {
        private readonly TableStorageService _tableStorageService;
        private readonly QueueStorageService _queueStorageService;

        public OrderController(TableStorageService tableStorageService, QueueStorageService queueStorageService)
        {
            _tableStorageService = tableStorageService;
            _queueStorageService = queueStorageService;
        }
        public async Task<IActionResult> Index()
        {
            try
            {
                var orders = await _tableStorageService.GetOrdersAsync();
                return View(orders);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed to load orders: {ex.Message}";
                return View(new List<Order>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            try
            {
                ViewBag.Customers = await _tableStorageService.GetCustomersAsync();
                ViewBag.Products = await _tableStorageService.GetProductsAsync();
                return View();
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed to load create order form: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create(Order order)
        {
            try
            {
                order.PartitionKey = "Order";
                order.RowKey = Guid.NewGuid().ToString();
                order.OrderDate = DateTime.UtcNow;

                var customer = await _tableStorageService.GetCustomerAsync("Customer", order.CustomerRowKey);
                if (customer != null)
                order.CustomerName = customer.Customer_Name;
                order.CustomerEmail = customer.Email;

                var product = await _tableStorageService.GetProductAsync("Product", order.ProductRowKey);
                if (product != null)
                {
                    order.ProductName = product.Name;
                    order.UnitPrice = product.Price;
                }

                order.TotalPrice = order.Quantity * order.UnitPrice;

                await _tableStorageService.AddOrderAsync(order);

                string message = $"New order by customer {order.CustomerName} of the product {order.ProductName}";
                await _queueStorageService.SendMessage(message);

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed to create order: {ex.Message}";
                ViewBag.Customers = await _tableStorageService.GetCustomersAsync();
                ViewBag.Products = await _tableStorageService.GetProductsAsync();
                return View(order);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string partitionKey, string rowKey)
        {
            try
            {
                var order = await _tableStorageService.GetOrderAsync(partitionKey, rowKey);
                if (order == null)
                    return NotFound();

                ViewBag.Customers = await _tableStorageService.GetCustomersAsync();
                ViewBag.Products = await _tableStorageService.GetProductsAsync();
                return View(order);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed to load order: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Order order)
        {
            try
            {

                var existingOrder = await _tableStorageService.GetOrderAsync(order.PartitionKey, order.RowKey);
                if (existingOrder == null)
                    return NotFound();

                order.CustomerEmail = existingOrder.CustomerEmail;
                order.OrderDate = existingOrder.OrderDate;

                var customer = await _tableStorageService.GetCustomerAsync("Customer", order.CustomerRowKey);
                if (customer != null)
                    order.CustomerName = customer.Customer_Name;

                var product = await _tableStorageService.GetProductAsync("Product", order.ProductRowKey);
                if (product != null)
                {
                    order.ProductName = product.Name;
                    order.UnitPrice = product.Price;
                }

                order.TotalPrice = order.Quantity * order.UnitPrice;

                await _tableStorageService.UpdateOrderAsync(order);

                TempData["Success"] = "Order updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed to update order: {ex.Message}";
                ViewBag.Customers = await _tableStorageService.GetCustomersAsync();
                ViewBag.Products = await _tableStorageService.GetProductsAsync();
                return View(order);
            }
        }

        public async Task<IActionResult> Delete(string partitionKey, string rowKey)
        {
            try
            {
                var order = await _tableStorageService.GetOrderAsync(partitionKey, rowKey);
                if (order == null)
                {
                    TempData["Error"] = "Order not found.";
                    return RedirectToAction(nameof(Index));
                }

                await _tableStorageService.DeleteOrderAsync(partitionKey, rowKey);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed to delete order: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpGet]
        public async Task<IActionResult> Details(string partitionKey, string rowKey)
        {
            if (string.IsNullOrEmpty(partitionKey) || string.IsNullOrEmpty(rowKey))
            {
                TempData["Error"] = "Invalid order identifiers.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var order = await _tableStorageService.GetOrderAsync(partitionKey, rowKey);
                if (order == null)
                {
                    TempData["Error"] = "Order not found.";
                    return RedirectToAction(nameof(Index));
                }

                return View(order);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed to load order details: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

    }
}
