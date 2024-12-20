using DatabaseQueryAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// Register services in the DI container
builder.Services.AddControllers();
builder.Services.AddScoped<DatabaseService>();  // Register DatabaseService for injection

var app = builder.Build();

// Tell the application to listen on a specific IP (e.g., 192.168.2.128) and port (e.g., 5233)
app.Urls.Add("http://192.168.2.128:5233");
app.Urls.Add("https://192.168.2.128:7136"); // For HTTPS if needed

// Configure middleware
app.UseAuthorization();
app.MapControllers();

// Start the app
app.Run();
