using Azure;
using Azure.AI.Inference;
using HttpInference.Models;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace HttpInference.Services;

public class Inference(IHttpClientFactory httpClientFactory, ILogger<Inference> logger)
{
    private readonly string route = $"{Environment.GetEnvironmentVariable("FILE_SYSTEM_API")!}/storage/files/object?path={Environment.GetEnvironmentVariable("FILE_SYSTEM_PATH")!}";
    private readonly string route_no_path = $"{Environment.GetEnvironmentVariable("FILE_SYSTEM_API")!}/storage/files/object?path=";
    private const string KEY_PREFIX = "AZKEY_";
    private static readonly ConcurrentDictionary<string, ChatCompletionsClient> clients = [];
    static Inference()
    {
        var clientKeys = GetEnvKeys();
        foreach (var key in clientKeys)
        {
            var parts = Environment.GetEnvironmentVariable(key)!.Split(';');
            if (parts.Length == 2)
            {
                var uri = new Uri(parts[0]);
                var apiKey = new AzureKeyCredential(parts[1]);
                var client = new ChatCompletionsClient(uri, apiKey);
                clients[key[KEY_PREFIX.Length..]] = client;
            }
        }
    }

    private static string[] GetEnvKeys()
    {
        return Environment.GetEnvironmentVariables().Keys.Cast<string>().Where(k => k.StartsWith("AZKEY_")).ToArray();
    }

    public string[] GetKeys()
    {
        return GetEnvKeys().Select(x => x[KEY_PREFIX.Length..]).ToArray();
    }

    public async Task<Result<QueryResponse>> QueryAsync(string key, Query query)
    {
        if (query.Text is null && query.ImageId is null && query.ImageRouteId is null)
        {
            return new Result<QueryResponse>(false, "Either Text or ImageId or ImageRouteId must be provided", default);
        }

        string imgInfo;
        BinaryData imageData;
        if (query.ImageId is not null || query.ImageRouteId is not null)
        {
            imgInfo = query.ImageRouteId is not null ? $"{route_no_path}{query.ImageRouteId}" : $"{route}/{query.ImageId}";
            // pull image from storage
            var http = httpClientFactory.CreateClient(Constants.FileSystemClient);
            try
            {
                var bytes = await http.GetByteArrayAsync(imgInfo);
                imageData = BinaryData.FromBytes(bytes);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to get image from storage");
                return new Result<QueryResponse>(false, "Failed to get image from storage", default);
            }
        }
        else
        {
            imgInfo = "from query text";
            imageData = BinaryData.FromString(query.Text!);
        }

        if (clients.TryGetValue(key, out var client))
        {
            List<ChatMessageContentItem> items = [];
            if (!string.IsNullOrEmpty(query.Text))
            {
                items.Add(new ChatMessageTextContentItem(query.Text));
            }

            items.Add(new ChatMessageImageContentItem(imageData, "image/jpg"));

            var request = new ChatCompletionsOptions
            {
                MaxTokens = query.MaxTokens,
                Temperature = query.Temperature,
                Messages = [
                    new ChatRequestSystemMessage(query.SystemPrompt),
                    new ChatRequestUserMessage(items)
                ]
            };

            var disableNucleusSamplingFactor = Environment.GetEnvironmentVariable($"DisableNucleusSamplingFactor_{key}");
            if (string.IsNullOrEmpty(disableNucleusSamplingFactor) || disableNucleusSamplingFactor != "true")
            {
                request.NucleusSamplingFactor = query.TopP;
            }

            logger.LogInformation($"processing image: {imgInfo}", imgInfo);
            try
            {
                Stopwatch stopwatch = new();
                stopwatch.Start();
                var response = await client.CompleteAsync(request);
                stopwatch.Stop();

                var queryResponse = new QueryResponse(response.Value.Content, stopwatch.ElapsedMilliseconds, response.Value.Usage);
                return new Result<QueryResponse>(true, null, queryResponse);
            }
            catch (Exception e)
            {
                return new Result<QueryResponse>(false, e.ToString(), default);
            }
        }
        return new Result<QueryResponse>(false, "Invalid key", default);
    }
}
