using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using Serilog;

namespace MinecraftProtoNet.Core.Auth.Authenticators;

public class MicrosoftAuthenticator
{
    private const string ClientId = "499c8d36-be2a-4231-9ebd-ef291b7bb64c";
    private readonly IPublicClientApplication _pca;
    private static readonly string[] Scopes = ["XboxLive.signin", "offline_access"];

    private const string CacheFileName = "MinecraftAuthCache.dat";
    private static readonly string CacheDir = Path.Combine(AppContext.BaseDirectory, "Configuration");

    public MicrosoftAuthenticator()
    {
        _pca = PublicClientApplicationBuilder.Create(ClientId)
            .WithAuthority(AzureCloudInstance.AzurePublic, "consumers")
            .WithRedirectUri("https://login.microsoftonline.com/common/oauth2/nativeclient")
            .Build();
        RegisterCache();
    }

    private void RegisterCache()
    {
        try
        {
            var storageProperties =
                new StorageCreationPropertiesBuilder(CacheFileName, CacheDir).Build();

            Directory.CreateDirectory(storageProperties.CacheDirectory);

            var cacheHelper = MsalCacheHelper.CreateAsync(storageProperties).GetAwaiter().GetResult();
            cacheHelper.RegisterCache(_pca.UserTokenCache);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to register token cache. Persistence may not work");
        }
    }

    public async Task<AuthenticationResult?> AuthenticateAsync()
    {
        var account = (await _pca.GetAccountsAsync()).FirstOrDefault();

        try
        {
            return await _pca.AcquireTokenSilent(Scopes, account).ExecuteAsync();
        }
        catch (MsalUiRequiredException)
        {
            try
            {
                return await _pca.AcquireTokenWithDeviceCode(Scopes, deviceCodeResult =>
                {
                    Console.WriteLine("---------------------------------------------------------------------------");
                    Console.WriteLine("Microsoft Authentication Needed:");
                    Console.WriteLine($" Please go to: {deviceCodeResult.VerificationUrl}");
                    Console.WriteLine($" Enter code:   {deviceCodeResult.UserCode}");
                    Console.WriteLine("---------------------------------------------------------------------------");
                    return Task.FromResult(0);
                }).ExecuteAsync();
            }
            catch (MsalServiceException msalEx) when (msalEx.Message.Contains("DeviceCodeAuthorizationDeclined"))
            {
                Log.Error("Device Code Flow Error: Authorization was declined by the user");
                return null;
            }
            catch (MsalServiceException msalEx) when (msalEx.Message.Contains("DeviceCodeTimeout"))
            {
                Log.Error("Device Code Flow Error: Timed out waiting for user authentication");
                return null;
            }
            catch (MsalException msalEx)
            {
                Log.Error("Microsoft Authentication Error (Device Code Flow): {MsalExMessage}", msalEx.Message);
                return null;
            }
            catch (Exception ex)
            {
                Log.Error("Unexpected Error during Device Code Flow: {ExMessage}", ex.Message);
                return null;
            }
        }
        catch (MsalException msalEx)
        {
            Log.Error("Microsoft Authentication Error (Silent): {MsalExMessage}", msalEx.Message);
            return null;
        }
        catch (Exception ex)
        {
            Log.Error("Unexpected Error during Silent Authentication: {ExMessage}", ex.Message);
            return null;
        }
    }
}
