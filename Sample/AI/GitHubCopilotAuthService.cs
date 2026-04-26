using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Sample.AI;

public class GitHubCopilotAuthService
{
    const string ClientId = "Iv1.b507a08c87ecfe98";
    const string DeviceCodeUrl = "https://github.com/login/device/code";
    const string AccessTokenUrl = "https://github.com/login/oauth/access_token";
    const string CopilotTokenUrl = "https://api.github.com/copilot_internal/v2/token";
    const string SecureStorageKey = "github_copilot_access_token";

    readonly HttpClient httpClient = new();

    string? gitHubAccessToken;
    string? copilotToken;
    DateTimeOffset copilotTokenExpiry;

    public bool IsAuthenticated => gitHubAccessToken != null;

    public async Task<bool> TryRestoreSessionAsync()
    {
        var stored = await SecureStorage.Default.GetAsync(SecureStorageKey);
        if (string.IsNullOrEmpty(stored))
            return false;

        gitHubAccessToken = stored;

        try
        {
            await GetCopilotTokenAsync();
            return true;
        }
        catch
        {
            // Token is invalid/revoked — clear it
            gitHubAccessToken = null;
            SecureStorage.Default.Remove(SecureStorageKey);
            return false;
        }
    }

    public async Task<DeviceCodeResponse> RequestDeviceCodeAsync(CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, DeviceCodeUrl)
        {
            Content = JsonContent.Create(new { client_id = ClientId, scope = "read:user" })
        };
        request.Headers.Accept.ParseAdd("application/json");

        var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<DeviceCodeResponse>(ct))!;
    }

    public async Task<bool> PollForAccessTokenAsync(
        string deviceCode,
        int intervalSeconds,
        CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), ct);

            var request = new HttpRequestMessage(HttpMethod.Post, AccessTokenUrl)
            {
                Content = JsonContent.Create(new
                {
                    client_id = ClientId,
                    device_code = deviceCode,
                    grant_type = "urn:ietf:params:oauth:grant-type:device_code"
                })
            };
            request.Headers.Accept.ParseAdd("application/json");

            var response = await httpClient.SendAsync(request, ct);
            var result = await response.Content.ReadFromJsonAsync<AccessTokenResponse>(ct);

            if (result?.AccessToken != null)
            {
                gitHubAccessToken = result.AccessToken;
                await SecureStorage.Default.SetAsync(SecureStorageKey, gitHubAccessToken);
                return true;
            }

            if (result?.Error == "expired_token")
                return false;

            // "authorization_pending" or "slow_down" - keep polling
            if (result?.Error == "slow_down")
                intervalSeconds += 5;
        }

        return false;
    }

    public async Task<string> GetCopilotTokenAsync(CancellationToken ct = default)
    {
        if (copilotToken != null && DateTimeOffset.UtcNow < copilotTokenExpiry.AddMinutes(-2))
            return copilotToken;

        if (gitHubAccessToken == null)
            throw new InvalidOperationException("Not authenticated. Complete the device flow first.");

        var request = new HttpRequestMessage(HttpMethod.Get, CopilotTokenUrl);
        request.Headers.Authorization = new("token", gitHubAccessToken);
        request.Headers.UserAgent.ParseAdd("GitHubCopilotChat/0.1");

        var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CopilotTokenResponse>(ct);
        copilotToken = result!.Token;
        copilotTokenExpiry = DateTimeOffset.FromUnixTimeSeconds(result.ExpiresAt);

        return copilotToken;
    }

    public void Logout()
    {
        gitHubAccessToken = null;
        copilotToken = null;
        SecureStorage.Default.Remove(SecureStorageKey);
    }
}

public record DeviceCodeResponse(
    [property: JsonPropertyName("device_code")] string DeviceCode,
    [property: JsonPropertyName("user_code")] string UserCode,
    [property: JsonPropertyName("verification_uri")] string VerificationUri,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("interval")] int Interval
);

public record AccessTokenResponse(
    [property: JsonPropertyName("access_token")] string? AccessToken,
    [property: JsonPropertyName("error")] string? Error
);

public record CopilotTokenResponse(
    [property: JsonPropertyName("token")] string Token,
    [property: JsonPropertyName("expires_at")] long ExpiresAt
);
