using ThreadedMosaic.BlazorServer.Components;
using ThreadedMosaic.Core.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add SignalR for real-time progress updates
builder.Services.AddSignalR(options =>
{
    if (builder.Environment.IsDevelopment())
    {
        options.EnableDetailedErrors = true;
    }
    
    // Configure timeouts from appsettings
    var signalRConfig = builder.Configuration.GetSection("SignalR");
    if (signalRConfig.Exists())
    {
        if (TimeSpan.TryParse(signalRConfig["KeepAliveInterval"], out var keepAlive))
            options.KeepAliveInterval = keepAlive;
            
        if (TimeSpan.TryParse(signalRConfig["HandshakeTimeout"], out var handshake))
            options.HandshakeTimeout = handshake;
            
        if (TimeSpan.TryParse(signalRConfig["ClientTimeoutInterval"], out var clientTimeout))
            options.ClientTimeoutInterval = clientTimeout;
    }
});

// Add ThreadedMosaic Core services
builder.Services.AddThreadedMosaicCore(builder.Configuration);

// Add HTTP client for API communication
builder.Services.AddHttpClient("ThreadedMosaicApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration.GetValue<string>("ApiConfiguration:BaseUrl") ?? "https://localhost:7001");
    client.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue<int>("ApiConfiguration:TimeoutSeconds", 300));
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    if (builder.Environment.IsDevelopment())
    {
        // In development, accept self-signed certificates
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
    }
    return handler;
});

// Add Blazor-specific services
builder.Services.AddScoped<ThreadedMosaic.BlazorServer.Services.MosaicApiService>();
builder.Services.AddSingleton<ThreadedMosaic.BlazorServer.Services.ProcessingStateService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

// Map SignalR hubs
app.MapHub<ThreadedMosaic.BlazorServer.Hubs.ProgressHub>("/progresshub");

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
