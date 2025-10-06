using Azure;
using Azure.Data.Tables;
using Functions.Models;
using Functions.Services;
using Microsoft.AspNetCore.Mvc;

namespace Functions.Controllers
{
    public class CustomerController : Controller
    {
        private readonly TableStorageService _tableStorageService;
        private readonly FileStorageService _fileStorageService;

        public CustomerController(TableStorageService tableStorageService, FileStorageService fileStorageService)
        {
            _tableStorageService = tableStorageService;
            _fileStorageService = fileStorageService;
        }
        public async Task<IActionResult> Index()
        {
            var customers = await _tableStorageService.GetCustomersAsync();
            return View(customers);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Customer customer)
        {
            try
            {
                var customers = await _tableStorageService.GetCustomersAsync();

                if (customers.Any(c => c.Email?.ToLower() == customer.Email?.ToLower()))
                {
                    ModelState.AddModelError("Email", "This email is already registered.");
                }

                if (customers.Any(c => c.Phone == customer.Phone))
                {
                    ModelState.AddModelError("Phone", "This phone number is already registered.");
                }

                if (!ModelState.IsValid)
                {
                    return View(customer); 
                }

                customer.PartitionKey = "Customer";
                customer.RowKey = Guid.NewGuid().ToString();

                await _tableStorageService.AddCustomerAsync(customer);
                TempData["Success"] = "Customer created successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error creating customer: {ex.Message}";
                return View(customer);
            }
        }
        public async Task<IActionResult> Delete(string partitionKey, string rowKey)
        {
            try
            {
                var customer = await _tableStorageService.GetCustomerAsync(partitionKey, rowKey);
                if (customer == null)
                {
                    TempData["Error"] = "Customer not found.";
                    return RedirectToAction(nameof(Index));
                }

                bool hasOrders = await _tableStorageService.CustomerHasOrdersAsync(rowKey);
                if (hasOrders)
                {
                    TempData["Error"] = "Cannot delete this customer because they have associated orders.";
                    return RedirectToAction(nameof(Index));
                }

                await _tableStorageService.DeleteCustomerAsync(partitionKey, rowKey);
                TempData["Success"] = "Customer deleted successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed to delete customer: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string partitionKey, string rowKey)
        {
            if (string.IsNullOrEmpty(partitionKey) || string.IsNullOrEmpty(rowKey))
            {
                TempData["Error"] = "Invalid customer identifiers.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var customer = await _tableStorageService.GetCustomerAsync(partitionKey, rowKey);
                if (customer == null)
                {
                    TempData["Error"] = "Customer not found.";
                    return RedirectToAction(nameof(Index));
                }

                return View(customer);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error retrieving customer: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Customer customer)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return View(customer);
                }

                if (string.IsNullOrEmpty(customer.PartitionKey) || string.IsNullOrEmpty(customer.RowKey))
                {
                    TempData["Error"] = "Invalid customer identifiers.";
                    return RedirectToAction(nameof(Index));
                }

                customer.ETag = Azure.ETag.All;

                await _tableStorageService.UpdateCustomerAsync(customer);
                TempData["Success"] = "Customer updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error updating customer: {ex.Message}";
                return View(customer);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Details(string partitionKey, string rowKey)
        {
            if (string.IsNullOrEmpty(partitionKey) || string.IsNullOrEmpty(rowKey))
            {
                TempData["Error"] = "Invalid customer identifiers.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var customer = await _tableStorageService.GetCustomerAsync(partitionKey, rowKey);
                if (customer == null)
                {
                    TempData["Error"] = "Customer not found.";
                    return RedirectToAction(nameof(Index));
                }

                return View(customer);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error retrieving customer details: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        public async Task<IActionResult> Files()
        {
            List<FileModel> files;
            try
            {
                files = await _fileStorageService.ListFileAsync("uploads");
            }
            catch (Exception ex)
            {

                TempData["Error"] = $"Failed to load files : {ex.Message}";
                files = new List<FileModel>();
            }
            return View(files);
        }

        [HttpPost]
        public async Task<IActionResult> UploadFile(IFormFile file, string orderId)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please select a file to upload";
                return RedirectToAction("Create", "Customer", new { id = orderId });
            }

            try
            {
                using (var stream = file.OpenReadStream())
                {
                    string directoryName = "uploads";
                    string fileName = file.FileName;
                    await _fileStorageService.UploadFileAsync(directoryName, fileName, stream);
                }
                TempData["Success"] = $"File '{file.FileName}' uploaded successfully";
            }
            catch (Exception e)
            {
                TempData["Error"] = $"File upload failed: {e.Message}";
            }

            return RedirectToAction("Create", "Customer", new { id = orderId });
        }

        [HttpGet]
        public async Task<IActionResult> DownloadFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return BadRequest("File name cannot be null or empty");
            }
            try
            {
                var fileStream = await _fileStorageService.DownloadFileAsync("uploads", fileName);
                if (fileStream == null)
                {
                    return NotFound($"File '{fileName}' not found");
                }
                return File(fileStream, "application/octet-stream", fileName);
            }
            catch (Exception e)
            {
                return BadRequest($"Error downloading file: {e.Message}");
            }
        }

        [HttpGet]
        public async Task<IActionResult> FilesDownload()
        {
            try
            {
                string directoryName = "uploads";
                var files = await _fileStorageService.ListFileAsync(directoryName);

                return View(files); 
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed to load files: {ex.Message}";
                return View(new List<FileModel>()); 
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                TempData["Error"] = "File name cannot be empty.";
                return RedirectToAction("FilesDownload");
            }

            try
            {
                await _fileStorageService.DeleteFileAsync("uploads", fileName);
                TempData["Success"] = $"File '{fileName}' deleted successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error deleting file: {ex.Message}";
            }

            return RedirectToAction("FilesDownload");
        }


    }
}
