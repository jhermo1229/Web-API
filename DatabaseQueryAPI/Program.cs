using DatabaseQueryAPI.Model;
using DatabaseQueryAPI.Services;
using DatabaseQueryAPI.Services.Scheduling;
using Microsoft.Extensions.Logging;  // Add the necessary namespace


var builder = WebApplication.CreateBuilder(args);

// Register services in the DI container
builder.Services.AddControllers();
builder.Services.AddScoped<DatabaseService>();  // Register DatabaseService for injection
builder.Services.AddScoped<EmailService>();
builder.Services.AddSingleton<ExcelReportService>();
builder.Services.AddScoped<GearReportService>();
builder.Services.AddHostedService<ReportSchedulerService>();
builder.Services.Configure<List<ReportJobOptions>>(builder.Configuration.GetSection("ReportJobs"));
builder.Services.AddScoped<ReportJobRunner>();

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

app.Urls.Clear();
app.Urls.Add("http://0.0.0.0:5233");

// Configure middleware
app.UseAuthorization();
app.MapControllers();

// Start the app
app.Run();
