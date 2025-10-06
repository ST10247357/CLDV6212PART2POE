using Functions.Models;
using System.Text;
using System.Text.Json;

namespace Functions.Services
{
    public class FileStorageService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<FileStorageService> _logger;

        public FileStorageService(IHttpClientFactory httpClientFactory, ILogger<FileStorageService> logger)
        {
            _httpClient = httpClientFactory.CreateClient("AzureFunctionsClient");
            _logger = logger;
        }

        public async Task UploadFileAsync(string directoryName, string fileName, Stream fileStream)
        {
            try
            {
                using var memoryStream = new MemoryStream();
                await fileStream.CopyToAsync(memoryStream);
                var base64Data = Convert.ToBase64String(memoryStream.ToArray());

                var uploadRequest = new
                {
                    Base64Data = base64Data
                };

                var uploadJson = JsonSerializer.Serialize(uploadRequest);
                var content = new StringContent(uploadJson, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"files/upload/{directoryName}/{fileName}", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new InvalidOperationException($"Error uploading file: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file via function");
                throw new InvalidOperationException($"Error uploading file: {ex.Message}");
            }
        }

        public async Task<Stream> DownloadFileAsync(string directoryName, string fileName)
        {
            try
            {
                var response = await _httpClient.GetAsync($"files/download/{directoryName}/{fileName}");

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(responseContent);

                    if (result != null && result.TryGetValue("base64Data", out JsonElement base64Element))
                    {
                        var base64Data = base64Element.GetString();
                        var fileBytes = Convert.FromBase64String(base64Data);
                        return new MemoryStream(fileBytes);
                    }
                    throw new InvalidOperationException("File data not found in response");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new InvalidOperationException($"Error downloading file: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file via function");
                throw new InvalidOperationException($"Error downloading file: {ex.Message}");
            }
        }

        public async Task<List<FileModel>> ListFileAsync(string directoryName)
        {
            try
            {
                var response = await _httpClient.GetAsync($"files/list/{directoryName}");

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(responseContent);

                    if (result != null && result.TryGetValue("files", out JsonElement filesElement))
                    {
                        var files = JsonSerializer.Deserialize<List<FileModel>>(filesElement.GetRawText());
                        return files ?? new List<FileModel>();
                    }
                    throw new InvalidOperationException("Files list not found in response");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new InvalidOperationException($"Error listing files: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing files via function");
                throw new InvalidOperationException($"Error listing files: {ex.Message}");
            }
        }

        public async Task DeleteFileAsync(string directoryName, string fileName)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"files/delete/{directoryName}/{fileName}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new InvalidOperationException($"Error deleting file: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file via function");
                throw new InvalidOperationException($"Error deleting file: {ex.Message}");
            }
        }

        public async Task UploadFileAutoAsync(string fileName, Stream fileStream, string directoryName = "uploads")
        {
            try
            {
                using var memoryStream = new MemoryStream();
                await fileStream.CopyToAsync(memoryStream);
                var base64Data = Convert.ToBase64String(memoryStream.ToArray());

                var uploadRequest = new
                {
                    DirectoryName = directoryName,
                    FileName = fileName,
                    Base64Data = base64Data
                };

                var uploadJson = JsonSerializer.Serialize(uploadRequest);
                var content = new StringContent(uploadJson, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("files/upload", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new InvalidOperationException($"Error uploading file: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file via function");
                throw new InvalidOperationException($"Error uploading file: {ex.Message}");
            }
        }

     
        }
    }


