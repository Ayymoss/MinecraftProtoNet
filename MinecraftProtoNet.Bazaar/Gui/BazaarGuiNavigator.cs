using MinecraftProtoNet.Bazaar.Configuration;
using MinecraftProtoNet.Core.Abstractions;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Services;
using MinecraftProtoNet.Core.State;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MinecraftProtoNet.Bazaar.Gui;

/// <summary>
/// Drives Bazaar GUI interaction via ContainerManager clicks and chat commands.
/// Handles opening the Bazaar, navigating to products, placing orders, and claiming.
/// </summary>
public sealed class BazaarGuiNavigator : IDisposable
{
    private readonly IMinecraftClient _client;
    private readonly IContainerManager _containerManager;
    private readonly IHumanizer _humanizer;
    private readonly BazaarTradingConfig _config;
    private readonly ILogger<BazaarGuiNavigator> _logger;

    private TaskCompletionSource<ContainerState>? _containerOpenTcs;
    private BazaarGuiScreen _currentScreen = BazaarGuiScreen.Closed;

    public BazaarGuiScreen CurrentScreen => _currentScreen;

    public BazaarGuiNavigator(
        IMinecraftClient client,
        IContainerManager containerManager,
        IHumanizer humanizer,
        IOptions<BazaarTradingConfig> config,
        ILogger<BazaarGuiNavigator> logger)
    {
        _client = client;
        _containerManager = containerManager;
        _humanizer = humanizer;
        _config = config.Value;
        _logger = logger;

        _containerManager.OnContainerOpened += OnContainerOpened;
        _containerManager.OnContainerClosed += OnContainerClosed;
    }

    /// <summary>
    /// Opens the Bazaar main menu by sending /bazaar chat command.
    /// </summary>
    public async Task<bool> OpenBazaarAsync(CancellationToken ct = default)
    {
        if (_containerManager.IsContainerOpen)
        {
            await _containerManager.CloseContainerAsync();
            await Task.Delay(_humanizer.GetGuiClickDelayMs(), ct);
        }

        _containerOpenTcs = new TaskCompletionSource<ContainerState>(TaskCreationOptions.RunContinuationsAsynchronously);

        await _client.SendChatMessageAsync("/bazaar", ct);

        // Wait for container to open
        var opened = await WaitForContainerAsync(ct);
        if (opened)
        {
            _currentScreen = BazaarGuiScreen.MainMenu;
            _logger.LogDebug("Bazaar main menu opened");
        }
        else
        {
            _logger.LogWarning("Bazaar main menu did not open within timeout");
        }

        return opened;
    }

    /// <summary>
    /// Navigates to a product's detail page by searching for it.
    /// Assumes Bazaar main menu is already open.
    /// </summary>
    public async Task<bool> NavigateToProductAsync(string productKey, CancellationToken ct = default)
    {
        if (_currentScreen == BazaarGuiScreen.Closed)
        {
            _logger.LogWarning("Cannot navigate to product — Bazaar is not open");
            return false;
        }

        var container = _containerManager.CurrentContainer;
        if (container?.Slots is null)
            return false;

        // Look for search button (usually a sign/book item in the Bazaar GUI)
        var searchSlot = BazaarGuiReader.FindSlotByName(container.Slots, "Search");
        if (searchSlot < 0)
        {
            _logger.LogWarning("Search button not found in Bazaar GUI");
            return false;
        }

        // Click search
        await _containerManager.ClickSlotAsync(searchSlot);
        await Task.Delay(_config.GuiClickDelayMs, ct);

        // Type the product name in chat (Hypixel uses chat input for Bazaar search)
        await _client.SendChatMessageAsync(productKey, ct);

        // Wait for search results container
        _containerOpenTcs = new TaskCompletionSource<ContainerState>(TaskCreationOptions.RunContinuationsAsynchronously);
        var opened = await WaitForContainerAsync(ct);
        if (!opened)
        {
            _logger.LogWarning("Search results did not open");
            return false;
        }

        // Find and click the matching product in results
        container = _containerManager.CurrentContainer;
        if (container?.Slots is null)
            return false;

        var productSlot = BazaarGuiReader.FindSlotByName(container.Slots, productKey.Replace("_", " "));
        if (productSlot < 0)
        {
            _logger.LogWarning("Product {ProductKey} not found in search results", productKey);
            return false;
        }

        _containerOpenTcs = new TaskCompletionSource<ContainerState>(TaskCreationOptions.RunContinuationsAsynchronously);
        await _containerManager.ClickSlotAsync(productSlot);

        opened = await WaitForContainerAsync(ct);
        if (opened)
        {
            _currentScreen = BazaarGuiScreen.ProductDetail;
            _logger.LogDebug("Navigated to product {ProductKey}", productKey);
        }

        return opened;
    }

    /// <summary>
    /// Clicks a slot by finding it by name. Returns true if the click was performed.
    /// </summary>
    public async Task<bool> ClickSlotByNameAsync(string name, CancellationToken ct = default)
    {
        var container = _containerManager.CurrentContainer;
        if (container?.Slots is null)
            return false;

        var slot = BazaarGuiReader.FindSlotByName(container.Slots, name);
        if (slot < 0)
        {
            _logger.LogWarning("Slot with name '{Name}' not found", name);
            return false;
        }

        _containerOpenTcs = new TaskCompletionSource<ContainerState>(TaskCreationOptions.RunContinuationsAsynchronously);
        await _containerManager.ClickSlotAsync(slot);
        await Task.Delay(_config.GuiClickDelayMs, ct);
        return true;
    }

    /// <summary>
    /// Closes the Bazaar GUI.
    /// </summary>
    public async Task CloseBazaarAsync()
    {
        if (_containerManager.IsContainerOpen)
        {
            await _containerManager.CloseContainerAsync();
        }

        _currentScreen = BazaarGuiScreen.Closed;
    }

    private async Task<bool> WaitForContainerAsync(CancellationToken ct)
    {
        if (_containerOpenTcs is null)
            return false;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_config.ChatConfirmationTimeoutMs);

        try
        {
            await _containerOpenTcs.Task.WaitAsync(cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private void OnContainerOpened(ContainerState container)
    {
        _containerOpenTcs?.TrySetResult(container);
    }

    private void OnContainerClosed()
    {
        _currentScreen = BazaarGuiScreen.Closed;
    }

    public void Dispose()
    {
        _containerManager.OnContainerOpened -= OnContainerOpened;
        _containerManager.OnContainerClosed -= OnContainerClosed;
    }
}
