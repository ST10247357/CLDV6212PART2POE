using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Functions.Services
{
    public class BlobStorageService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<BlobStorageService> _logger;

        public BlobStorageService(IHttpClientFactory httpClientFactory, ILogger<BlobStorageService> logger)
        {
            _httpClient = httpClientFactory.CreateClient("AzureFunctionsClient");
            _logger = logger;
        }

        public async Task<string> UploadBlobAsync(Stream fileStream, string fileName)
        {
            try
            {
                _logger.LogInformation($"Starting blob upload for file: {fileName}");

                if (fileStream.CanSeek)
                {
                    fileStream.Position = 0;
                }

                using var memoryStream = new MemoryStream();
                await fileStream.CopyToAsync(memoryStream);
                var fileBytes = memoryStream.ToArray();
                var base64Data = Convert.ToBase64String(fileBytes);

                _logger.LogInformation($"File converted to base64, size: {base64Data.Length} characters, original bytes: {fileBytes.Length}");

                var uploadRequest = new
                {
                    FileName = fileName,
                    Base64Data = base64Data
                };

                var uploadJson = JsonSerializer.Serialize(uploadRequest);

                var content = new StringContent(uploadJson, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("blob/upload", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(responseContent);

                    if (result != null && result.TryGetValue("blobUrl", out JsonElement blobUrlElement))
                    {
                        var blobUrl = blobUrlElement.GetString();
                        _logger.LogInformation($"Blob upload successful: {blobUrl}");
                        return blobUrl;
                    }
                    throw new InvalidOperationException("Blob URL not found in response");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Function error response: {errorContent}");
                    throw new InvalidOperationException($"Error uploading blob: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading blob via function");
                throw new InvalidOperationException($"Error uploading blob: {ex.Message}");
            }
        }

        public async Task DeleteBlobAsync(string blobUri)
        {
            try
            {
                var encodedBlobUri = System.Net.WebUtility.UrlEncode(blobUri);
                var response = await _httpClient.DeleteAsync($"blob/delete/{encodedBlobUri}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new InvalidOperationException($"Error deleting blob: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting blob via function");
                throw new InvalidOperationException($"Error deleting blob: {ex.Message}");
            }
        }

        public async Task<string> UpdateBlobAsync(Stream fileStream, string fileName)
        {
            try
            {
                // Convert stream to base64
                using var memoryStream = new MemoryStream();
                await fileStream.CopyToAsync(memoryStream);
                var base64Data = Convert.ToBase64String(memoryStream.ToArray());

                var uploadRequest = new
                {
                    Base64Data = base64Data
                };

                var uploadJson = JsonSerializer.Serialize(uploadRequest);
                var content = new StringContent(uploadJson, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"blob/upload/{fileName}", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(responseContent);

                    if (result != null && result.TryGetValue("blobUrl", out JsonElement blobUrlElement))
                    {
                        return blobUrlElement.GetString();
                    }
                    throw new InvalidOperationException("Blob URL not found in response");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new InvalidOperationException($"Error updating blob: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating blob via function");
                throw new InvalidOperationException($"Error updating blob: {ex.Message}");
            }
        }


    }
}
