using HttpInference.Models;

namespace HttpInference.Services;

public class ExperimentRunJob(IHttpClientFactory httpClientFactory, Inference inference, ExperimentRun experiment, ILogger<ExperimentRunner> logger, CancellationToken stoppingToken)
{
    private readonly HttpClient fileSystemclient = httpClientFactory.CreateClient(Constants.FileSystemClient);
    private readonly string route = $"{Environment.GetEnvironmentVariable("FILE_SYSTEM_API")!}/storage/files/object?path=";

    public async Task RunAsync()
    {
        if (experiment.OutputFileSystemApiPath is null)
        {
            logger.LogError("OutputFileSystemApiPath is not set for experiment {experiment_id}", experiment.Id);
            return;
        }

        await LogAsync($"initializing experiment {experiment.Id}", ExperimentLogLevel.Information);
        experiment.Start = DateTime.UtcNow;
        await CreateOrUpdateAsync(experiment);

        if (experiment.ModelId is null)
        {
            await LogAsync($"ModelId is not set for experiment {experiment.Id}", ExperimentLogLevel.Error, true);
            experiment.End = DateTime.UtcNow;
            await CreateOrUpdateAsync(experiment);
            return;
        }

        if (experiment.SystemPrompt is null)
        {
            await LogAsync($"SystemPrompt is not set for experiment {experiment.Id}", ExperimentLogLevel.Error, true);
            experiment.End = DateTime.UtcNow;
            await CreateOrUpdateAsync(experiment);
            return;
        }

        var datasetFilePaths = await fileSystemclient.GetFromJsonAsync<string[]>(experiment.DataSetFileSystemApiPath, cancellationToken: stoppingToken);
        if (datasetFilePaths is null || datasetFilePaths.Length == 0)
        {
            await LogAsync($"Failed to get dataset file paths for experiment {experiment.Id}", ExperimentLogLevel.Error, true);
            experiment.End = DateTime.UtcNow;
            await CreateOrUpdateAsync(experiment);
            return;
        }

        var imageFilePaths = datasetFilePaths.Where(x => x.EndsWith(".jpg")).ToArray();
        if (imageFilePaths.Length == 0)
        {
            await LogAsync($"Failed to get dataset image file paths for experiment {experiment.Id}", ExperimentLogLevel.Error, true);
            experiment.End = DateTime.UtcNow;
            await CreateOrUpdateAsync(experiment);
            return;
        }

        if (experiment.GroundTruthTagFilters is not null && experiment.GroundTruthTagFilters.Length > 0)
        {
            foreach (var dsFilePath in datasetFilePaths.Where(x => x.EndsWith(".json")))
            {
                try
                {
                    var ds = await fileSystemclient.GetFromJsonAsync<GroundTruthImage>($"{route}{dsFilePath}", cancellationToken: stoppingToken);
                    if (ds is not null && ds.Tags is not null)
                    {
                        int count = 0;
                        // all must match
                        foreach (var expTag in experiment.GroundTruthTagFilters)
                        {
                            count += ds.Tags.Any(x => x.Name == expTag.Name && x.Value == expTag.Value) ? 1 : 0;
                        }

                        if (count != experiment.GroundTruthTagFilters.Length)
                        {
                            var dsImagePath = dsFilePath.Replace(".json", ".jpg");
                            imageFilePaths = imageFilePaths.Where(p => p != dsImagePath).ToArray();
                        }
                    }
                }
                catch (HttpRequestException e)
                {
                    await LogAsync($"Failed to get ground truth image {dsFilePath} for experiment {experiment.Id}, error: {e}", ExperimentLogLevel.Error);
                    continue;
                }
            }

            if (imageFilePaths.Length == 0)
            {
                await LogAsync($"no images left to process for {experiment.Id} after filtering applied", ExperimentLogLevel.Warning, true);
                experiment.End = DateTime.UtcNow;
                await CreateOrUpdateAsync(experiment);
                return;
            }
        }

        int max = experiment.Iterations * imageFilePaths.Length;
        int progress = 0;
        int errorCount = 0;
        for (int i = 0; i < experiment.Iterations; i++)
        {
            foreach (var imageFilePath in imageFilePaths)
            {
                try
                {
                    var result = await inference.QueryAsync(experiment.ModelId!, new Query(default, imageFilePath, experiment.UserPrompt, experiment.SystemPrompt, experiment.Temperature, experiment.MaxTokens, experiment.TopP, experiment.ExpectJsonOutput));

                    if (!result.Success)
                    {
                        errorCount++;
                        await LogAsync($"failed to inference image {imageFilePath} at iteration {i}, error: {result.ErrorMessage}", ExperimentLogLevel.Error);
                    }
                    else
                    {
                        if (result.Item is not null)
                        {
                            var e = new ExperimentRunResult
                            {
                                Id = Guid.NewGuid(),
                                Text = result.Item.Text,
                                CompletionTokens = result.Item.Usage!.CompletionTokens,
                                PromptTokens = result.Item.Usage.PromptTokens,
                                TotalTokens = result.Item.Usage.TotalTokens
                            };
                            await LogQueryResponseAsync(e);
                            var meta = new Dictionary<string, string>
                            {
                                { "iteration", i.ToString() },
                                { "image_file_path", imageFilePath }
                            };
                            await LogMetricAsync(e.Id, "inference_time", result.Item!.TimeTakenInMilliseconds, meta);
                        }
                    }
                }
                catch (Exception ex)
                {
                    errorCount++;
                    await LogAsync($"failed to inference image {imageFilePath} at iteration {i}, error: {ex}", ExperimentLogLevel.Error);
                }

                progress++;
                await LogAsync($"processed {progress} of {max}", ExperimentLogLevel.Information);
            }
        }

        if (errorCount > 0)
        {
            await LogAsync($"experiment {experiment.Id} completed with {errorCount} errors", ExperimentLogLevel.Warning, true);
        }
        else
        {
            await LogAsync($"experiment {experiment.Id} completed successfully", ExperimentLogLevel.Information, true);
        }

        experiment.End = DateTime.UtcNow;
        await CreateOrUpdateAsync(experiment);
    }

    private async Task LogQueryResponseAsync(ExperimentRunResult runResult)
    {
        try
        {
            var timestampFileName = DateTime.UtcNow.Ticks;
            await fileSystemclient.PutAsJsonAsync($"{route}{experiment.OutputFileSystemApiPath}/{experiment.Id}/results/{timestampFileName}.json", runResult);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to log response for experiment {experiment_id}", experiment.Id);
        }
    }

    private async Task LogMetricAsync(Guid resultId, string metricName, double value, Dictionary<string, string>? Meta)
    {
        try
        {
            var timestampFileName = DateTime.UtcNow.Ticks;
            await fileSystemclient.PutAsJsonAsync($"{route}{experiment.OutputFileSystemApiPath}/{experiment.Id}/metrics/{timestampFileName}.json", new ExperimentMetric
            {
                ResultId = resultId,
                Name = metricName,
                Value = value,
                Meta = Meta
            });
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to log metric for experiment {experiment_id}", experiment.Id);
        }
    }

    private async Task CreateOrUpdateAsync(ExperimentRun experimentRun)
    {
        try
        {
            await fileSystemclient.PutAsJsonAsync($"{route}{experiment.OutputFileSystemApiPath}/{experiment.Id}/run.json", experimentRun);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to create or update experiment run {experiment_id}", experiment.Id);
        }
    }

    private async Task LogAsync(string message, ExperimentLogLevel level, bool? lastLog = null)
    {
        try
        {
            var timestampFileName = DateTime.UtcNow.Ticks;
            await fileSystemclient.PutAsJsonAsync($"{route}{experiment.OutputFileSystemApiPath}/{experiment.Id}/logs/{timestampFileName}.json", new ExperimentLog
            {
                Level = level,
                Message = message,
                Created = DateTime.UtcNow,
                LastLog = lastLog
            });
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to log {level} for experiment {experiment_id}", level, experiment.Id);
        }
    }
}

