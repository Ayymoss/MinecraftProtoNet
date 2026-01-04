using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Identity.Client;
using MinecraftProtoNet.Core.Auth.Authenticators;
using MinecraftProtoNet.Core.Auth.Dtos;
using MinecraftProtoNet.Core.Auth.Managers;
using MinecraftProtoNet.Core.Auth.Utilities;
using Serilog;
using Serilog.Events;

namespace MinecraftProtoNet.Core.Auth;

public static class AuthenticationFlow
{
    private static readonly string ConfigDirectory = Path.Combine(AppContext.BaseDirectory, "Configuration");

    public static async Task<AuthResult?> AuthenticateAsync()
    {
        if (!_loggingRegistered)
        {
            RegisterLogging();
        }

        var msAuth = new MicrosoftAuthenticator();
        var xboxAuth = new XboxAuthenticator();
        var mcAuth = new MinecraftAuthenticator();

        try
        {
            // 1. Microsoft Authentication (Handles silent/interactive and caching)
            var msAuthResult = await msAuth.AuthenticateAsync();
            if (msAuthResult == null || string.IsNullOrEmpty(msAuthResult.AccessToken))
            {
                return null;
            }

            // 2. Xbox Live Authentication
            var xblResult = await xboxAuth.GetXblTokenAsync(msAuthResult.AccessToken);
            if (xblResult == null || string.IsNullOrEmpty(xblResult.Token))
            {
                Log.Error("Authentication Error: Failed to obtain Xbox Live token");
                return null;
            }

            // 3. XSTS Authentication
            var xstsResult = await xboxAuth.GetXstsTokenAsync(xblResult.Token);
            if (xstsResult == null || string.IsNullOrEmpty(xstsResult.Token))
            {
                Log.Error("Authentication Error: Failed to obtain XSTS token");
                return null;
            }

            var userHash = XboxAuthenticator.GetUserHash(xstsResult);
            if (string.IsNullOrEmpty(userHash))
            {
                Log.Error("Authentication Error: Failed to extract UserHash from XSTS token");
                return null;
            }

            // 4. Minecraft Services Login
            var mcLoginResult = await mcAuth.LoginWithXboxAsync(userHash, xstsResult.Token);
            if (mcLoginResult == null || string.IsNullOrEmpty(mcLoginResult.AccessToken))
            {
                Log.Error("Authentication Error: Failed to log in to Minecraft services");
                return null;
            }

            // 5. Get Minecraft Profile (UUID and correct username)
            var mcProfile = await mcAuth.GetMinecraftProfileAsync(mcLoginResult.AccessToken);
            if (mcProfile == null)
            {
                Log.Error("Authentication Error: Failed to retrieve Minecraft profile");
                return null;
            }

            var initialAuthResult = new AuthResult(mcProfile.Name, new Guid(mcProfile.Id), mcLoginResult.AccessToken, null, null);
            var keyManager = new PlayerKeyManager(initialAuthResult, ConfigDirectory);
            var (privateKey, sessionInfo) = await keyManager.EnsureKeysAsync();

            if (privateKey == null || sessionInfo == null)
            {
                Log.Error("Authentication Error: Failed to ensure player profile keys or retrieve chat session info");
                return initialAuthResult;
            }

            return new AuthResult(mcProfile.Name, new Guid(mcProfile.Id), mcLoginResult.AccessToken, privateKey, sessionInfo);
        }
        catch (MsalException msalEx)
        {
            Log.Error("Microsoft Authentication Error: {MsalExMessage}", msalEx.Message);
            return null;
        }
        catch (HttpRequestException httpEx)
        {
            Log.Error("Network Error during authentication: {HttpExMessage}", httpEx.Message);
            return null;
        }
        catch (JsonException jsonEx)
        {
            Log.Error("Data Parsing Error during authentication: {JsonExMessage}", jsonEx.Message);
            return null;
        }
        catch (Exception ex)
        {
            Log.Error("An unexpected error occurred during authentication: {ExMessage}", ex.Message);
            return null;
        }
    }


    private static bool _loggingRegistered = false;

    private static void RegisterLogging()
    {
        Log.Logger = new LoggerConfiguration()
#if DEBUG
            .MinimumLevel.Information()
            .MinimumLevel.Override("MinecraftProtoNet.Core.Auth", LogEventLevel.Debug)
#else
            .MinimumLevel.Warning()
            .MinimumLevel.Override("MinecraftProtoNet.Core.Auth", LogEventLevel.Information)
#endif
            .Enrich.FromLogContext()
            .Enrich.With<ShortSourceContextEnricher>()
            .WriteTo.Console()
            .CreateLogger();
        _loggingRegistered = true;
    }
}
