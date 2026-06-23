using Microsoft.Identity.Client;
using MinecraftProtoNet.Core.Auth.Dtos;

namespace MinecraftProtoNet.Core.Auth.Managers;

/// <summary>
/// Manages the set of Microsoft accounts cached in the MSAL token store, plus the
/// "active" account that the bot will authenticate as on the next connect.
/// </summary>
public interface IAccountManager
{
    /// <summary>
    /// Lists all cached Microsoft accounts with their usernames and which one is marked active.
    /// </summary>
    Task<IReadOnlyList<AccountInfo>> ListAccountsAsync();

    /// <summary>
    /// Returns the HomeAccountId of the currently active account, or null if none is set
    /// or if the persisted id no longer matches a cached account.
    /// </summary>
    Task<string?> GetActiveAccountIdAsync();

    /// <summary>
    /// Sets the active account by HomeAccountId. Throws if the id does not match a cached account.
    /// </summary>
    Task SetActiveAccountAsync(string homeAccountId);

    /// <summary>
    /// Clears the active account selection.
    /// </summary>
    Task ClearActiveAccountAsync();

    /// <summary>
    /// Removes a cached account from the token store. Also clears the active selection
    /// if that account was the active one.
    /// </summary>
    Task<bool> RemoveAccountAsync(string homeAccountId);

    /// <summary>
    /// Runs a device-code flow to add a new account. The callback receives the verification URL
    /// and user code so the UI can render them. On success the new account is automatically
    /// marked active.
    /// </summary>
    Task<AuthResult?> AddAccountAsync(Func<DeviceCodeResult, Task> deviceCodeCallback, CancellationToken ct = default);
}
