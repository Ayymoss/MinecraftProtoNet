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

    /// <summary>
    /// Lists all Microsoft accounts currently cached in the token store.
    /// </summary>
    public async Task<IReadOnlyList<IAccount>> GetCachedAccountsAsync()
    {
        var accounts = await _pca.GetAccountsAsync();
        return accounts.ToList();
    }

    /// <summary>
    /// Silently acquires a token for the specified account.
    /// If <paramref name="homeAccountId"/> is null, falls back to the first cached account.
    /// Returns null if no cached account matches or if interactive login is required.
    /// </summary>
    public async Task<AuthenticationResult?> AuthenticateAsync(string? homeAccountId = null)
    {
        var accounts = await _pca.GetAccountsAsync();
        IAccount? account = null;

        if (!string.IsNullOrEmpty(homeAccountId))
        {
            account = accounts.FirstOrDefault(a => a.HomeAccountId.Identifier == homeAccountId);
            if (account == null)
            {
                Log.Error("No cached MSAL account matches HomeAccountId {HomeAccountId}", homeAccountId);
                return null;
            }
        }
        else
        {
            account = accounts.FirstOrDefault();
            if (account == null)
            {
                Log.Error("No cached MSAL accounts exist. Add an account via the web UI before connecting.");
                return null;
            }
        }

        try
        {
            return await _pca.AcquireTokenSilent(Scopes, account).ExecuteAsync();
        }
        catch (MsalUiRequiredException)
        {
            Log.Error("Silent auth for {Username} requires interactive re-login. Re-add the account via the web UI.", account.Username);
            return null;
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

    /// <summary>
    /// Runs a device-code flow to add a new account. The callback receives the verification URL + user code
    /// so the UI can display them. Returns null on decline, timeout, or any error.
    /// </summary>
    public async Task<AuthenticationResult?> AddAccountViaDeviceCodeAsync(
        Func<DeviceCodeResult, Task> deviceCodeCallback,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _pca.AcquireTokenWithDeviceCode(Scopes, deviceCodeCallback)
                .ExecuteAsync(cancellationToken);
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
        catch (OperationCanceledException)
        {
            Log.Information("Device Code Flow cancelled by user");
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

    /// <summary>
    /// Removes a cached MSAL account, clearing its refresh tokens.
    /// </summary>
    public async Task<bool> RemoveAccountAsync(string homeAccountId)
    {
        var accounts = await _pca.GetAccountsAsync();
        var account = accounts.FirstOrDefault(a => a.HomeAccountId.Identifier == homeAccountId);
        if (account == null) return false;

        await _pca.RemoveAsync(account);
        return true;
    }
}
