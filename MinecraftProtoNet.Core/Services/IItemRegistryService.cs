namespace MinecraftProtoNet.Core.Services;

public interface IItemRegistryService
{
    Task InitializeAsync();
    string? GetItemName(int protocolId);
    bool IsThrowawayBlock(int protocolId);
}
