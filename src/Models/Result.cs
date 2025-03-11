namespace HttpInference.Models;

public record Result<T>(bool Success, string? ErrorMessage, T? Item);
