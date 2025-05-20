using MinecraftProtoNet.State;
using MinecraftProtoNet.State.Base;
using MinecraftProtoNet.Packets.Base;
using System;
using System.Threading.Tasks;

namespace MinecraftProtoNet.Core.Abstractions
{
    public interface IPhysicsService
    {
        Task PhysicsTickAsync(Entity entity, Level level, Func<IServerboundPacket, Task> sendPacketDelegate, Action<Entity> updatePathFollowingInput);
    }
}
