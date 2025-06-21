using HttpInference;
using HttpInference.Services;

// Validate configuration before starting the application
ConfigurationValidator.ValidateEnvironmentVariables();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins", builder => builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

// Add services to the container.
builder.Services.AddSingleton<Inference>();
builder.Services.AddSingleton<ExperimentQueue>();
builder.Services.AddControllers();
builder.Services.AddHostedService<ExperimentRunner>();

var baseUrl = Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.FileSystemUrl)!;

builder.Services.AddHttpClient(nameof(Constants.FileSystemClient), client =>
{
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromMinutes(8);
});

var apiListenPortStr = Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.ApiListenPort);
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
