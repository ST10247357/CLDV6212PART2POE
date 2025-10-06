using Azure.Data.Tables;
using Azure.Storage.Queues;
using FunctionApp2.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FunctionApp2.Functions
{
    public class OrderQueueFunction
    {
        private readonly ILogger<OrderQueueFunction> _logger;
        private readonly QueueClient _queueClient;
        private readonly TableClient _orderTable;

        public OrderQueueFunction(ILogger<OrderQueueFunction> logger)
        {
            var connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");

            _queueClient = new QueueClient(connectionString, "order");
            _orderTable = new TableClient(connectionString, "Order");
            _logger = logger;
        }

        [Function("ProcessOrder")]
        public async Task ProcessOrder([QueueTrigger("order", Connection = "AzureWebJobsStorage")] string messageText)
        {
            _logger.LogInformation($"Queue Triggered!");
            await _orderTable.CreateIfNotExistsAsync();

            var order = JsonSerializer.Deserialize<Order>(messageText);
            if (order == null)
            {
                _logger.LogError("Failed to deserialize");
                return;
            }

            order.RowKey = Guid.NewGuid().ToString();
            order.PartitionKey = "Order";

            _logger.LogInformation($"Saving entity with rowkey: {order.RowKey}");

            await _orderTable.AddEntityAsync(order);
            _logger.LogInformation("Successfully saved order to table.");
        }

        [Function("QueueOrder")]
        public async Task<HttpResponseData> QueueOrder(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            _logger.LogInformation("QueueOrder HTTP function triggered");

            try
            {
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                _logger.LogInformation($"Received order: {requestBody}");

                await _queueClient.CreateIfNotExistsAsync();

                var response = await _queueClient.SendMessageAsync(requestBody);

                _logger.LogInformation("Order successfully queued");

                var httpResponse = req.CreateResponse(System.Net.HttpStatusCode.OK);
                await httpResponse.WriteStringAsync("Order queued successfully");
                return httpResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error queuing order");
                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Error processing order");
                return errorResponse;
            }
        }
    }
}
