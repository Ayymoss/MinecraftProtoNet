using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Behavior;
using MinecraftProtoNet.Baritone.Api.Process;
using MinecraftProtoNet.Bazaar.Engine;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Core.Commands;
using MinecraftProtoNet.Core.Configuration;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Core.Abstractions;
using MinecraftProtoNet.Core.Dtos;
using MinecraftProtoNet.Core.Models.Core;
using MinecraftProtoNet.Core.Services;
using MinecraftProtoNet.Core.State.Base;
using MinecraftProtoNet.Core.Utilities;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Bot.Webcore.Services;

/// <summary>
/// Thin wrapper around MinecraftClient for Blazor UI consumption.
/// Contains NO Minecraft business logic - only exposes core services for UI binding.
/// </summary>
public class BotService : IDisposable
{
    // Keywords in system chat that should trigger an immediate auto-disconnect on remote servers.
    // Kept intentionally narrow — anything that indicates punitive action (ban, mute, kick) or
    // rate-limit on our account. Match is case-insensitive, word-boundary where possible.
    private static readonly Regex BanKeywordPattern = new(
        @"\b(banned|muted|silenced|kicked|blacklist(ed)?|rate.?limit(ed)?|you are being throttled)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly IMinecraftClient _client;
    private readonly IBaritoneProvider _baritoneProvider;
    private readonly IChatEventBus _chatEventBus;
    private readonly HumanizerConfig _humanizerConfig;
    private readonly ILogger<BotService>? _logger;
    private readonly System.Timers.Timer? _refreshTimer;
    private int _panicInFlight;

    public event Action? OnStateChanged;

    /// <summary>Reason captured from the most recent panic stop, for UI display. Null when no recent panic.</summary>
    public string? LastPanicReason { get; private set; }
    public DateTimeOffset? LastPanicAt { get; private set; }

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
        ISignEventBus signEventBus,
        IChatEventBus chatEventBus,
        IOptions<HumanizerConfig> humanizerConfig,
        BazaarTradingEngine bazaarEngine,
        ILogger<BotService>? logger = null)
    {
        _client = client;
        _baritoneProvider = baritoneProvider;
        _chatEventBus = chatEventBus;
        _humanizerConfig = humanizerConfig.Value;
        _logger = logger;
        BazaarEngine = bazaarEngine;
        State = state;
        ItemRegistry = itemRegistry;
        CommandRegistry = commandRegistry;
        InventoryManager = inventoryManager;
        ContainerManager = containerManager;

        // Subscribe to sign editor events for UI display
        signEventBus.OnSignEditorOpened += HandleSignEditorOpened;

        // Listen for disconnect events to update UI
        _client.OnDisconnected += (_, _) => NotifyStateChanged();

        // Safety-guard breach → immediate panic (disconnect) on top of the engine halt.
        // Rationale: a halted engine leaves the bot logged in looking idle, which on a remote
        // server is the worst outcome after something already went wrong.
        BazaarEngine.OnHalted += reason =>
        {
            _ = PanicStopAsync($"Bazaar safety halt: {reason}");
        };

        // Watch system chat for ban/mute/kick keywords. Only reacts on remote servers so local
        // testing isn't disrupted by accidental keyword matches in dev server messages.
        _chatEventBus.OnSystemChat += OnSystemChatForBanKeywords;

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
    /// Immediately stops all bot activity: halts the Bazaar engine, then disconnects from the
    /// server. Idempotent — concurrent callers all observe the same stop.
    /// </summary>
    public async Task PanicStopAsync(string reason)
    {
        // Interlocked ensures only one call actually executes the stop sequence even if
        // multiple triggers (safety halt + ban keyword + manual button) fire in the same tick.
        if (Interlocked.Exchange(ref _panicInFlight, 1) != 0) return;

        try
        {
            LastPanicReason = reason;
            LastPanicAt = DateTimeOffset.UtcNow;
            _logger?.LogError("PANIC STOP: {Reason}", reason);

            try { BazaarEngine.Stop(); }
            catch (Exception ex) { _logger?.LogWarning(ex, "BazaarEngine.Stop threw during panic"); }

            if (_client.IsConnected)
            {
                try { await _client.DisconnectAsync(); }
                catch (Exception ex) { _logger?.LogWarning(ex, "DisconnectAsync threw during panic"); }
            }
        }
        finally
        {
            Interlocked.Exchange(ref _panicInFlight, 0);
            NotifyStateChanged();
        }
    }

    /// <summary>Clears the last panic reason from the UI after the user acknowledges it.</summary>
    public void AcknowledgePanic()
    {
        LastPanicReason = null;
        LastPanicAt = null;
        NotifyStateChanged();
    }

    /// <summary>Clears the last disconnect reason from the UI after the user acknowledges it.</summary>
    public void AcknowledgeDisconnect()
    {
        State.LastDisconnectReason = null;
        State.LastDisconnectTranslateKey = null;
        State.LastDisconnectAt = null;
        NotifyStateChanged();
    }

    // ===== Manual movement (web UI WASD panel) =====
    //
    // These helpers let the manual-drive panel toggle input flags + set rotation on the local
    // player entity. PhysicsService reads Entity.Input each tick, so setting flags here is enough
    // to produce movement — we don't send PlayerInputPacket ourselves.
    //
    // Caveat: Baritone's game-loop hook overwrites these flags when a pathing process is active.
    // For now the panel assumes Baritone is idle. A future improvement is a "manual control"
    // flag that suppresses Baritone's input-setting pass.

    public enum MovementKey
    {
        Forward,
        Backward,
        Left,
        Right,
        Jump,
        Sneak,
        Sprint
    }

    /// <summary>
    /// Sets or clears a single movement flag on the local player. No-op if there is no local entity.
    /// </summary>
    public void SetMovementKey(MovementKey key, bool pressed)
    {
        var entity = State.LocalPlayer.Entity;
        if (entity is null) return;

        switch (key)
        {
            case MovementKey.Forward:  entity.InputState.SetForward(pressed); break;
            case MovementKey.Backward: entity.InputState.SetBackward(pressed); break;
            case MovementKey.Left:     entity.InputState.SetLeft(pressed); break;
            case MovementKey.Right:    entity.InputState.SetRight(pressed); break;
            case MovementKey.Jump:     entity.InputState.SetJump(pressed); break;
            case MovementKey.Sneak:    entity.InputState.SetSneak(pressed); break;
            case MovementKey.Sprint:   entity.InputState.SetSprint(pressed); break;
        }
        NotifyStateChanged();
    }

    /// <summary>Clears every movement flag. Safety call when the panel loses focus.</summary>
    public void ClearMovement()
    {
        var entity = State.LocalPlayer.Entity;
        if (entity is null) return;
        entity.ClearMovementInput();
        NotifyStateChanged();
    }

    /// <summary>
    /// Sets the local player's look direction. Yaw wraps to [-180, 180); pitch clamps to [-90, 90].
    /// PhysicsService will include the new rotation on the next outgoing position packet.
    /// </summary>
    public void SetYawPitch(float yaw, float pitch)
    {
        var entity = State.LocalPlayer.Entity;
        if (entity is null) return;

        // Normalize yaw into [-180, 180) to match Minecraft's packet encoding.
        yaw = ((yaw + 180f) % 360f + 360f) % 360f - 180f;
        pitch = Math.Clamp(pitch, -90f, 90f);

        entity.YawPitch = new MinecraftProtoNet.Core.Models.Core.Vector2<float>(yaw, pitch);
        NotifyStateChanged();
    }

    /// <summary>
    /// Applies deltas to the current yaw/pitch. Useful for "nudge" buttons and mouse-drag look.
    /// </summary>
    public void AdjustYawPitch(float deltaYaw, float deltaPitch)
    {
        var entity = State.LocalPlayer.Entity;
        if (entity is null) return;
        SetYawPitch(entity.YawPitch.X + deltaYaw, entity.YawPitch.Y + deltaPitch);
    }

    private void OnSystemChatForBanKeywords(SystemChatEventArgs args)
    {
        // Only react on remote servers — local testing can trigger keywords in dev messages.
        if (!ServerClassification.IsRemote(State.ConnectedServerHost, _humanizerConfig.LocalNetworks))
            return;

        var text = args.TextParts != null && args.TextParts.Count > 0
            ? string.Join(" ", args.TextParts)
            : null;
        if (string.IsNullOrEmpty(text)) return;

        if (BanKeywordPattern.IsMatch(text))
        {
            _ = PanicStopAsync($"Ban/kick keyword in system chat: \"{text.Trim()}\"");
        }
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

    // Bazaar trading engine
    public BazaarTradingEngine BazaarEngine { get; }

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

        // Default chat redirection: on for remote servers, off for local development.
        // Applied before every connect so switching between local and Hypixel flips the default
        // automatically. The user can still override mid-session via the ChatReview toggle.
        State.BotSettings.RedirectChat = ServerClassification.IsRemote(ServerAddress, _humanizerConfig.LocalNetworks);

        // Retry logic for Mojang session propagation race ("unverified_username")
        const int maxRetries = 3;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            await _client.ConnectAsync(ServerAddress, ServerPort, false);

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
        _chatEventBus.OnSystemChat -= OnSystemChatForBanKeywords;

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

