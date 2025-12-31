using MinecraftProtoNet.Commands;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Pathfinding;
using MinecraftProtoNet.Services;
using MinecraftProtoNet.State.Base;

namespace Bot_Web.Services;

/// <summary>
/// Thin wrapper around MinecraftClient for Blazor UI consumption.
/// Contains NO Minecraft business logic - only exposes core services for UI binding.
/// </summary>
public class BotService : IDisposable
{
    private readonly IMinecraftClient _client;
    private readonly System.Timers.Timer? _refreshTimer;

    public event Action? OnStateChanged;

    public BotService(
        IMinecraftClient client, 
        ClientState state, 
        IItemRegistryService itemRegistry,
        CommandRegistry commandRegistry,
        IInventoryManager inventoryManager,
        IPathingService pathingService)
    {
        _client = client;
        State = state;
        ItemRegistry = itemRegistry;
        CommandRegistry = commandRegistry;
        InventoryManager = inventoryManager;
        PathingService = pathingService;
        
        // Listen for disconnect events to update UI
        _client.OnDisconnected += (_, _) => NotifyStateChanged();
        
        // Setup periodic refresh for live updates (fallback if events are missed)
        _refreshTimer = new System.Timers.Timer(1000); 
        _refreshTimer.Elapsed += (_, _) => NotifyStateChanged();
        _refreshTimer.AutoReset = true;

        // Subscribe to real-time events
        state.LocalPlayer.Entity.Inventory.OnInventoryChanged += NotifyStateChanged;
        state.LocalPlayer.Entity.OnStatsChanged += NotifyStateChanged;
        state.Level.OnPlayersChanged += NotifyStateChanged;
        pathingService.OnStateChanged += NotifyStateChanged;
    }

    // Delegate to core client
    public bool IsConnected => _client.IsConnected;
    public bool IsAuthenticated { get; private set; }
    
    // Expose read-only state for UI binding
    public ClientState State { get; }
    public IItemRegistryService ItemRegistry { get; }
    public CommandRegistry CommandRegistry { get; }
    public IInventoryManager InventoryManager { get; }
    public IPathingService PathingService { get; }
    
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
        State.LocalPlayer.Entity.Inventory.OnInventoryChanged -= NotifyStateChanged;
        State.LocalPlayer.Entity.OnStatsChanged -= NotifyStateChanged;
        State.Level.OnPlayersChanged -= NotifyStateChanged;
        PathingService.OnStateChanged -= NotifyStateChanged;
    }
}
