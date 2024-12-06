using DatabaseQueryAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// Register services in the DI container
builder.Services.AddControllers();
builder.Services.AddScoped<DatabaseService>();  // Register DatabaseService for injection

var app = builder.Build();

app.UseAuthorization();

app.MapControllers();

app.Run();
