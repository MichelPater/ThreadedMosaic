using ThreadedMosaic.Api.Hubs;
using ThreadedMosaic.Api.Middleware;
using ThreadedMosaic.Core.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "ThreadedMosaic API", Version = "v1" });
    c.EnableAnnotations();
});

// Add ThreadedMosaic Core services
builder.Services.AddThreadedMosaicCore(builder.Configuration);

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorOrigin", policy =>
    {
        policy.WithOrigins(
                "https://localhost:7001", "http://localhost:5001",  // API ports
                "https://localhost:7002", "http://localhost:5002",  // Blazor Server ports
                "https://localhost:7003", "http://localhost:5003"   // Additional ports
              )
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Add SignalR for progress updates
builder.Services.AddSignalR();

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
if (builder.Environment.IsDevelopment())
{
    builder.Logging.SetMinimumLevel(LogLevel.Debug);
}
else
{
    builder.Logging.SetMinimumLevel(LogLevel.Information);
}

var app = builder.Build();

// Initialize database
await app.Services.EnsureDatabaseCreatedAsync();

// Configure the HTTP request pipeline
app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ThreadedMosaic API V1");
        c.RoutePrefix = string.Empty; // Serve Swagger UI at root
    });
}

app.UseHttpsRedirection();
app.UseMiddleware<ValidationMiddleware>();
app.UseCors("AllowBlazorOrigin");
app.UseAuthorization();
app.MapControllers();
app.MapHub<ProgressHub>("/progressHub");

app.Run();

// Make Program accessible for testing
public partial class Program { }
