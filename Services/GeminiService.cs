
using System.Net.Http.Json;
using System.Text.Json;

namespace EStore.Services
{
    public class GeminiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GeminiService> _logger;

        public GeminiService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<GeminiService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<string> GenerateReplyAsync(
            string prompt,
            CancellationToken cancellationToken = default)
        {
            string? apiKey = _configuration["Gemini:ApiKey"];
            string? model = _configuration["Gemini:Model"];

            if (string.IsNullOrWhiteSpace(apiKey) ||
                string.IsNullOrWhiteSpace(model))
            {
                throw new InvalidOperationException(
                    "Gemini:ApiKey or Gemini:Model is missing. Check User Secrets.");
            }

            // Remove an optional "models/" prefix from the configured ID.
            model = model.Trim();
            if (model.StartsWith("models/", StringComparison.OrdinalIgnoreCase))
                model = model["models/".Length..];

            string endpoint =
                $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";

            // Maximum 4 attempts for temporary errors.
            for (int attempt = 0; attempt < 2; attempt++)
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Post, endpoint);

                request.Headers.Add("x-goog-api-key", apiKey);

                request.Content = JsonContent.Create(new
                {
                    contents = new[]
                    {
                        new
                        {
                            role = "user",
                            parts = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.4,
                        maxOutputTokens = 600
                    }
                });

                using var response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                string responseText = await response.Content
                    .ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    using var json = JsonDocument.Parse(responseText);
                    var root = json.RootElement;

                    if (root.TryGetProperty("candidates", out var candidates) &&
                        candidates.GetArrayLength() > 0 &&
                        candidates[0].TryGetProperty("content", out var content) &&
                        content.TryGetProperty("parts", out var parts))
                    {
                        var answer = string.Join(
                            "\n",
                            parts.EnumerateArray()
                                .Where(p => p.TryGetProperty("text", out _))
                                .Select(p => p.GetProperty("text").GetString())
                                .Where(t => !string.IsNullOrWhiteSpace(t)));

                        if (!string.IsNullOrWhiteSpace(answer))
                            return answer;
                    }

                    throw new InvalidOperationException(
                        "Gemini responded but returned no text. Check the model response.");
                }

                int status = (int)response.StatusCode;

                bool retryable =
                    status == 408 ||
                    status == 429 ||
                    status == 500 ||
                    status == 502 ||
                    status == 503 ||
                    status == 504;
                if (!retryable || attempt == 1)
                {
                    _logger.LogError(
                        "Gemini API failed. HTTP {StatusCode}; response: {Response}",
                        status, responseText);

                    throw new HttpRequestException(
                        $"Gemini API returned HTTP {status}. {responseText}",
                        null,
                        response.StatusCode);
                }

                // Exponential backoff: approximately 1, 2, 4 seconds,
                // with a small random delay to avoid synchronized retries.
                int delaySeconds = (int)Math.Pow(2, attempt + 1) / 2;
                int jitterMs = Random.Shared.Next(100, 700);

                await Task.Delay(
       TimeSpan.FromSeconds(1) +
       TimeSpan.FromMilliseconds(Random.Shared.Next(100, 400)),
       cancellationToken);
            }

            throw new HttpRequestException(
                "Gemini could not complete the request after retries.");
        }
    }
}
