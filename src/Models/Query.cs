using Azure.AI.Inference;

namespace HttpInference.Models;

public record Query(string? ImageId, string? Text, string SystemPrompt, float Temperature, int MaxTokens, float TopP);

public record QueryResponse(string Text, double TimeTakenInMilliseconds, CompletionsUsage? Usage);
