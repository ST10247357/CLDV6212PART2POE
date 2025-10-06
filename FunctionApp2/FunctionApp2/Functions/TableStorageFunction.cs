using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Azure.Data.Tables;
using Azure;
using FunctionApp2.Models;

namespace FunctionApp2
{
    public class TableStorageFunction
    {
        private readonly TableClient _customerTable;
        private readonly TableClient _productTable;
        private readonly TableClient _orderTable;
        private readonly ILogger<TableStorageFunction> _logger;

        public TableStorageFunction(ILogger<TableStorageFunction> logger)
        {
            var connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            _customerTable = new TableClient(connectionString, "Customer");
            _productTable = new TableClient(connectionString, "Product");
            _orderTable = new TableClient(connectionString, "Order");
            _logger = logger;
        }

        // Customer Functions
        [Function("GetCustomers")]
        public async Task<HttpResponseData> GetCustomers(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "customers")] HttpRequestData req)
        {
            var response = req.CreateResponse();

            try
            {
                _logger.LogInformation("GetCustomers function started");

                var customers = new List<Customer>();
                await foreach (var customer in _customerTable.QueryAsync<Customer>())
                {
                    customers.Add(customer);
                }

                _logger.LogInformation($"Retrieved {customers.Count} customers");

                response.StatusCode = System.Net.HttpStatusCode.OK;
                var json = JsonSerializer.Serialize(customers);
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customers");
                response.StatusCode = System.Net.HttpStatusCode.InternalServerError;
                var errorJson = JsonSerializer.Serialize(new { error = ex.Message });
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(errorJson);
            }

            return response;
        }

        [Function("GetCustomer")]
        public async Task<HttpResponseData> GetCustomer(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "customers/{partitionKey}/{rowKey}")] HttpRequestData req,
            string partitionKey, string rowKey)
        {
            var response = req.CreateResponse();

            try
            {
                var customerResponse = await _customerTable.GetEntityAsync<Customer>(partitionKey, rowKey);
                var customer = customerResponse.Value;

                if (customer == null)
                {
                    response.StatusCode = System.Net.HttpStatusCode.NotFound;
                    var errorJson = JsonSerializer.Serialize(new { error = "Customer not found" });
                    response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                    await response.WriteStringAsync(errorJson);
                }
                else
                {
                    response.StatusCode = System.Net.HttpStatusCode.OK;
                    var json = JsonSerializer.Serialize(customer);
                    response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                    await response.WriteStringAsync(json);
                }
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                response.StatusCode = System.Net.HttpStatusCode.NotFound;
                var errorJson = JsonSerializer.Serialize(new { error = "Customer not found" });
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(errorJson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customer");
                response.StatusCode = System.Net.HttpStatusCode.BadRequest;
                var errorJson = JsonSerializer.Serialize(new { error = ex.Message });
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(errorJson);
            }

            return response;
        }

        [Function("CreateCustomer")]
        public async Task<HttpResponseData> CreateCustomer(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "customers")] HttpRequestData req)
        {
            var response = req.CreateResponse();

            try
            {
                var body = await new StreamReader(req.Body).ReadToEndAsync();
                var customer = JsonSerializer.Deserialize<Customer>(body);

                customer.PartitionKey ??= "Customer";
                customer.RowKey ??= Guid.NewGuid().ToString();

                await _customerTable.AddEntityAsync(customer);
                _logger.LogInformation($"Customer created: {customer.RowKey}");

                response.StatusCode = System.Net.HttpStatusCode.OK;
                var json = JsonSerializer.Serialize(new
                {
                    message = "Customer created successfully",
                    id = customer.RowKey
                });
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating customer");
                response.StatusCode = System.Net.HttpStatusCode.BadRequest;
                var errorJson = JsonSerializer.Serialize(new { error = ex.Message });
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(errorJson);
            }

            return response;
        }

        [Function("UpdateCustomer")]
        public async Task<HttpResponseData> UpdateCustomer(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "customers")] HttpRequestData req)
        {
            var response = req.CreateResponse();

            try
            {
                var body = await new StreamReader(req.Body).ReadToEndAsync();
                var customer = JsonSerializer.Deserialize<Customer>(body);

                await _customerTable.UpdateEntityAsync(customer, ETag.All, TableUpdateMode.Replace);
                _logger.LogInformation($"Customer updated: {customer.RowKey}");

                response.StatusCode = System.Net.HttpStatusCode.OK;
                var json = JsonSerializer.Serialize(new { message = "Customer updated successfully" });
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating customer");
                response.StatusCode = System.Net.HttpStatusCode.BadRequest;
                var errorJson = JsonSerializer.Serialize(new { error = ex.Message });
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(errorJson);
            }

            return response;
        }

        [Function("DeleteCustomer")]
        public async Task<HttpResponseData> DeleteCustomer(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "customers/{partitionKey}/{rowKey}")] HttpRequestData req,
            string partitionKey, string rowKey)
        {
            var response = req.CreateResponse();

            try
            {
                _logger.LogInformation($"Checking if customer {rowKey} has orders...");

                bool hasOrders = false;
                await foreach (var order in _orderTable.QueryAsync<Order>(o => o.CustomerRowKey == rowKey))
                {
                    hasOrders = true;
                    break;
                }

                if (hasOrders)
                {
                    _logger.LogInformation($"Customer {rowKey} has associated orders, cannot delete");
                    response.StatusCode = System.Net.HttpStatusCode.BadRequest;
                    var errorJson = JsonSerializer.Serialize(new { error = "Cannot delete customer because they have associated orders" });
                    response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                    await response.WriteStringAsync(errorJson);
                    return response;
                }

                _logger.LogInformation($"Deleting customer: {partitionKey}/{rowKey}");
                await _customerTable.DeleteEntityAsync(partitionKey, rowKey);
                _logger.LogInformation($"Customer deleted: {rowKey}");

                response.StatusCode = System.Net.HttpStatusCode.OK;
                var json = JsonSerializer.Serialize(new { message = "Customer deleted successfully" });
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting customer");
                response.StatusCode = System.Net.HttpStatusCode.BadRequest;
                var errorJson = JsonSerializer.Serialize(new { error = ex.Message });
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(errorJson);
            }

            return response;
        }

        [Function("CustomerHasOrders")]
        public async Task<HttpResponseData> CustomerHasOrders(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "customers/{customerRowKey}/hasorders")] HttpRequestData req,
            string customerRowKey)
        {
            var response = req.CreateResponse();

            try
            {
                bool hasOrders = false;
                await foreach (var order in _orderTable.QueryAsync<Order>(o => o.CustomerRowKey == customerRowKey))
                {
                    hasOrders = true;
                    break;
                }

                response.StatusCode = System.Net.HttpStatusCode.OK;
                var json = JsonSerializer.Serialize(new { hasOrders = hasOrders });
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking customer orders");
                response.StatusCode = System.Net.HttpStatusCode.BadRequest;
                var errorJson = JsonSerializer.Serialize(new { error = ex.Message });
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(errorJson);
            }

            return response;
        }

        // Product Functions
        [Function("GetProducts")]
        public async Task<HttpResponseData> GetProducts(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "products")] HttpRequestData req)
        {
            var response = req.CreateResponse();

            try
            {
                _logger.LogInformation("GetProducts function started");

                var products = new List<Product>();
                await foreach (var product in _productTable.QueryAsync<Product>())
                {
                    products.Add(product);
                }

                _logger.LogInformation($"Retrieved {products.Count} products");

                response.StatusCode = System.Net.HttpStatusCode.OK;
                var json = JsonSerializer.Serialize(products);
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving products");
                response.StatusCode = System.Net.HttpStatusCode.InternalServerError;
                var errorJson = JsonSerializer.Serialize(new { error = ex.Message });
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(errorJson);
            }

            return response;
        }

        [Function("CreateProduct")]
        public async Task<HttpResponseData> CreateProduct(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "products")] HttpRequestData req)
        {
            var response = req.CreateResponse();

            try
            {
                var body = await new StreamReader(req.Body).ReadToEndAsync();
                var product = JsonSerializer.Deserialize<Product>(body);

                product.PartitionKey ??= "Product";
                product.RowKey ??= Guid.NewGuid().ToString();

                await _productTable.AddEntityAsync(product);
                _logger.LogInformation($"Product created: {product.RowKey}");

                response.StatusCode = System.Net.HttpStatusCode.OK;
                var json = JsonSerializer.Serialize(new
                {
                    message = "Product created successfully",
                    id = product.RowKey
                });
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating product");
                response.StatusCode = System.Net.HttpStatusCode.BadRequest;
                var errorJson = JsonSerializer.Serialize(new { error = ex.Message });
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(errorJson);
            }

            return response;
        }

        [Function("GetProduct")]
        public async Task<HttpResponseData> GetProduct(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "products/{partitionKey}/{rowKey}")] HttpRequestData req,
            string partitionKey, string rowKey)
        {
            var response = req.CreateResponse();

            try
            {
                var productResponse = await _productTable.GetEntityAsync<Product>(partitionKey, rowKey);
                var product = productResponse.Value;

                if (product == null)
                {
                    response.StatusCode = System.Net.HttpStatusCode.NotFound;
                    var errorJson = JsonSerializer.Serialize(new { error = "Product not found" });
                    response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                    await response.WriteStringAsync(errorJson);
                }
                else
                {
                    response.StatusCode = System.Net.HttpStatusCode.OK;
                    var json = JsonSerializer.Serialize(product);
                    response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                    await response.WriteStringAsync(json);
                }
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                response.StatusCode = System.Net.HttpStatusCode.NotFound;
                var errorJson = JsonSerializer.Serialize(new { error = "Product not found" });
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(errorJson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving product");
                response.StatusCode = System.Net.HttpStatusCode.BadRequest;
                var errorJson = JsonSerializer.Serialize(new { error = ex.Message });
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(errorJson);
            }

            return response;
        }

        [Function("UpdateProduct")]
        public async Task<HttpResponseData> UpdateProduct(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "products")] HttpRequestData req)
        {
            var response = req.CreateResponse();

            try
            {
                var body = await new StreamReader(req.Body).ReadToEndAsync();
                var product = JsonSerializer.Deserialize<Product>(body);

                await _productTable.UpdateEntityAsync(product, ETag.All, TableUpdateMode.Replace);
                _logger.LogInformation($"Product updated: {product.RowKey}");

                response.StatusCode = System.Net.HttpStatusCode.OK;
                var json = JsonSerializer.Serialize(new { message = "Product updated successfully" });
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating product");
                response.StatusCode = System.Net.HttpStatusCode.BadRequest;
                var errorJson = JsonSerializer.Serialize(new { error = ex.Message });
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(errorJson);
            }

            return response;
        }

        [Function("DeleteProduct")]
        public async Task<HttpResponseData> DeleteProduct(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "products/{partitionKey}/{rowKey}")] HttpRequestData req,
            string partitionKey, string rowKey)
        {
            var response = req.CreateResponse();

            try
            {
                _logger.LogInformation($"Checking if product {rowKey} has orders...");

                bool hasOrders = false;
                await foreach (var order in _orderTable.QueryAsync<Order>(o => o.ProductRowKey == rowKey))
                {
                    hasOrders = true;
                    break;
                }

                if (hasOrders)
                {
                    _logger.LogInformation($"Product {rowKey} has associated orders, cannot delete");
                    response.StatusCode = System.Net.HttpStatusCode.BadRequest;
                    var errorJson = JsonSerializer.Serialize(new { error = "Cannot delete product because it is associated with existing orders" });
                    response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                    await response.WriteStringAsync(errorJson);
                    return response;
                }

                _logger.LogInformation($"Deleting product: {partitionKey}/{rowKey}");
                await _productTable.DeleteEntityAsync(partitionKey, rowKey);
                _logger.LogInformation($"Product deleted: {rowKey}");

                response.StatusCode = System.Net.HttpStatusCode.OK;
                var json = JsonSerializer.Serialize(new { message = "Product deleted successfully" });
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting product");
                response.StatusCode = System.Net.HttpStatusCode.BadRequest;
                var errorJson = JsonSerializer.Serialize(new { error = ex.Message });
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(errorJson);
            }

            return response;
        }

        [Function("ProductHasOrders")]
        public async Task<HttpResponseData> ProductHasOrders(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "products/{productRowKey}/hasorders")] HttpRequestData req,
            string productRowKey)
        {
            var response = req.CreateResponse();

            try
            {
                bool hasOrders = false;
                await foreach (var order in _orderTable.QueryAsync<Order>(o => o.ProductRowKey == productRowKey))
                {
                    hasOrders = true;
                    break;
                }

                response.StatusCode = System.Net.HttpStatusCode.OK;
                var json = JsonSerializer.Serialize(new { hasOrders = hasOrders });
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking product orders");
                response.StatusCode = System.Net.HttpStatusCode.BadRequest;
                var errorJson = JsonSerializer.Serialize(new { error = ex.Message });
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(errorJson);
            }

            return response;
        }

        // Order Functions
        [Function("GetOrders")]
        public async Task<HttpResponseData> GetOrders(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "orders")] HttpRequestData req)
        {
            var response = req.CreateResponse();

            try
            {
                _logger.LogInformation("GetOrders function started");
                var orders = new List<Order>();
                await foreach (var order in _orderTable.QueryAsync<Order>())
                {
                    orders.Add(order);
                }

                _logger.LogInformation($"Retrieved {orders.Count} orders from table storage");

                response.StatusCode = System.Net.HttpStatusCode.OK;
                var json = JsonSerializer.Serialize(orders);
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving orders");
                response.StatusCode = System.Net.HttpStatusCode.BadRequest;
                var errorJson = JsonSerializer.Serialize(new { error = ex.Message });
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(errorJson);
            }

            return response;
        }

        [Function("GetOrder")]
        public async Task<HttpResponseData> GetOrder(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "orders/{partitionKey}/{rowKey}")] HttpRequestData req,
            string partitionKey, string rowKey)
        {
            var response = req.CreateResponse();

            try
            {
                var orderResponse = await _orderTable.GetEntityAsync<Order>(partitionKey, rowKey);
                var order = orderResponse.Value;

                if (order == null)
                {
                    response.StatusCode = System.Net.HttpStatusCode.NotFound;
                    var errorJson = JsonSerializer.Serialize(new { error = "Order not found" });
                    response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                    await response.WriteStringAsync(errorJson);
                }
                else
                {
                    response.StatusCode = System.Net.HttpStatusCode.OK;
                    var json = JsonSerializer.Serialize(order);
                    response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                    await response.WriteStringAsync(json);
                }
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                response.StatusCode = System.Net.HttpStatusCode.NotFound;
                var errorJson = JsonSerializer.Serialize(new { error = "Order not found" });
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(errorJson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving order");
                response.StatusCode = System.Net.HttpStatusCode.BadRequest;
                var errorJson = JsonSerializer.Serialize(new { error = ex.Message });
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(errorJson);
            }

            return response;
        }

        [Function("CreateOrder")]
        public async Task<HttpResponseData> CreateOrder(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orders")] HttpRequestData req)
        {
            var response = req.CreateResponse();

            try
            {
                var body = await new StreamReader(req.Body).ReadToEndAsync();
                var order = JsonSerializer.Deserialize<Order>(body);

                order.PartitionKey ??= "Order";
                order.RowKey ??= Guid.NewGuid().ToString();
                order.OrderDate = DateTime.UtcNow;

                await _orderTable.AddEntityAsync(order);
                _logger.LogInformation($"Order created: {order.RowKey}");

                response.StatusCode = System.Net.HttpStatusCode.OK;
                var json = JsonSerializer.Serialize(new
                {
                    message = "Order created successfully",
                    id = order.RowKey
                });
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating order");
                response.StatusCode = System.Net.HttpStatusCode.BadRequest;
                var errorJson = JsonSerializer.Serialize(new { error = ex.Message });
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(errorJson);
            }

            return response;
        }

        [Function("UpdateOrder")]
        public async Task<HttpResponseData> UpdateOrder(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "orders")] HttpRequestData req)
        {
            var response = req.CreateResponse();

            try
            {
                var body = await new StreamReader(req.Body).ReadToEndAsync();
                var order = JsonSerializer.Deserialize<Order>(body);

                _logger.LogInformation($"Updating order: {order.RowKey}");

                await _orderTable.UpdateEntityAsync(order, ETag.All, TableUpdateMode.Replace);
                _logger.LogInformation($"Order updated: {order.RowKey}");

                response.StatusCode = System.Net.HttpStatusCode.OK;
                var json = JsonSerializer.Serialize(new { message = "Order updated successfully" });
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating order");
                response.StatusCode = System.Net.HttpStatusCode.BadRequest;
                var errorJson = JsonSerializer.Serialize(new { error = ex.Message });
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(errorJson);
            }

            return response;
        }

        [Function("DeleteOrder")]
        public async Task<HttpResponseData> DeleteOrder(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "orders/{partitionKey}/{rowKey}")] HttpRequestData req,
            string partitionKey, string rowKey)
        {
            var response = req.CreateResponse();

            try
            {
                await _orderTable.DeleteEntityAsync(partitionKey, rowKey);
                _logger.LogInformation($"Order deleted: {rowKey}");

                var json = JsonSerializer.Serialize(new { message = "Order deleted successfully" });
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(json);
                response.StatusCode = System.Net.HttpStatusCode.OK;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting order");
                var errorJson = JsonSerializer.Serialize(new { error = ex.Message });
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(errorJson);
                response.StatusCode = System.Net.HttpStatusCode.BadRequest;
            }

            return response;
        }
    }
}