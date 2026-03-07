namespace MinecraftProtoNet.Bazaar.Orders;

/// <summary>
/// Tracks a single Bazaar order (buy order or sell offer).
/// </summary>
public sealed class OrderRecord
{
    public required string Id { get; init; }
    public required string ProductKey { get; init; }
    public required string ProductName { get; init; }
    public required OrderSide Side { get; init; }
    public required double PricePerUnit { get; set; }
    public required int Quantity { get; init; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public DateTime PlacedAt { get; init; } = DateTime.UtcNow;
    public DateTime? FilledAt { get; set; }
    public int WalkCount { get; set; }
    public double OriginalPrice { get; init; }

    /// <summary>Total cost of this order (price * quantity).</summary>
    public double TotalCost => PricePerUnit * Quantity;
}

public enum OrderSide
{
    Buy,
    Sell
}

public enum OrderStatus
{
    Pending,
    Active,
    PartiallyFilled,
    Filled,
    Cancelled,
    Unknown
}
