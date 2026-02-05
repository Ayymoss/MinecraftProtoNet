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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/BaritoneProcessHelper.java
 */

using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Process;
using MinecraftProtoNet.Baritone.Api.Utils;

namespace MinecraftProtoNet.Baritone.Utils;

/// <summary>
/// Base helper class for Baritone processes.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/BaritoneProcessHelper.java
/// </summary>
public abstract class BaritoneProcessHelper : IBaritoneProcess
{
    protected readonly IBaritone Baritone;
    protected readonly IPlayerContext Ctx;

    protected BaritoneProcessHelper(IBaritone baritone)
    {
        Baritone = baritone;
        Ctx = baritone.GetPlayerContext();
    }

    public abstract bool IsActive();
    public abstract PathingCommand OnTick(bool calcFailed, bool isSafeToCancel);
    public abstract void OnLostControl();
    public virtual bool IsTemporary() => false;
    public virtual double Priority() => IBaritoneProcess.DefaultPriority;
    public abstract string DisplayName();

    // Helper methods for logging
    protected void LogDirect(string message)
    {
        Baritone.GetGameEventHandler().LogDirect(message);
    }

    protected void LogNotification(string message, bool logToChat)
    {
        Baritone.GetGameEventHandler().LogNotification(message, logToChat);
    }
}

