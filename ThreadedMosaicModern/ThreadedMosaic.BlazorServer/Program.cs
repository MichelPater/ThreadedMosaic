using ThreadedMosaic.BlazorServer.Components;
using ThreadedMosaic.Core.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add SignalR for real-time progress updates
builder.Services.AddSignalR();

// Add ThreadedMosaic Core services
builder.Services.AddThreadedMosaicCore(builder.Configuration);

// Add HTTP client for API communication
builder.Services.AddHttpClient("ThreadedMosaicApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration.GetValue<string>("ApiConfiguration:BaseUrl") ?? "https://localhost:7001");
    client.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue<int>("ApiConfiguration:TimeoutSeconds", 300));
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
