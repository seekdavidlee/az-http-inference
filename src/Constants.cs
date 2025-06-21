namespace HttpInference;

public static class Constants
{
    public const string FileSystemClient = "FileSystemClient";
      /// <summary>
    /// Environment variable keys used by the application
    /// </summary>
    public static class EnvironmentVariables
    {
        public const string FileSystemUrl = "FILE_SYSTEM_URL";
        public const string FileSystemApi = "FILE_SYSTEM_API";
        public const string FileSystemPath = "FILE_SYSTEM_PATH";
        public const string ApiListenPort = "API_LISTEN_PORT";
        public const string AzKeyPrefix = "AZKEY_";
        
        // Inference settings
        public const string MaxInferenceRetry = "MAX_INFERENCE_RETRY";
        public const string InferenceRetryIntervalInMilliseconds = "INFERENCE_RETRY_INTERVAL_IN_MILLISECONDS";
        public const string DisableNucleusSamplingFactorPrefix = "DisableNucleusSamplingFactor_";
        
        // Experiment settings
        public const string MaxParallelExperiments = "MAX_PARALLEL_EXPERIMENTS";
        public const string PollingIntervalSeconds = "POLLING_INTERVAL_SECONDS";
    }
}
