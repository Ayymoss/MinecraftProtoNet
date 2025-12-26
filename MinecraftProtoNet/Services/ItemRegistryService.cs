namespace MinecraftProtoNet.Services;

public class ItemRegistryService(IRegistryDataLoader dataLoader) : IItemRegistryService
{
    private Dictionary<int, string> _itemNames = new();
    
    // Set of common throwaway blocks for quick lookup
    private readonly HashSet<string> _throwawayBlockKeywords =
    [
        "dirt", "cobblestone", "stone", "netherrack", "diorite", "granite", "andesite",
        "sand", "gravel", "planks", "log", "wood", "leaves", "glass", "wool"
    ];

    public async Task InitializeAsync()
    {
        _itemNames = await dataLoader.LoadItemsAsync();
    }

    public string? GetItemName(int protocolId)
    {
        return _itemNames.GetValueOrDefault(protocolId);
    }

    public bool IsThrowawayBlock(int protocolId)
    {
        if (!_itemNames.TryGetValue(protocolId, out var name)) return false;
        
        // Remove namespace if present
        if (name.Contains(":"))
            name = name.Split(':')[1];
            
        // Check keywords
        foreach (var keyword in _throwawayBlockKeywords)
        {
            if (name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        
        return false;
    }
}
