using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Behavior;
using MinecraftProtoNet.Baritone.Api.Process;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Core.Commands;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Core.Abstractions;
using MinecraftProtoNet.Core.Dtos;
using MinecraftProtoNet.Core.Models.Core;
using MinecraftProtoNet.Core.Services;
using MinecraftProtoNet.Core.State.Base;
using System.Collections.Concurrent;

namespace Bot.Webcore.Services;

/// <summary>
/// Thin wrapper around MinecraftClient for Blazor UI consumption.
/// Contains NO Minecraft business logic - only exposes core services for UI binding.
/// </summary>
public class BotService : IDisposable
{
    private readonly IMinecraftClient _client;
    private readonly IBaritoneProvider _baritoneProvider;
    private readonly System.Timers.Timer? _refreshTimer;

    public event Action? OnStateChanged;

    /// <summary>
    /// Pending chat messages redirected for review.
    /// </summary>
    public ConcurrentQueue<ChatRedirectRequest> PendingRedirectedChat { get; } = new();

    /// <summary>
    /// Current sign editor state. Non-null when a sign editor is open in the UI.
    /// </summary>
    public SignEditorState? CurrentSignEditor { get; set; }

    public BotService(
        IMinecraftClient client,
        ClientState state,
        IItemRegistryService itemRegistry,
        CommandRegistry commandRegistry,
        IInventoryManager inventoryManager,
        IBaritoneProvider baritoneProvider,
        IContainerManager containerManager,
        ISignEventBus signEventBus)
    {
        _client = client;
        _baritoneProvider = baritoneProvider;
        State = state;
        ItemRegistry = itemRegistry;
        CommandRegistry = commandRegistry;
        InventoryManager = inventoryManager;
        ContainerManager = containerManager;

        // Subscribe to sign editor events for UI display
        signEventBus.OnSignEditorOpened += HandleSignEditorOpened;

        // Listen for disconnect events to update UI
        _client.OnDisconnected += (_, _) => NotifyStateChanged();
        
        // Setup periodic refresh for live updates (fallback if events are missed)
        _refreshTimer = new System.Timers.Timer(1000); 
        _refreshTimer.Elapsed += (_, _) => NotifyStateChanged();
        _refreshTimer.AutoReset = true;

        // Subscribe to real-time events
        if (state.LocalPlayer.Entity != null)
        {
            state.LocalPlayer.Entity.Inventory.OnInventoryChanged += NotifyStateChanged;
            state.LocalPlayer.Entity.OnStatsChanged += NotifyStateChanged;
        }
        
        if (state.Level != null)
        {
            state.Level.OnPlayersChanged += NotifyStateChanged;
        }

        // Container events
        if (containerManager != null)
        {
            containerManager.OnContainerOpened += _ => NotifyStateChanged();
            containerManager.OnContainerClosed += NotifyStateChanged;
        }

        // Ensure Baritone is initialized for this client early
        _baritoneProvider.CreateBaritone(_client);
    }

    /// <summary>
    /// Adds a redirected chat message to the pending queue.
    /// </summary>
    public void AddRedirectedChat(ChatRedirectRequest request)
    {
        PendingRedirectedChat.Enqueue(request);
        NotifyStateChanged();
    }

    /// <summary>
    /// Sends a redirected chat message to the server (manual override).
    /// </summary>
    public async Task SendRedirectedChatAsync(ChatRedirectRequest request)
    {
        // To avoid infinite recursion, we temporarily disable redirection or call a direct method
        var previousRedirect = State.BotSettings.RedirectChat;
        try
        {
            State.BotSettings.RedirectChat = false;
            await _client.SendChatMessageAsync(request.Message);
        }
        finally
        {
            State.BotSettings.RedirectChat = previousRedirect;
        }
    }

    /// <summary>
    /// Clears a message from the pending queue.
    /// </summary>
    public void DismissRedirectedChat(ChatRedirectRequest request)
    {
        // Simplified dismissal - in a real app we might use IDs
        var remaining = PendingRedirectedChat.Where(x => x != request).ToList();
        PendingRedirectedChat.Clear();
        foreach (var msg in remaining) PendingRedirectedChat.Enqueue(msg);
        NotifyStateChanged();
    }

    // Delegate to core client
    public bool IsConnected => _client.IsConnected;
    public bool IsAuthenticated { get; private set; }
    
    // Expose read-only state for UI binding
    public ClientState State { get; }
    public IItemRegistryService ItemRegistry { get; }
    public CommandRegistry CommandRegistry { get; }
    public IInventoryManager InventoryManager { get; }
    public IContainerManager ContainerManager { get; }
    
    /// <summary>
    /// Gets the Baritone follow process for UI binding.
    /// Returns null if Baritone is not available.
    /// </summary>
    public IFollowProcess? FollowProcess
    {
        get
        {
            try
            {
                return _baritoneProvider.CreateBaritone(_client).GetFollowProcess();
            }
            catch { return null; }
        }
    }

    /// <summary>
    /// Gets the Baritone custom goal process for UI binding.
    /// Returns null if Baritone is not available.
    /// </summary>
    public ICustomGoalProcess? CustomGoalProcess
    {
        get
        {
            try
            {
                return _baritoneProvider.CreateBaritone(_client).GetCustomGoalProcess();
            }
            catch { return null; }
        }
    }

    /// <summary>
    /// Gets the Baritone pathing behavior for UI binding.
    /// Returns null if Baritone is not available.
    /// </summary>
    public IPathingBehavior? PathingBehavior
    {
        get
        {
            try
            {
                return _baritoneProvider.CreateBaritone(_client).GetPathingBehavior();
            }
            catch { return null; }
        }
    }
    
    // Expose client for command execution
    public IMinecraftClient Client => _client;
    
    // Connection settings
    public string ServerAddress { get; set; } = "10.10.1.20";
    public int ServerPort { get; set; } = 25565;

    public async Task<bool> AuthenticateAsync()
    {
        IsAuthenticated = await _client.AuthenticateAsync();
        NotifyStateChanged();
        return IsAuthenticated;
    }

    public async Task<bool> ConnectAsync()
    {
        if (!IsAuthenticated)
        {
            var authResult = await AuthenticateAsync();
            if (!authResult) return false;
        }

        // Retry logic for Mojang session propagation race ("unverified_username")
        const int maxRetries = 3;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            await _client.ConnectAsync(ServerAddress, ServerPort, true);

            if (IsConnected)
            {
                _refreshTimer?.Start();
                NotifyStateChanged();
                return true;
            }

            // If not connected after ConnectAsync, the server likely disconnected us during login.
            // Wait before retrying to allow Mojang session propagation.
            if (attempt < maxRetries)
            {
                var delayMs = attempt * 2000; // 2s, 4s
                await Task.Delay(delayMs);
            }
        }

        NotifyStateChanged();
        return IsConnected;
    }

    public async Task DisconnectAsync()
    {
        _refreshTimer?.Stop();
        await _client.DisconnectAsync();
        NotifyStateChanged();
    }

    private Task HandleSignEditorOpened(SignEditorEventArgs args)
    {
        // Don't open UI if another subscriber already handled it (e.g., Bazaar auto-fill)
        if (args.Handled) return Task.CompletedTask;

        CurrentSignEditor = new SignEditorState
        {
            Position = args.Position,
            IsFrontText = args.IsFrontText,
            Lines = [
                args.ExistingLines.ElementAtOrDefault(0) ?? "",
                args.ExistingLines.ElementAtOrDefault(1) ?? "",
                args.ExistingLines.ElementAtOrDefault(2) ?? "",
                args.ExistingLines.ElementAtOrDefault(3) ?? ""
            ]
        };
        NotifyStateChanged();
        return Task.CompletedTask;
    }

    public void NotifyStateChanged() => OnStateChanged?.Invoke();

    public void Dispose()
    {
        _refreshTimer?.Dispose();
        
        // Unsubscribe from real-time events
        if (State.LocalPlayer.Entity != null)
        {
            State.LocalPlayer.Entity.Inventory.OnInventoryChanged -= NotifyStateChanged;
            State.LocalPlayer.Entity.OnStatsChanged -= NotifyStateChanged;
        }

        if (State.Level != null)
        {
            State.Level.OnPlayersChanged -= NotifyStateChanged;
        }
    }
}

/// <summary>
/// Tracks the state of an open sign editor for the Blazor UI.
/// </summary>
public class SignEditorState
{
    public required Vector3<int> Position { get; init; }
    public bool IsFrontText { get; set; }
    public string[] Lines { get; set; } = ["", "", "", ""];
}

