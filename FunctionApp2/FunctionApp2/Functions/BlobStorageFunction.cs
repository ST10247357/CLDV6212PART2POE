using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace FunctionApp2
{
    public class BlobStorageFunction
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly string _containerName = "image";
        private readonly ILogger<BlobStorageFunction> _logger;

        public BlobStorageFunction(ILogger<BlobStorageFunction> logger)
        {
            var connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            _blobServiceClient = new BlobServiceClient(connectionString);
            _logger = logger;
        }

        [Function("UploadBlob")]
        public async Task<HttpResponseData> UploadBlob(
      [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "blob/upload")] HttpRequestData req)
        {
            var response = req.CreateResponse();

            try
            {
                var body = await req.ReadAsStringAsync();
                _logger.LogInformation($"Received upload request: {body.Length} characters");

                var request = JsonSerializer.Deserialize<UploadBlobRequest>(body);

                if (string.IsNullOrEmpty(request?.FileName) || string.IsNullOrEmpty(request?.Base64Data))
                {
                    response.StatusCode = System.Net.HttpStatusCode.BadRequest;
                    await response.WriteAsJsonAsync(new { error = "FileName and Base64Data are required" });
                    return response;
                }

                _logger.LogInformation($"Processing file: {request.FileName}");

                var fileBytes = Convert.FromBase64String(request.Base64Data);
                _logger.LogInformation($"File bytes: {fileBytes.Length}");

                using var stream = new MemoryStream(fileBytes);

                var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
                await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

                var blobClient = containerClient.GetBlobClient(request.FileName);

                var uploadOptions = new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = GetContentType(request.FileName)
                    }
                };

                await blobClient.UploadAsync(stream, uploadOptions);

                var blobUrl = blobClient.Uri.ToString();
                _logger.LogInformation($"Blob uploaded successfully: {blobUrl}");

                response.StatusCode = System.Net.HttpStatusCode.OK;
                await response.WriteAsJsonAsync(new
                {
                    message = "Blob uploaded successfully",
                    fileName = request.FileName,
                    blobUrl = blobUrl
                });
            }
            catch (FormatException formatEx)
            {
                _logger.LogError(formatEx, "Invalid base64 data");
                response = req.CreateResponse();
                response.StatusCode = System.Net.HttpStatusCode.BadRequest;
                await response.WriteAsJsonAsync(new { error = "Invalid base64 data format" });
            }
            catch (Azure.RequestFailedException azureEx)
            {
                _logger.LogError(azureEx, "Azure storage error");
                response = req.CreateResponse();
                response.StatusCode = System.Net.HttpStatusCode.InternalServerError;
                await response.WriteAsJsonAsync(new { error = $"Storage error: {azureEx.Message}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading blob");
                response = req.CreateResponse();
                response.StatusCode = System.Net.HttpStatusCode.InternalServerError;
                await response.WriteAsJsonAsync(new { error = ex.Message });
            }

            return response;
        }

        private string GetContentType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };
        }

        [Function("DeleteBlob")]
        public async Task<HttpResponseData> DeleteBlob(
     [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "blob/delete/{blobUri}")] HttpRequestData req,
     string blobUri)
        {
            var response = req.CreateResponse();

            try
            {
                if (string.IsNullOrEmpty(blobUri))
                {
                    response.StatusCode = System.Net.HttpStatusCode.BadRequest;
                    await response.WriteAsJsonAsync(new { error = "Blob URI is required" });
                    return response;
                }

                var decodedBlobUrl = System.Net.WebUtility.UrlDecode(blobUri);

                var uri = new Uri(decodedBlobUrl);
                var blobName = Path.GetFileName(uri.LocalPath);
                var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
                var blobClient = containerClient.GetBlobClient(blobName);

                await blobClient.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots);

                _logger.LogInformation($"Blob deleted: {decodedBlobUrl}");

                response.StatusCode = System.Net.HttpStatusCode.OK;
                await response.WriteAsJsonAsync(new
                {
                    message = "Blob deleted successfully",
                    blobUri = decodedBlobUrl
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting blob");
                response = req.CreateResponse();
                response.StatusCode = System.Net.HttpStatusCode.BadRequest;
                await response.WriteAsJsonAsync(new { error = ex.Message });
            }

            return response;
        }
    }

    public class UploadBlobRequest
    {
        public string FileName { get; set; }
        public string Base64Data { get; set; }
    }
}