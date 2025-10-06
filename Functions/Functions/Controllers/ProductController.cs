using Functions.Models;
using Functions.Services;
using Microsoft.AspNetCore.Mvc;

namespace Functions.Controllers
{
    public class ProductController : Controller
    {
        private readonly TableStorageService _tableStorageService;
        private readonly BlobStorageService _blobStorageService;

        public ProductController(TableStorageService tableStorageService, BlobStorageService blobStorageService)
        {
            _tableStorageService = tableStorageService;
            _blobStorageService = blobStorageService;
        }
        public async Task<IActionResult> Index()
        {
                var products = await _tableStorageService.GetProductsAsync();
                return View(products);
        }

        [HttpGet]
        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product, IFormFile file)
        {
            product.PartitionKey = "Product";
            product.RowKey = Guid.NewGuid().ToString();

            if (file != null && file.Length > 0)
            {
                using var stream = file.OpenReadStream();
                string blobUrl = await _blobStorageService.UploadBlobAsync(stream, Guid.NewGuid() + Path.GetExtension(file.FileName));
                product.ImageUrl = blobUrl; 
            }

            if (ModelState.IsValid)
            {
                await _tableStorageService.AddProductAsync(product);
                TempData["Success"] = "Product created successfully!";
                return RedirectToAction(nameof(Index));
            }

            return View(product);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string partitionKey, string rowKey)
        {
            if (string.IsNullOrEmpty(partitionKey) || string.IsNullOrEmpty(rowKey))
            {
                TempData["Error"] = "Invalid product identifiers.";
                return RedirectToAction("Index");
            }

            var product = await _tableStorageService.GetProductAsync(partitionKey, rowKey);
            if (product == null)
            {
                TempData["Error"] = "Product not found.";
                return RedirectToAction("Index");
            }

            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Product product, IFormFile file)
        {
            if (ModelState.IsValid)
            {
                if (file != null && file.Length > 0)
                {
                    string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);

                    using (var stream = file.OpenReadStream())
                    {
                        string blobUrl = await _blobStorageService.UploadBlobAsync(stream, fileName);
                        product.ImageUrl = blobUrl; 
                    }
                }

                await _tableStorageService.UpdateProductAsync(product);
                return RedirectToAction(nameof(Index));
            }

            return View(product);
        }

      /*  public async Task<IActionResult> Delete(string partitionKey, string rowKey)
        {
            try
            {
                var product = await _tableStorageService.GetProductAsync(partitionKey, rowKey);
                if (product == null)
                {
                    TempData["Error"] = "Product not found.";
                    return RedirectToAction("Index");
                }

                bool hasOrders = await _tableStorageService.ProductHasOrdersAsync(rowKey);
                if (hasOrders)
                {
                    TempData["Error"] = "Cannot delete this product because it is associated with existing orders.";
                    return RedirectToAction("Index");
                }

                if (!string.IsNullOrEmpty(product.ImageUrl))
                {
                    await _blobStorageService.DeleteBlobAsync(product.ImageUrl);
                }
                await _tableStorageService.DeleteProductAsync(partitionKey, rowKey);

                TempData["Success"] = "Product deleted successfully.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed to delete product: {ex.Message}";
                return RedirectToAction("Index");
            }
        }
      */

        [HttpGet]
        public async Task<IActionResult> Details(string partitionKey, string rowKey)
        {
            if (string.IsNullOrEmpty(partitionKey) || string.IsNullOrEmpty(rowKey))
            {
                TempData["Error"] = "Invalid product identifiers.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var product = await _tableStorageService.GetProductAsync(partitionKey, rowKey);
                if (product == null)
                {
                    TempData["Error"] = "Product not found.";
                    return RedirectToAction(nameof(Index));
                }

                return View(product);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed to load product details: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
