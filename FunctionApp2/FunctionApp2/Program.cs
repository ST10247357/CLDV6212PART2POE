using FunctionApp2;
using FunctionApp2.Functions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();
builder.Services.AddScoped<TableStorageFunction>();
builder.Services.AddScoped<BlobStorageFunction>();
builder.Services.AddScoped<FileStorageFunction>();
builder.Services.AddScoped<OrderQueueFunction>(); 


builder.Build().Run();
