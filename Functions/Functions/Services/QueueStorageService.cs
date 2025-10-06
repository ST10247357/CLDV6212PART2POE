using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text;

namespace Functions.Services
{
    public class QueueStorageService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<QueueStorageService> _logger;

        public QueueStorageService(IHttpClientFactory httpClientFactory, ILogger<QueueStorageService> logger)
        {
            _httpClient = httpClientFactory.CreateClient("AzureFunctionsClient");
            _logger = logger;
        }

        public async Task SendMessage(string message)
        {
            try
            {
                var content = new StringContent(message, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("QueueOrder", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new InvalidOperationException($"Error sending message to queue function: {errorContent}");
                }

                _logger.LogInformation("✅ Message sent to QueueOrder function successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message via function");
                throw new InvalidOperationException($"Error sending message: {ex.Message}");
            }
        }
    }
}