using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Core.Abstractions;
using MinecraftProtoNet.Packets.Play.Serverbound;

namespace MinecraftProtoNet.Services;

/// <summary>
/// Manages the main game loop that drives physics ticks at the server's tick rate.
/// </summary>
public class GameLoop : IGameLoop
{
    private readonly ILogger<GameLoop> _logger;
    private CancellationTokenSource? _cts;
    private Thread? _gameLoopThread;

    public bool IsRunning => _gameLoopThread?.IsAlive ?? false;

    public GameLoop(ILogger<GameLoop> logger)
    {
        _logger = logger;
    }

    public void Start(IMinecraftClient client)
    {
        if (IsRunning)
        {
            _logger.LogWarning("Game loop is already running");
            return;
        }

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _gameLoopThread = new Thread(async () =>
        {
            _logger.LogInformation("Game loop started");
            var stopwatch = new Stopwatch();

            try
            {
                while (!token.IsCancellationRequested)
                {
                    stopwatch.Restart();

                    try
                    {
                        await client.PhysicsTickAsync();
                        client.State.Level.IncrementClientTickCounter();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in physics tick");
                    }

                    stopwatch.Stop();

                    var targetDelayMs = client.State.Level.TickInterval;
                    var processingTimeMs = stopwatch.ElapsedMilliseconds;
                    targetDelayMs = Math.Max(1, Math.Min(1000, targetDelayMs));

                    if (processingTimeMs < targetDelayMs)
                    {
                        var remainingDelayMs = targetDelayMs - processingTimeMs;
                        await Task.Delay(TimeSpan.FromMilliseconds(remainingDelayMs), token);
                    }
                    else
                    {
                        await Task.Yield();
                    }

                    await client.SendPacketAsync(new ClientTickEndPacket());
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
            }
            finally
            {
                _logger.LogInformation("Game loop stopped");
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
        if (_cts == null || !IsRunning)
        {
            return;
        }

        _logger.LogInformation("Stopping game loop...");
        await _cts.CancelAsync();
        
        // Give the thread a moment to clean up
        await Task.Delay(100);
        
        _cts.Dispose();
        _cts = null;
        _gameLoopThread = null;
    }
}
