using Functions.Models;
using System.Text;
using System.Text.Json;

namespace Functions.Services
{
    public class TableStorageService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<TableStorageService> _logger;

        public TableStorageService(IHttpClientFactory httpClientFactory, ILogger<TableStorageService> logger)
        {
            _httpClient = httpClientFactory.CreateClient("AzureFunctionsClient");
            _logger = logger;
        }

        // Customer Methods
        public async Task<List<Customer>> GetCustomersAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("customers");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var customers = JsonSerializer.Deserialize<List<Customer>>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    return customers ?? new List<Customer>();
                }
                return new List<Customer>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting customers from function");
                return new List<Customer>();
            }
        }

        public async Task<Customer?> GetCustomerAsync(string partitionKey, string rowKey)
        {
            try
            {
                var response = await _httpClient.GetAsync($"customers/{partitionKey}/{rowKey}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<Customer>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting customer from function");
                return null;
            }
        }

        public async Task AddCustomerAsync(Customer customer)
        {
            try
            {
                var customerJson = JsonSerializer.Serialize(customer);
                var content = new StringContent(customerJson, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("customers", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new InvalidOperationException($"Error adding customer: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding customer via function");
                throw new InvalidOperationException($"Error adding customer: {ex.Message}");
            }
        }

        public async Task UpdateCustomerAsync(Customer customer)
        {
            try
            {
                var customerJson = JsonSerializer.Serialize(customer);
                var content = new StringContent(customerJson, Encoding.UTF8, "application/json");
                var response = await _httpClient.PutAsync("customers", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new InvalidOperationException($"Error updating customer: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating customer via function");
                throw new InvalidOperationException($"Error updating customer: {ex.Message}");
            }
        }

        public async Task DeleteCustomerAsync(string partitionKey, string rowKey)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"customers/{partitionKey}/{rowKey}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new InvalidOperationException($"Error deleting customer: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting customer via function");
                throw new InvalidOperationException($"Error deleting customer: {ex.Message}");
            }
        }

        public async Task<bool> CustomerHasOrdersAsync(string customerRowKey)
        {
            try
            {
                var response = await _httpClient.GetAsync($"customers/{customerRowKey}/hasorders");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<Dictionary<string, bool>>(json);
                    return result?.ContainsKey("hasOrders") == true && result["hasOrders"];
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if customer has orders via function");
                return false;
            }
        }

        // Product Methods
        public async Task<List<Product>> GetProductsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("products");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var products = JsonSerializer.Deserialize<List<Product>>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    return products ?? new List<Product>();
                }
                return new List<Product>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting products from function");
                return new List<Product>();
            }
        }

        public async Task<Product?> GetProductAsync(string partitionKey, string rowKey)
        {
            try
            {
                var response = await _httpClient.GetAsync($"products/{partitionKey}/{rowKey}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<Product>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting product from function");
                return null;
            }
        }

        public async Task AddProductAsync(Product product)
        {
            try
            {
                var productJson = JsonSerializer.Serialize(product);
                var content = new StringContent(productJson, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("products", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new InvalidOperationException($"Error adding product: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding product via function");
                throw new InvalidOperationException($"Error adding product: {ex.Message}");
            }
        }

        public async Task UpdateProductAsync(Product product)
        {
            try
            {
                var productJson = JsonSerializer.Serialize(product);
                var content = new StringContent(productJson, Encoding.UTF8, "application/json");
                var response = await _httpClient.PutAsync("products", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new InvalidOperationException($"Error updating product: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating product via function");
                throw new InvalidOperationException($"Error updating product: {ex.Message}");
            }
        }

        public async Task DeleteProductAsync(string partitionKey, string rowKey)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"products/{partitionKey}/{rowKey}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new InvalidOperationException($"Error deleting product: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting product via function");
                throw new InvalidOperationException($"Error deleting product: {ex.Message}");
            }
        }

        public async Task<bool> ProductHasOrdersAsync(string productRowKey)
        {
            try
            {
                var response = await _httpClient.GetAsync($"products/{productRowKey}/hasorders");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<Dictionary<string, bool>>(json);
                    return result?.ContainsKey("hasOrders") == true && result["hasOrders"];
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if product has orders via function");
                return false;
            }
        }

        // Order Methods
        public async Task<List<Order>> GetOrdersAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("orders");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var orders = JsonSerializer.Deserialize<List<Order>>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    return orders ?? new List<Order>();
                }
                return new List<Order>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting orders from function");
                return new List<Order>();
            }
        }

        public async Task<Order?> GetOrderAsync(string partitionKey, string rowKey)
        {
            try
            {
                var response = await _httpClient.GetAsync($"orders/{partitionKey}/{rowKey}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<Order>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting order from function");
                return null;
            }
        }

        public async Task AddOrderAsync(Order order)
        {
            try
            {
                var orderJson = JsonSerializer.Serialize(order);
                var content = new StringContent(orderJson, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("orders", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new InvalidOperationException($"Error adding order: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding order via function");
                throw new InvalidOperationException($"Error adding order: {ex.Message}");
            }
        }

        public async Task UpdateOrderAsync(Order order)
        {
            try
            {
                var orderJson = JsonSerializer.Serialize(order);
                var content = new StringContent(orderJson, Encoding.UTF8, "application/json");
                var response = await _httpClient.PutAsync("orders", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new InvalidOperationException($"Error updating order: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating order via function");
                throw new InvalidOperationException($"Error updating order: {ex.Message}");
            }
        }

        public async Task DeleteOrderAsync(string partitionKey, string rowKey)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"orders/{partitionKey}/{rowKey}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new InvalidOperationException($"Error deleting order: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting order via function");
                throw new InvalidOperationException($"Error deleting order: {ex.Message}");
            }
        }
    }
}