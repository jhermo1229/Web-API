using DatabaseQueryAPI.Services;
using Microsoft.Extensions.Logging;  // Add the necessary namespace

var builder = WebApplication.CreateBuilder(args);

// Register services in the DI container
builder.Services.AddControllers();
builder.Services.AddScoped<DatabaseService>();  // Register DatabaseService for injection

// Add logging services
builder.Services.AddLogging(config =>
{
    // Configure logging to the console
    config.AddConsole();
    config.AddDebug();  // Optional: This will log to the debug output window in Visual Studio

    // Set the minimum log level to Information (adjust if needed)
    config.SetMinimumLevel(LogLevel.Information);
});

var app = builder.Build();

// Tell the application to listen on specific IPs and ports
app.Urls.Add("http://192.168.2.127:5233");
app.Urls.Add("https://192.168.2.127:7136"); // For HTTPS if needed

// Configure middleware
app.UseAuthorization();
app.MapControllers();

// Start the app
app.Run();
