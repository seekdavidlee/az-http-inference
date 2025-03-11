using HttpInference;
using HttpInference.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins", builder => builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

// Add services to the container.
builder.Services.AddScoped<Inference>();
builder.Services.AddControllers();

var baseUrl = Environment.GetEnvironmentVariable("FILE_SYSTEM_URL")!;

builder.Services.AddHttpClient(nameof(Constants.FileSystemClient), client =>
{
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromMinutes(8);
});

var apiListenPortStr = Environment.GetEnvironmentVariable("API_LISTEN_PORT");
if (apiListenPortStr is not null && int.TryParse(apiListenPortStr, out var apiListenPort))
{
    builder.WebHost.ConfigureKestrel(serverOptions =>
    {
        serverOptions.ListenAnyIP(apiListenPort);
    });
}

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseCors("AllowAllOrigins");

app.MapControllers();

app.Run();
