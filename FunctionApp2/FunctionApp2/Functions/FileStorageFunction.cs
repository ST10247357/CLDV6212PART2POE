using Azure;
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using FunctionApp2.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FunctionApp2
{
    public class FileStorageFunction
    {
        private readonly ShareServiceClient _shareServiceClient;
        private readonly string _fileShareName = "orderdoc";
        private readonly ILogger<FileStorageFunction> _logger;

        public FileStorageFunction(ILogger<FileStorageFunction> logger)
        {
            var connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            _shareServiceClient = new ShareServiceClient(connectionString);
            _logger = logger;
        }

        [Function("UploadFile")]
        public async Task<HttpResponseData> UploadFile(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "files/upload/{directoryName}/{fileName}")] HttpRequestData req,
            string directoryName, string fileName)
        {
            var response = req.CreateResponse();

            try
            {
                var body = await req.ReadAsStringAsync();
                var request = JsonSerializer.Deserialize<UploadFileRequest>(body);

                if (string.IsNullOrEmpty(request?.Base64Data))
                {
                    response.StatusCode = System.Net.HttpStatusCode.BadRequest;
                    var errorJson = JsonSerializer.Serialize(new { error = "Base64Data is required" });
                    response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                    await response.WriteStringAsync(errorJson);
                    return response;
                }

                var fileBytes = Convert.FromBase64String(request.Base64Data);
                using var stream = new MemoryStream(fileBytes);

                var shareClient = _shareServiceClient.GetShareClient(_fileShareName);
                await shareClient.CreateIfNotExistsAsync();

                var directoryClient = shareClient.GetDirectoryClient(directoryName);
                await directoryClient.CreateIfNotExistsAsync();

                var fileClient = directoryClient.GetFileClient(fileName);
                await fileClient.CreateAsync(fileBytes.Length);
                await fileClient.UploadRangeAsync(new HttpRange(0, fileBytes.Length), stream);

                _logger.LogInformation($"File uploaded: {directoryName}/{fileName}");

                response.StatusCode = System.Net.HttpStatusCode.OK;
                var json = JsonSerializer.Serialize(new
                {
                    message = "File uploaded successfully",
                    directoryName = directoryName,
                    fileName = fileName,
                    fileSize = fileBytes.Length
                });
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file");
                response.StatusCode = System.Net.HttpStatusCode.BadRequest;
                var errorJson = JsonSerializer.Serialize(new { error = ex.Message });
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(errorJson);
            }

            return response;
        }

        [Function("DownloadFile")]
        public async Task<HttpResponseData> DownloadFile(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "files/download/{directoryName}/{fileName}")] HttpRequestData req,
            string directoryName, string fileName)
        {
            var response = req.CreateResponse();

            try
            {
                var shareClient = _shareServiceClient.GetShareClient(_fileShareName);
                var directoryClient = shareClient.GetDirectoryClient(directoryName);
                var fileClient = directoryClient.GetFileClient(fileName);

                var downloadInfo = await fileClient.DownloadAsync();
                using var memoryStream = new MemoryStream();
                await downloadInfo.Value.Content.CopyToAsync(memoryStream);
                var fileBytes = memoryStream.ToArray();
                var base64Data = Convert.ToBase64String(fileBytes);

                _logger.LogInformation($"File downloaded: {directoryName}/{fileName}");

                response.StatusCode = System.Net.HttpStatusCode.OK;
                var json = JsonSerializer.Serialize(new
                {
                    message = "File downloaded successfully",
                    directoryName = directoryName,
                    fileName = fileName,
                    base64Data = base64Data,
                    fileSize = fileBytes.Length
                });
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file");
                response.StatusCode = System.Net.HttpStatusCode.BadRequest;
                var errorJson = JsonSerializer.Serialize(new { error = ex.Message });
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(errorJson);
            }

            return response;
        }

        [Function("ListFiles")]
        public async Task<HttpResponseData> ListFiles(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "files/list/{directoryName}")] HttpRequestData req,
            string directoryName)
        {
            var response = req.CreateResponse();

            try
            {
                var shareClient = _shareServiceClient.GetShareClient(_fileShareName);
                var directoryClient = shareClient.GetDirectoryClient(directoryName);

                var fileModels = new List<FileModel>();
                await foreach (ShareFileItem item in directoryClient.GetFilesAndDirectoriesAsync())
                {
                    if (!item.IsDirectory)
                    {
                        var fileClient = directoryClient.GetFileClient(item.Name);
                        var properties = await fileClient.GetPropertiesAsync();
                        fileModels.Add(new FileModel
                        {
                            Name = item.Name,
                            Size = properties.Value.ContentLength,
                            LastModified = properties.Value.LastModified.UtcDateTime
                        });
                    }
                }

                _logger.LogInformation($"Listed files in directory: {directoryName}, Count: {fileModels.Count}");

                response.StatusCode = System.Net.HttpStatusCode.OK;
                var json = JsonSerializer.Serialize(new
                {
                    message = "Files listed successfully",
                    directoryName = directoryName,
                    files = fileModels,
                    count = fileModels.Count
                });
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing files");
                response.StatusCode = System.Net.HttpStatusCode.BadRequest;
                var errorJson = JsonSerializer.Serialize(new { error = ex.Message });
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(errorJson);
            }

            return response;
        }

        [Function("DeleteFile")]
        public async Task<HttpResponseData> DeleteFile(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "files/delete/{directoryName}/{fileName}")] HttpRequestData req,
            string directoryName, string fileName)
        {
            var response = req.CreateResponse();

            try
            {
                var shareClient = _shareServiceClient.GetShareClient(_fileShareName);
                var directoryClient = shareClient.GetDirectoryClient(directoryName);
                var fileClient = directoryClient.GetFileClient(fileName);

                await fileClient.DeleteIfExistsAsync();

                _logger.LogInformation($"File deleted: {directoryName}/{fileName}");

                response.StatusCode = System.Net.HttpStatusCode.OK;
                var json = JsonSerializer.Serialize(new
                {
                    message = "File deleted successfully",
                    directoryName = directoryName,
                    fileName = fileName
                });
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file");
                response.StatusCode = System.Net.HttpStatusCode.BadRequest;
                var errorJson = JsonSerializer.Serialize(new { error = ex.Message });
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(errorJson);
            }

            return response;
        }

        [Function("UploadFileAutoDirectory")]
        public async Task<HttpResponseData> UploadFileAutoDirectory(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "files/upload")] HttpRequestData req)
        {
            var response = req.CreateResponse();

            try
            {
                var body = await req.ReadAsStringAsync();
                var request = JsonSerializer.Deserialize<UploadFileAutoRequest>(body);

                if (string.IsNullOrEmpty(request?.Base64Data) || string.IsNullOrEmpty(request?.FileName))
                {
                    response.StatusCode = System.Net.HttpStatusCode.BadRequest;
                    var errorJson = JsonSerializer.Serialize(new { error = "FileName and Base64Data are required" });
                    response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                    await response.WriteStringAsync(errorJson);
                    return response;
                }

                var directoryName = request.DirectoryName ?? "uploads";
                var fileName = request.FileName;
                var fileBytes = Convert.FromBase64String(request.Base64Data);
                using var stream = new MemoryStream(fileBytes);

                var shareClient = _shareServiceClient.GetShareClient(_fileShareName);
                await shareClient.CreateIfNotExistsAsync();

                var directoryClient = shareClient.GetDirectoryClient(directoryName);
                await directoryClient.CreateIfNotExistsAsync();

                var fileClient = directoryClient.GetFileClient(fileName);
                await fileClient.CreateAsync(fileBytes.Length);
                await fileClient.UploadRangeAsync(new HttpRange(0, fileBytes.Length), stream);

                _logger.LogInformation($"File uploaded to auto directory: {directoryName}/{fileName}");

                response.StatusCode = System.Net.HttpStatusCode.OK;
                var json = JsonSerializer.Serialize(new
                {
                    message = "File uploaded successfully",
                    directoryName = directoryName,
                    fileName = fileName,
                    fileSize = fileBytes.Length
                });
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file to auto directory");
                response.StatusCode = System.Net.HttpStatusCode.BadRequest;
                var errorJson = JsonSerializer.Serialize(new { error = ex.Message });
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(errorJson);
            }

            return response;
        }

        [Function("GetFileInfo")]
        public async Task<HttpResponseData> GetFileInfo(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "files/info/{directoryName}")] HttpRequestData req,
            string directoryName)
        {
            var response = req.CreateResponse();

            try
            {
                var shareClient = _shareServiceClient.GetShareClient(_fileShareName);
                var directoryClient = shareClient.GetDirectoryClient(directoryName);

                var fileModels = new List<FileModel>();
                await foreach (ShareFileItem item in directoryClient.GetFilesAndDirectoriesAsync())
                {
                    if (!item.IsDirectory)
                    {
                        var fileClient = directoryClient.GetFileClient(item.Name);
                        var properties = await fileClient.GetPropertiesAsync();
                        fileModels.Add(new FileModel
                        {
                            Name = item.Name,
                            Size = properties.Value.ContentLength,
                            LastModified = properties.Value.LastModified.UtcDateTime
                        });
                    }
                }

                var totalSize = fileModels.Sum(f => f.Size);
                var latestFile = fileModels.OrderByDescending(f => f.LastModified).FirstOrDefault();

                response.StatusCode = System.Net.HttpStatusCode.OK;
                var json = JsonSerializer.Serialize(new
                {
                    message = "File information retrieved successfully",
                    directoryName = directoryName,
                    fileCount = fileModels.Count,
                    totalSize = totalSize,
                    latestFile = latestFile?.Name,
                    files = fileModels
                });
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file info");
                response.StatusCode = System.Net.HttpStatusCode.BadRequest;
                var errorJson = JsonSerializer.Serialize(new { error = ex.Message });
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(errorJson);
            }

            return response;
        }
    }

    public class UploadFileRequest
    {
        public string Base64Data { get; set; }
    }

    public class UploadFileAutoRequest
    {
        public string DirectoryName { get; set; } = "uploads";
        public string FileName { get; set; }
        public string Base64Data { get; set; }
    }
}