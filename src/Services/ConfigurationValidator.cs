namespace HttpInference.Services;

/// <summary>
/// Validates that all required configuration settings are present.
/// </summary>
public static class ConfigurationValidator
{
    /// <summary>
    /// Validates that all required environment variables are present.
    /// Throws an InvalidOperationException if any required configuration is missing.
    /// </summary>
    public static void ValidateEnvironmentVariables()
    {
        var requiredConfigurations = new Dictionary<string, string>
        {
            { Constants.EnvironmentVariables.FileSystemUrl, "Base URL for file system API" },
            { Constants.EnvironmentVariables.FileSystemApi, "File system API endpoint" },
            { Constants.EnvironmentVariables.FileSystemPath, "Path in the file system" }
        };        // Check if we have at least one AZKEY_ configuration
        var azKeyExists = Environment.GetEnvironmentVariables().Keys
            .Cast<string>()
            .Any(k => k.StartsWith(Constants.EnvironmentVariables.AzKeyPrefix));

        if (!azKeyExists)
        {
            requiredConfigurations.Add($"{Constants.EnvironmentVariables.AzKeyPrefix}*", "At least one Azure key configuration (e.g., AZKEY_MODEL) is required");
        }

        var missingConfigs = requiredConfigurations.Keys
            .Where(key => string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)) && !key.Contains("*"))
            .ToList();

        var isConfigValid = missingConfigs.Count == 0 && azKeyExists;

        if (!isConfigValid)
        {
            var errorMessage = GenerateErrorMessage(missingConfigs);
            throw new InvalidOperationException(errorMessage);
        }
    }

    /// <summary>
    /// Generates a descriptive error message for missing configurations.
    /// </summary>
    /// <param name="missingConfigurations">List of missing configuration keys.</param>
    /// <returns>A formatted error message.</returns>
    private static string GenerateErrorMessage(List<string> missingConfigurations)
    {
        if (missingConfigurations.Count == 0)
        {
            return string.Empty;
        }

        var message = "The application is missing the following required configuration settings:\n";
        foreach (var config in missingConfigurations)
        {
            message += $"- {config}\n";
        }
        message += "\nPlease check your environment variables or application settings.";

        return message;
    }
}
