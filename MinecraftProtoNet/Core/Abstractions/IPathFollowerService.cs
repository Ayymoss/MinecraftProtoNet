using MinecraftProtoNet.State.Base;
using MinecraftProtoNet.State;
using MinecraftProtoNet.Models.Core;

namespace MinecraftProtoNet.Core.Abstractions
{
    public interface IPathFollowerService
    {
        void Initialize(Level level);
        bool FollowPathTo(Entity entity, Vector3<double> target);
        void StopFollowingPath(Entity entity);
        void UpdatePathFollowingInput(Entity entity);
    }
}
