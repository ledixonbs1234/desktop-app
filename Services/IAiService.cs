namespace desktop_app.Services;

public interface IAiService
{
    Task<AiResponse> AskAsync(string question, string? imageBase64 = null);
}

public record AiResponse(string Answer, string Model, DateTime Timestamp);
