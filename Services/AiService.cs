using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.IO;
using System.Text;

namespace desktop_app.Services;

public class AiService : IAiService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl = "http://localhost:54321";

    public AiService()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(5);
    }

    public async Task<bool> CheckConnectionAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var response = await _httpClient.GetAsync($"{_baseUrl}/health", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
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

    public async IAsyncEnumerable<AiStreamChunk> AskStreamAsync(string question, string? imageBase64 = null)
    {
        var payload = new
        {
            question,
            imageBase64
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/ask")
        {
            Content = JsonContent.Create(payload)
        };

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream, Encoding.UTF8);

        string? line;
        // KHẮC PHỤC CHÍNH: Loại bỏ hoàn toàn thuộc tính đồng bộ .EndOfStream gây nghẽn luồng
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (line.StartsWith("data: "))
            {
                var dataContent = line.Substring(6).Trim();
                if (dataContent == "[DONE]")
                {
                    yield break;
                }

                AiStreamChunk? chunk = null;
                try
                {
                    chunk = JsonSerializer.Deserialize<AiStreamChunk>(dataContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch
                {
                    // Thầm lặng bỏ qua các lỗi phân tích cú pháp dở dang giữa chừng của mạng
                }

                if (chunk != null)
                {
                    yield return chunk;
                }
            }
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}