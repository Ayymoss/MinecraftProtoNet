/*
 * This file is part of Baritone.
 *
 * Baritone is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * Baritone is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with Baritone.  If not, see <https://www.gnu.org/licenses/>.
 *
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/Baritone.java
 */

using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Behavior;
using MinecraftProtoNet.Baritone.Api.Cache;
using MinecraftProtoNet.Baritone.Api.Command.Manager;
using MinecraftProtoNet.Baritone.Api.Event.Listener;
using MinecraftProtoNet.Baritone.Api.Pathing.Calc;
using MinecraftProtoNet.Baritone.Api.Process;
using MinecraftProtoNet.Baritone.Api.Selection;
using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Baritone.Behaviors;
using MinecraftProtoNet.Baritone.Cache;
using MinecraftProtoNet.Baritone.Command.Manager;
using MinecraftProtoNet.Baritone.Events;
using MinecraftProtoNet.Baritone.Process;
using MinecraftProtoNet.Baritone.Selection;
using MinecraftProtoNet.Baritone.Utils;
using MinecraftProtoNet.Baritone.Utils.Player;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Services;

namespace MinecraftProtoNet.Baritone.Core;

/// <summary>
/// Main Baritone class.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/Baritone.java
/// </summary>
public class Baritone : IBaritone
{
    private static readonly ThreadPoolExecutor ThreadPool;
    private static IItemRegistryService? _itemRegistryService;

    static Baritone()
    {
        ThreadPool = new ThreadPoolExecutor(4, int.MaxValue, 60L, TimeUnit.Seconds, new SynchronousQueue<Runnable>());
    }

    /// <summary>
    /// Sets the item registry service for item lookups.
    /// Should be called during startup, similar to EntityInventory.SetRegistryService.
    /// </summary>
    public static void SetItemRegistryService(IItemRegistryService service)
    {
        _itemRegistryService = service;
    }

    private readonly IMinecraftClient _mc;
    private readonly string _directory;

    private readonly GameEventHandler _gameEventHandler;

    private readonly PathingBehavior _pathingBehavior;
    private readonly LookBehavior _lookBehavior;
    private readonly InventoryBehavior _inventoryBehavior;
    private readonly InputOverrideHandler _inputOverrideHandler;

    private readonly FollowProcess _followProcess;
    private readonly MineProcess _mineProcess;
    private readonly GetToBlockProcess _getToBlockProcess;
    private readonly CustomGoalProcess _customGoalProcess;
    private readonly BuilderProcess _builderProcess;
    private readonly ExploreProcess _exploreProcess;
    private readonly FarmProcess _farmProcess;
    private readonly InventoryPauserProcess _inventoryPauserProcess;
    private readonly IElytraProcess _elytraProcess;

    private readonly PathingControlManager _pathingControlManager;
    private readonly SelectionManager _selectionManager;
    private readonly CommandManager _commandManager;

    private readonly IPlayerContext _playerContext;
    private readonly WorldProvider _worldProvider;

    public BlockStateInterface? Bsi { get; set; }

    public Baritone(IMinecraftClient mc)
    {
        _mc = mc;
        _gameEventHandler = new GameEventHandler(this);

        // Create baritone directory
        _directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "baritone");
        if (!Directory.Exists(_directory))
        {
            try
            {
                Directory.CreateDirectory(_directory);
            }
            catch (Exception)
            {
                // Ignore
            }
        }

        // Define this before behaviors try and get it, or else it will be null and the builds will fail!
        _playerContext = new BaritonePlayerContext(this, mc);

        // Register behaviors
        _lookBehavior = RegisterBehavior(() => new LookBehavior(this));
        _pathingBehavior = RegisterBehavior(() => new PathingBehavior(this));
        _inventoryBehavior = RegisterBehavior(() => new InventoryBehavior(this));
        _inputOverrideHandler = RegisterBehavior(() => new InputOverrideHandler(this));
        RegisterBehavior(() => new WaypointBehavior(this));

        _pathingControlManager = new PathingControlManager(this);

        // Register processes
        _followProcess = RegisterProcess(() => new FollowProcess(this));
        _mineProcess = RegisterProcess(() => new MineProcess(this));
        _customGoalProcess = RegisterProcess(() => new CustomGoalProcess(this));
        _getToBlockProcess = RegisterProcess(() => new GetToBlockProcess(this));
        _builderProcess = RegisterProcess(() => new BuilderProcess(this));
        _exploreProcess = RegisterProcess(() => new ExploreProcess(this));
        _farmProcess = RegisterProcess(() => new FarmProcess(this));
        _inventoryPauserProcess = RegisterProcess(() => new InventoryPauserProcess(this));
        _elytraProcess = RegisterProcess(() => new ElytraProcess(this));
        RegisterProcess(() => new BackfillProcess(this));

        _worldProvider = new WorldProvider(this);
        _selectionManager = new SelectionManager(this);
        _commandManager = new CommandManager(this);
    }

    public void RegisterBehavior(IBehavior behavior)
    {
        _gameEventHandler.RegisterEventListener(behavior);
    }

    public T RegisterBehavior<T>(Func<T> constructor) where T : IBehavior
    {
        var behavior = constructor();
        RegisterBehavior(behavior);
        return behavior;
    }

    public T RegisterProcess<T>(Func<T> constructor) where T : IBaritoneProcess
    {
        var process = constructor();
        _pathingControlManager.RegisterProcess(process);
        return process;
    }

    public IPathingControlManager GetPathingControlManager() => _pathingControlManager;

    public IInputOverrideHandler GetInputOverrideHandler() => _inputOverrideHandler;

    public ICustomGoalProcess GetCustomGoalProcess() => _customGoalProcess;

    public IGetToBlockProcess GetGetToBlockProcess() => _getToBlockProcess;

    public IPlayerContext GetPlayerContext() => _playerContext;

    public IFollowProcess GetFollowProcess() => _followProcess;

    public IBuilderProcess GetBuilderProcess() => _builderProcess;

    public IInventoryBehavior GetInventoryBehavior() => _inventoryBehavior;

    public ILookBehavior GetLookBehavior() => _lookBehavior;

    public IExploreProcess GetExploreProcess() => _exploreProcess;

    public IMineProcess GetMineProcess() => _mineProcess;

    public IFarmProcess GetFarmProcess() => _farmProcess;

    public InventoryPauserProcess GetInventoryPauserProcess() => _inventoryPauserProcess;

    public IPathingBehavior GetPathingBehavior() => _pathingBehavior;

    public ISelectionManager GetSelectionManager() => _selectionManager;

    public IWorldProvider GetWorldProvider() => _worldProvider;

    public IEventBus GetGameEventHandler() => _gameEventHandler;

    public ICommandManager GetCommandManager() => _commandManager;

    public IElytraProcess GetElytraProcess() => _elytraProcess;

    public string GetDirectory() => _directory;

    public static Settings.Settings Settings()
    {
        return BaritoneAPI.GetSettings();
    }

    public static Executor GetExecutor()
    {
        return ThreadPool;
    }

    public void OpenClick()
    {
        // GUI not implemented for headless client
    }

    public IItemRegistryService GetItemRegistryService()
    {
        if (_itemRegistryService == null)
        {
            throw new InvalidOperationException("ItemRegistryService has not been set. Call Baritone.SetItemRegistryService() during startup.");
        }
        return _itemRegistryService;
    }
}

/// <summary>
/// Simple executor interface for compatibility.
/// </summary>
public interface Executor
{
    void Execute(Runnable command);
}

/// <summary>
/// Runnable interface for compatibility.
/// </summary>
public interface Runnable
{
    void Run();
}

/// <summary>
/// Thread pool executor implementation.
/// </summary>
internal class ThreadPoolExecutor : Executor
{
    private readonly int _corePoolSize;
    private readonly int _maximumPoolSize;
    private readonly long _keepAliveTime;
    private readonly TimeUnit _unit;
    private readonly SynchronousQueue<Runnable> _workQueue;

    public ThreadPoolExecutor(int corePoolSize, int maximumPoolSize, long keepAliveTime, TimeUnit unit, SynchronousQueue<Runnable> workQueue)
    {
        _corePoolSize = corePoolSize;
        _maximumPoolSize = maximumPoolSize;
        _keepAliveTime = keepAliveTime;
        _unit = unit;
        _workQueue = workQueue;
    }

    public void Execute(Runnable command)
    {
        Task.Run(() => command.Run());
    }
}

/// <summary>
/// Synchronous queue implementation.
/// </summary>
internal class SynchronousQueue<T>
{
    // Simple wrapper for now - actual implementation would be more complex
}

/// <summary>
/// Time unit enum.
/// </summary>
public enum TimeUnit
{
    Seconds,
    Minutes,
    Hours,
    Days
}

