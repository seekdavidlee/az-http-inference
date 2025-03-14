namespace HttpInference.Services;

public class ExperimentRunner(IHttpClientFactory httpClientFactory, Inference inference, ExperimentQueue experimentQueue, ILogger<ExperimentRunner> logger) : BackgroundService
{
    private const int INTERNAL_MAX_PARALLEL_EXPERIMENTS = 3;// Limit to 3 concurrent tasks
    private const int INTERNAL_POLLING_INTERVAL_SECONDS = 1;

    private readonly SemaphoreSlim semaphore = new(int.TryParse(Environment.GetEnvironmentVariable("MAX_PARALLEL_EXPERIMENTS"),
        out var max) ? max : INTERNAL_MAX_PARALLEL_EXPERIMENTS);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TimeSpan pollingInterval = TimeSpan.FromSeconds(int.TryParse(Environment.GetEnvironmentVariable("POLLING_INTERVAL_SECONDS"),
            out var pollingIntervalValue) ? pollingIntervalValue : INTERNAL_POLLING_INTERVAL_SECONDS);

        var runningTasks = new List<Task>();

        while (!stoppingToken.IsCancellationRequested)
        {
            var experiment = experimentQueue.GetNextImageExperiment();
            if (experiment is not null)
            {
                await semaphore.WaitAsync(stoppingToken);

                // Run the experiment in a separate task
                var task = Task.Run(async () =>
                {
                    try
                    {
                        var job = new ExperimentRunJob(httpClientFactory, inference, experiment, logger, stoppingToken);
                        await job.RunAsync();
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "experiment failed to complete: {experiment_id}", experiment.Id);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, stoppingToken);

                runningTasks.Add(task);

                // Remove completed tasks
                runningTasks.RemoveAll(t => t.IsCompleted);
            }
            else
            {
                await Task.Delay(pollingInterval, stoppingToken);
            }
        }
    }
}
