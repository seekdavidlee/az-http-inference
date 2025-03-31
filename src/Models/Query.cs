using Azure.AI.Inference;

namespace HttpInference.Models;

public record Query(string? ImageId, string? ImageRouteId, string? Text, string SystemPrompt, float Temperature, int MaxTokens, float TopP, bool? ExpectJson);

public record QueryResponse(string Text, double TimeTakenInMilliseconds, CompletionsUsage? Usage);
