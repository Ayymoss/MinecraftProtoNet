using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Behavior;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Core.Commands;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Services;
using MinecraftProtoNet.Core.State.Base;

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

    public BotService(
        IMinecraftClient client, 
        ClientState state, 
        IItemRegistryService itemRegistry,
        CommandRegistry commandRegistry,
        IInventoryManager inventoryManager,
        IBaritoneProvider baritoneProvider,
        IContainerManager containerManager)
    {
        _client = client;
        _baritoneProvider = baritoneProvider;
        State = state;
        ItemRegistry = itemRegistry;
        CommandRegistry = commandRegistry;
        InventoryManager = inventoryManager;
        ContainerManager = containerManager;
        
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
    /// Gets the Baritone pathing behavior for UI binding.
    /// Returns null if Baritone is not available.
    /// </summary>
    public IPathingBehavior? PathingBehavior
    {
        get
        {
            try
            {
                var baritones = _baritoneProvider.GetAllBaritones();
                if (baritones.Count == 0)
                {
                    return null;
                }
                return baritones[0].GetPathingBehavior();
            }
            catch
            {
                return null;
            }
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

        await _client.ConnectAsync(ServerAddress, ServerPort, true);
        _refreshTimer?.Start();
        NotifyStateChanged();
        return IsConnected;
    }

    public async Task DisconnectAsync()
    {
        _refreshTimer?.Stop();
        await _client.DisconnectAsync();
        NotifyStateChanged();
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

