using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BaseMVC.WebUI.Security;

public sealed class RecaptchaV3Verifier(HttpClient httpClient, IConfiguration configuration) : IRecaptchaV3Verifier
{
    private const string SiteVerifyUrl = "https://www.google.com/recaptcha/api/siteverify";
    private const double MinimumScore = 0.5;

    public async Task<bool> VerifyAsync(string token, string expectedAction, CancellationToken cancellationToken)
    {
        var secretKey = configuration["RECAPTCHA_SECRET_KEY"];
        if (string.IsNullOrWhiteSpace(secretKey) || string.IsNullOrWhiteSpace(token))
            return false;

        try
        {
            using var response = await httpClient.PostAsync(
                SiteVerifyUrl,
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["secret"] = secretKey,
                    ["response"] = token
                }),
                cancellationToken);
            if (!response.IsSuccessStatusCode)
                return false;

            var result = await response.Content.ReadFromJsonAsync<SiteVerifyResponse>(cancellationToken: cancellationToken);
            return result is { Success: true, Score: not null, Action: not null }
                && result.Score.Value >= MinimumScore
                && string.Equals(result.Action, expectedAction, StringComparison.Ordinal);
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private sealed class SiteVerifyResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; init; }

        [JsonPropertyName("score")]
        public double? Score { get; init; }

        [JsonPropertyName("action")]
        public string? Action { get; init; }

        [JsonPropertyName("hostname")]
        public string? Hostname { get; init; }
    }
}
