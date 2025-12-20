using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MinecraftProtoNet.Auth.Dtos;
using MinecraftProtoNet.Auth.Utilities;
using Serilog;

namespace MinecraftProtoNet.Auth.Authenticators;

public class MinecraftAuthenticator
{
    private readonly HttpClient _httpClient = new();

    private record MinecraftLoginRequest(string IdentityToken);

    public class MinecraftLoginResponse
    {
        [JsonPropertyName("username")] string Username { get; init; }
        [JsonPropertyName("roles")] public string[] Roles { get; init; }
        [JsonPropertyName("access_token")] public string AccessToken { get; init; }
        [JsonPropertyName("token_type")] public string TokenType { get; init; }
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; init; }
    }

    public class MinecraftProfile
    {
        [JsonPropertyName("id")] public string Id { get; init; }
        [JsonPropertyName("name")] public string Name { get; init; }
    }

    public async Task<MinecraftLoginResponse?> LoginWithXboxAsync(string userHash, string xstsToken)
    {
        var requestPayload = new MinecraftLoginRequest($"XBL3.0 x={userHash};{xstsToken}");
        var jsonPayload = JsonSerializer.Serialize(requestPayload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Clear();

        try
        {
            var response = await _httpClient.PostAsync("https://api.minecraftservices.com/authentication/login_with_xbox", content);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<MinecraftLoginResponse>(responseBody);
        }
        catch (HttpRequestException ex)
        {
            Log.Error(ex, "Minecraft Login Error: {ExStatusCode}", ex.StatusCode);
            return null;
        }
        catch (JsonException ex)
        {
            Log.Error(ex, "Error parsing Minecraft Login response");
            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unexpected error during Minecraft Login");
            return null;
        }
    }

    public async Task<MinecraftProfile?> GetMinecraftProfileAsync(string minecraftAccessToken)
    {
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", minecraftAccessToken);

        try
        {
            var response = await _httpClient.GetAsync("https://api.minecraftservices.com/minecraft/profile");
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<MinecraftProfile>(responseBody);
        }
        catch (HttpRequestException ex)
        {
            Log.Error(ex, "Minecraft Profile Error: {ExStatusCode} (if 404 often means the Minecraft access token is invalid or expired)",
                ex.StatusCode);
            return null;
        }
        catch (JsonException ex)
        {
            Log.Error(ex, "Error parsing Minecraft Profile response: {ExMessage}", ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unexpected error during Minecraft Profile retrieval: {ExMessage}", ex.Message);
            return null;
        }
    }

    public static async Task<bool> AuthenticateWithMojangAsync(string serverId, byte[] sharedSecret, byte[] serverPublicKey,
        AuthResult authResult)
    {
        try
        {
            // Convert hash to Minecraft's hex string format
            var serverHash = CryptographyHelper.GetServerHash(serverId, sharedSecret, serverPublicKey);
            Log.Information("ServerHash: {ServerHash}", serverHash);

            // Send request to Mojang session server
            using var httpClient = new HttpClient();
            var requestData = new
            {
                accessToken = authResult.MinecraftAccessToken,
                selectedProfile = authResult.Uuid.ToString("N"), // UUID without hyphens for Mojang API
                serverId = serverHash
            };

            var jsonPayload = JsonSerializer.Serialize(requestData);
            Log.Information("Payload: {JsonPayload}", jsonPayload);
            var jsonContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync("https://sessionserver.mojang.com/session/minecraft/join", jsonContent);
            if (response.IsSuccessStatusCode) return response.IsSuccessStatusCode;
            var errorBody = await response.Content.ReadAsStringAsync();
            Log.Error("Mojang authentication failed. Status code: {StatusCode}. Error body: {ErrorBody}", response.StatusCode,
                errorBody);
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during Mojang authentication request");
            return false;
        }
    }
}
