using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Core.Abstractions;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Core.Abstractions;
using MinecraftProtoNet.Core.Packets.Play.Serverbound;
using MinecraftProtoNet.Core.State;

namespace MinecraftProtoNet.Core.Services;

/// <summary>
/// Manages the main game loop that drives physics ticks at the server's tick rate.
/// </summary>
public class GameLoop(ILogger<GameLoop> logger, IHumanizer humanizer) : IGameLoop
{
    private CancellationTokenSource? _cts;
    private Thread? _gameLoopThread;
    private volatile bool _isRunning;

    public bool IsRunning => _isRunning;
    public event Action<Entity>? PhysicsTick;
    
    /// <summary>
    /// Event fired before each physics tick. External systems (e.g., Baritone) can subscribe to this.
    /// </summary>
    public event Action<IMinecraftClient>? PreTick;
    
    /// <summary>
    /// Event fired after each physics tick. External systems (e.g., Baritone) can subscribe to this.
    /// </summary>
    public event Action<IMinecraftClient>? PostTick;

    public void Start(IMinecraftClient client)
    {
        if (IsRunning)
        {
            logger.LogWarning("Game loop is already running");
            return;
        }

        var preTickCount = PreTick?.GetInvocationList().Length ?? 0;
        var postTickCount = PostTick?.GetInvocationList().Length ?? 0;
        logger.LogWarning("GameLoop.Start: Starting game loop (PreTick subscribers: {PreTickCount}, PostTick subscribers: {PostTickCount})", 
            preTickCount, postTickCount);

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _isRunning = true;
        _gameLoopThread = new Thread(async () =>
        {
            logger.LogInformation("Game loop started");
            var stopwatch = new Stopwatch();

            try
            {
                while (!token.IsCancellationRequested)
                {
                    stopwatch.Restart();

                    try
                    {
                        // Increment tick counter FIRST (matches vanilla Minecraft.java:1740)
                        client.State.Level.IncrementClientTickCounter();

                        // Invoke pre-tick hook for external systems (e.g., Baritone)
                        if (PreTick != null)
                        {
                            // Log occasionally to verify it's being called (every 100 ticks = ~5 seconds)
                            if (client.State.Level.ClientTickCounter % 100 == 0)
                            {
                                logger.LogWarning("GameLoop: Invoking PreTick event (subscribers: {Count}, tick: {Tick})",
                                    PreTick.GetInvocationList().Length, client.State.Level.ClientTickCounter);
                            }
                            PreTick.Invoke(client);
                        }

                        await client.PhysicsTickAsync(entity => PhysicsTick?.Invoke(entity));

                        // Invoke post-tick hook for external systems (e.g., Baritone)
                        if (PostTick != null)
                        {
                            logger.LogTrace("GameLoop: Invoking PostTick event (subscribers: {Count})", PostTick.GetInvocationList().Length);
                            PostTick.Invoke(client);
                        }

                        // Send ClientTickEndPacket at end of tick BEFORE sleep (matches vanilla Minecraft.java:1864)
                        await client.SendPacketAsync(new ClientTickEndPacket());
                    }
                    catch (ObjectDisposedException)
                    {
                        logger.LogWarning("Connection disposed during physics tick, stopping game loop");
                        break;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error in physics tick");
                    }

                    stopwatch.Stop();

                    var targetDelayMs = client.State.Level.TickInterval;
                    var processingTimeMs = stopwatch.ElapsedMilliseconds;
                    targetDelayMs = Math.Max(1, Math.Min(1000, targetDelayMs));

                    if (processingTimeMs < targetDelayMs)
                    {
                        var remainingDelayMs = targetDelayMs - processingTimeMs + humanizer.GetTickJitterMs();
                        remainingDelayMs = Math.Max(1, remainingDelayMs);
                        await Task.Delay(TimeSpan.FromMilliseconds(remainingDelayMs), token);
                    }
                    else
                    {
                        await Task.Yield();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
            }
            finally
            {
                _isRunning = false;
                logger.LogInformation("Game loop stopped");
            }
        })
        {
            Name = "GameLoop",
            IsBackground = true
        };

        _gameLoopThread.Start();
    }

    public async Task StopAsync()
    {
        if (_cts == null || !_isRunning)
        {
            return;
        }

        logger.LogInformation("Stopping game loop...");
        await _cts.CancelAsync();

        // Give the loop a moment to observe cancellation and exit
        await Task.Delay(200);

        _isRunning = false;
        _cts.Dispose();
        _cts = null;
        _gameLoopThread = null;
    }
}
