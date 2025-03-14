using HttpInference.Models;
using System.Collections.Concurrent;

namespace HttpInference.Services;

public class ExperimentQueue
{
    private readonly ConcurrentQueue<ExperimentRun> imageQueue = new();
    public void AddImageExperiment(ExperimentRun experimentRun)
    {
        imageQueue.Enqueue(experimentRun);
    }

    public ExperimentRun? GetNextImageExperiment()
    {
        imageQueue.TryDequeue(out var experimentRun);
        return experimentRun;
    }
}
