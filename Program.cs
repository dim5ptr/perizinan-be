using Microsoft.EntityFrameworkCore;
using PresensiQRBackend.Data;
using PresensiQRBackend.Services;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

// Service Registrations
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<INotificationService, NotificationService>(); // Tambahkan ini

builder.Services.AddHttpClient();

// DB Context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
           .EnableSensitiveDataLogging()
           .EnableDetailedErrors());

// HTTP Client for AuthService
builder.Services.AddHttpClient<AuthService>(client =>
{
    var baseUrl = builder.Configuration["AuthApi:BaseUrl"];
    var apiKey = builder.Configuration["AuthApi:ApiKey"];
    var devKey = builder.Configuration["AuthApi:DevKey"];

    if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(devKey))
    {
        Console.WriteLine("❌ Environment variable AuthApi config is missing!");
        throw new InvalidOperationException("Missing AuthApi configuration");
    }

    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add("x-api-key", apiKey);
    client.DefaultRequestHeaders.Add("dev-key", devKey);
});


// Logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Debug);
});

var app = builder.Build();

// Middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.MapControllers();

// 👇 Auto migrate saat aplikasi start (penting untuk Docker)
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.Migrate();
}

// ✅ Listen on all network interfaces (IP public)
app.Run("http://0.0.0.0:8082");

