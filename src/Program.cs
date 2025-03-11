using HttpInference;
using HttpInference.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddScoped<Inference>();
builder.Services.AddControllers();

var baseUrl = Environment.GetEnvironmentVariable("FILE_SYSTEM_URL")!;

builder.Services.AddHttpClient(nameof(Constants.FileSystemClient), client =>
{
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromMinutes(8);
});


var app = builder.Build();

// Configure the HTTP request pipeline.

app.MapControllers();

app.Run();
