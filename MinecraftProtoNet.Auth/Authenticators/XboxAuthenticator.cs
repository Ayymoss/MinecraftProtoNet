using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Serilog;

namespace MinecraftProtoNet.Auth.Authenticators;

public class XboxAuthenticator
{
    private readonly HttpClient _httpClient = new();

    private record XblAuthRequestProperties(string AuthMethod = "RPS", string SiteName = "user.auth.xboxlive.com", string RpsTicket = "");

    private record XblAuthRequest(
        XblAuthRequestProperties Properties,
        string RelyingParty = "http://auth.xboxlive.com",
        string TokenType = "JWT");

    public record XboxAuthResponse(string IssueInstant, string NotAfter, string Token, JsonElement DisplayClaims);

    private record XstsAuthRequestProperties(string[]? UserTokens, string SandboxId = "RETAIL");

    private record XstsAuthRequest(
        XstsAuthRequestProperties Properties,
        string RelyingParty = "rp://api.minecraftservices.com/",
        string TokenType = "JWT");

    public async Task<XboxAuthResponse?> GetXblTokenAsync(string microsoftAccessToken)
    {
        var requestPayload = new XblAuthRequest(
            new XblAuthRequestProperties(RpsTicket: $"d={microsoftAccessToken}"),
            RelyingParty: "http://auth.xboxlive.com",
            TokenType: "JWT"
        );
        return await SendXboxRequestAsync<XboxAuthResponse>("https://user.auth.xboxlive.com/user/authenticate", requestPayload, "XBL");
    }

    public async Task<XboxAuthResponse?> GetXstsTokenAsync(string xblToken)
    {
        var requestPayload = new XstsAuthRequest(
            Properties: new XstsAuthRequestProperties([xblToken]),
            RelyingParty: "rp://api.minecraftservices.com/",
            TokenType: "JWT"
        );
        return await SendXboxRequestAsync<XboxAuthResponse>("https://xsts.auth.xboxlive.com/xsts/authorize", requestPayload, "XSTS");
    }

    private async Task<T?> SendXboxRequestAsync<T>(string url, object payload, string requestType) where T : class
    {
        try
        {
            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("x-xbl-contract-version", "1");
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.PostAsync(url, content);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                HandleXboxError(response.StatusCode, errorBody, requestType);
                return null;
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(responseBody);
        }
        catch (HttpRequestException ex)
        {
            Log.Error("Network Error during {RequestType} request: {ExMessage}", requestType, ex.Message);
            return null;
        }
        catch (JsonException ex)
        {
            Log.Error("Error parsing {RequestType} response: {ExMessage}", requestType, ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            Log.Error("Unexpected error during {RequestType} request: {ExMessage}", requestType, ex.Message);
            return null;
        }
    }

    private void HandleXboxError(System.Net.HttpStatusCode statusCode, string errorBody, string requestType)
    {
        Log.Error("{RequestType} Error ({StatusCode}). See details below", requestType, statusCode);
        try
        {
            using var jsonDoc = JsonDocument.Parse(errorBody);
            if (jsonDoc.RootElement.TryGetProperty("XErr", out var xErrElement) && xErrElement.ValueKind == JsonValueKind.Number)
            {
                var xErrCode = xErrElement.GetInt64();
                var commonError = xErrCode switch
                {
                    2148916233 => "User doesn't have an Xbox account or issue linking MSA.",
                    2148916235 => "Xbox Live service is unavailable or experiencing issues.",
                    2148916236 => "User must purchase the game (Minecraft).",
                    2148916237 => "Xbox Live service is unavailable or experiencing issues.",
                    2148916238 => "User is a child and needs parental consent / cannot access resource.",
                    _ => $"Unknown XErr code."
                };
                Log.Error("  XErr Code: {XErrCode} ({CommonError})", xErrCode, commonError);
            }

            if (jsonDoc.RootElement.TryGetProperty("Message", out var messageElement) && messageElement.ValueKind == JsonValueKind.String)
            {
                Log.Error("  Message: {S}", messageElement.GetString());
            }

            if (jsonDoc.RootElement.TryGetProperty("Redirect", out var redirectElement) &&
                redirectElement.ValueKind == JsonValueKind.String)
            {
                Log.Error("  Redirect URL: {S}", redirectElement.GetString());
            }
        }
        catch (JsonException)
        {
            Log.Error("  Raw Error Body: {ErrorBody}", errorBody);
        }
    }

    public static string? GetUserHash(XboxAuthResponse response)
    {
        if (response.DisplayClaims.TryGetProperty("xui", out var xuiElement) &&
            xuiElement.ValueKind == JsonValueKind.Array &&
            xuiElement.GetArrayLength() > 0)
        {
            var firstClaim = xuiElement[0];
            if (firstClaim.TryGetProperty("uhs", out var uhsElement) && uhsElement.ValueKind == JsonValueKind.String)
            {
                return uhsElement.GetString();
            }
        }

        return null;
    }
}
