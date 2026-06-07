using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace desktop_app.Services;

public class AiService : IAiService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl = "http://localhost:3000";

    public AiService()
    {
        _httpClient = new HttpClient();
    }

    public async Task<AiResponse> AskAsync(string question, string? imageBase64 = null)
    {
        var payload = new
        {
            question,
            imageBase64
        };

        var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/ask", payload);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        
        return new AiResponse(
            Answer: result.GetProperty("answer").GetString() ?? "",
            Model: result.GetProperty("model").GetString() ?? "unknown",
            Timestamp: DateTime.Now
        );
    }
}
