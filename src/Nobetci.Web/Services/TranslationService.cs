using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Nobetci.Web.Services;

public class TranslationService : ITranslationService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TranslationService> _logger;

    public TranslationService(
        HttpClient httpClient, 
        IConfiguration configuration,
        ILogger<TranslationService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> TranslateToEnglishAsync(string turkishText)
    {
        return await TranslateAsync(turkishText, "Turkish", "English");
    }

    public async Task<string> TranslateToTurkishAsync(string englishText)
    {
        return await TranslateAsync(englishText, "English", "Turkish");
    }

    private async Task<string> TranslateAsync(string text, string fromLanguage, string toLanguage)
    {
        try
        {
            var apiKey = _configuration["DeepInfra:ApiKey"];
            var model = _configuration["DeepInfra:Model"] ?? "deepseek-ai/DeepSeek-V3";
            var baseUrl = _configuration["DeepInfra:BaseUrl"] ?? "https://api.deepinfra.com/v1/openai/chat/completions";

            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("DeepInfra API key not configured");
                return text;
            }

            var prompt = $@"Translate the following {fromLanguage} text to {toLanguage}. 
Keep the HTML formatting intact if present. 
Only return the translated text, nothing else.

Text to translate:
{text}";

            var requestBody = new
            {
                model = model,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                },
                temperature = 0.3
            };

            var request = new HttpRequestMessage(HttpMethod.Post, baseUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("DeepInfra API error: {StatusCode} - {Error}", response.StatusCode, errorContent);
                return text;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var jsonResponse = JsonDocument.Parse(responseContent);
            
            var translatedText = jsonResponse.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return translatedText?.Trim() ?? text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error translating text");
            return text;
        }
    }
}

