namespace desktop_app.Services;

public interface IAiService
{
    Task<AiResponse> AskAsync(string question, string? imageBase64 = null);
    IAsyncEnumerable<AiStreamChunk> AskStreamAsync(string question, string? imageBase64 = null);
    Task<bool> CheckConnectionAsync();
}

public record AiResponse(string Answer, string Model, DateTime Timestamp);
public record AiStreamChunk(string Text, string Model);