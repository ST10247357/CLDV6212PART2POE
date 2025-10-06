using Functions.Services;

namespace Functions
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllersWithViews();

            builder.Services.AddHttpClient("AzureFunctionsClient", client =>
            {

                client.BaseAddress = new Uri("https://st10247357.azurewebsites.net/api/");
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            builder.Services.AddScoped<TableStorageService>(); 
            builder.Services.AddScoped<BlobStorageService>();
            builder.Services.AddScoped<FileStorageService>();
            builder.Services.AddScoped<QueueStorageService>();

            var app = builder.Build();

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }
    }
}