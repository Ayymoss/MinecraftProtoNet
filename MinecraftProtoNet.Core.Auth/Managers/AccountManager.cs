using System.Text.Json;
using Microsoft.Identity.Client;
using MinecraftProtoNet.Core.Auth.Authenticators;
using MinecraftProtoNet.Core.Auth.Dtos;
using Serilog;

namespace MinecraftProtoNet.Core.Auth.Managers;

public class AccountManager : IAccountManager
{
    // MCPROTO_CONFIG_DIR lets a sibling process (e.g. the test harness) point at another binary's
    // Configuration dir to share the active account + token cache. Defaults to the per-binary path.
    private static readonly string ConfigDirectory =
        Environment.GetEnvironmentVariable("MCPROTO_CONFIG_DIR") ?? Path.Combine(AppContext.BaseDirectory, "Configuration");
    private static readonly string ActiveAccountPath = Path.Combine(ConfigDirectory, "active-account.json");

    private readonly MicrosoftAuthenticator _msAuth;
    private readonly SemaphoreSlim _persistLock = new(1, 1);

    public AccountManager(MicrosoftAuthenticator msAuth)
    {
        _msAuth = msAuth;
    }

    public async Task<IReadOnlyList<AccountInfo>> ListAccountsAsync()
    {
        var accounts = await _msAuth.GetCachedAccountsAsync();
        var active = await ReadActiveAccountIdAsync();

        return accounts
            .Select(a => new AccountInfo(
                HomeAccountId: a.HomeAccountId.Identifier,
                Username: a.Username ?? "(unknown)",
                IsActive: a.HomeAccountId.Identifier == active))
            .ToList();
    }

    public async Task<string?> GetActiveAccountIdAsync()
    {
        var persisted = await ReadActiveAccountIdAsync();
        if (string.IsNullOrEmpty(persisted)) return null;

        var accounts = await _msAuth.GetCachedAccountsAsync();
        if (accounts.All(a => a.HomeAccountId.Identifier != persisted))
        {
            Log.Warning("Active account {HomeAccountId} no longer exists in token cache; clearing", persisted);
            await ClearActiveAccountAsync();
            return null;
        }

        return persisted;
    }

    public async Task SetActiveAccountAsync(string homeAccountId)
    {
        var accounts = await _msAuth.GetCachedAccountsAsync();
        if (accounts.All(a => a.HomeAccountId.Identifier != homeAccountId))
        {
            throw new InvalidOperationException($"No cached account matches HomeAccountId '{homeAccountId}'");
        }

        await WriteActiveAccountIdAsync(homeAccountId);
    }

    public async Task ClearActiveAccountAsync()
    {
        await WriteActiveAccountIdAsync(null);
    }

    public async Task<bool> RemoveAccountAsync(string homeAccountId)
    {
        var removed = await _msAuth.RemoveAccountAsync(homeAccountId);
        if (!removed) return false;

        var active = await ReadActiveAccountIdAsync();
        if (active == homeAccountId)
        {
            await ClearActiveAccountAsync();
        }
        return true;
    }

    public async Task<AuthResult?> AddAccountAsync(Func<DeviceCodeResult, Task> deviceCodeCallback, CancellationToken ct = default)
    {
        var result = await AuthenticationFlow.AddAccountAsync(_msAuth, deviceCodeCallback, ct);
        if (result == null) return null;

        await WriteActiveAccountIdAsync(result.Value.homeAccountId);
        return result.Value.result;
    }

    private async Task<string?> ReadActiveAccountIdAsync()
    {
        try
        {
            if (!File.Exists(ActiveAccountPath)) return null;

            await using var stream = File.OpenRead(ActiveAccountPath);
            var doc = await JsonDocument.ParseAsync(stream);
            if (doc.RootElement.TryGetProperty("homeAccountId", out var prop) &&
                prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString();
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to read active-account.json");
        }
        return null;
    }

    private async Task WriteActiveAccountIdAsync(string? homeAccountId)
    {
        await _persistLock.WaitAsync();
        try
        {
            Directory.CreateDirectory(ConfigDirectory);
            var payload = JsonSerializer.Serialize(new { homeAccountId });
            await File.WriteAllTextAsync(ActiveAccountPath, payload);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to persist active-account.json");
        }
        finally
        {
            _persistLock.Release();
        }
    }
}
