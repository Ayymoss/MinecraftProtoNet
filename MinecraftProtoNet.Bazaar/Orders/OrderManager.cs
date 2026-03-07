using MinecraftProtoNet.Bazaar.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MinecraftProtoNet.Bazaar.Orders;

/// <summary>
/// Tracks all active and completed orders. Enforces order limits.
/// </summary>
public sealed class OrderManager(
    IOptions<BazaarTradingConfig> config,
    ILogger<OrderManager> logger)
{
    private readonly BazaarTradingConfig _config = config.Value;
    private readonly Dictionary<string, OrderRecord> _orders = new();
    private readonly Lock _lock = new();

    /// <summary>All tracked orders.</summary>
    public IReadOnlyList<OrderRecord> AllOrders
    {
        get { lock (_lock) return _orders.Values.ToList(); }
    }

    /// <summary>Active orders (not filled, not cancelled).</summary>
    public IReadOnlyList<OrderRecord> ActiveOrders
    {
        get
        {
            lock (_lock)
                return _orders.Values
                    .Where(o => o.Status is OrderStatus.Active or OrderStatus.Pending or OrderStatus.PartiallyFilled)
                    .ToList();
        }
    }

    public int ActiveBuyCount
    {
        get
        {
            lock (_lock)
                return _orders.Values.Count(o =>
                    o.Side == OrderSide.Buy &&
                    o.Status is OrderStatus.Active or OrderStatus.Pending or OrderStatus.PartiallyFilled);
        }
    }

    public int ActiveSellCount
    {
        get
        {
            lock (_lock)
                return _orders.Values.Count(o =>
                    o.Side == OrderSide.Sell &&
                    o.Status is OrderStatus.Active or OrderStatus.Pending or OrderStatus.PartiallyFilled);
        }
    }

    /// <summary>Whether we can place another buy order.</summary>
    public bool CanPlaceBuyOrder => ActiveBuyCount < _config.MaxBuyOrders
                                    && ActiveOrders.Count < _config.MaxConcurrentOrders;

    /// <summary>Whether we can place another sell offer.</summary>
    public bool CanPlaceSellOffer => ActiveSellCount < _config.MaxSellOffers
                                    && ActiveOrders.Count < _config.MaxConcurrentOrders;

    /// <summary>Adds an order to tracking.</summary>
    public void AddOrder(OrderRecord order)
    {
        lock (_lock)
        {
            _orders[order.Id] = order;
            logger.LogInformation("Order {OrderId} added: {Side} {Qty}x {Product} @ {Price}",
                order.Id, order.Side, order.Quantity, order.ProductName, order.PricePerUnit);
        }
    }

    /// <summary>Gets an order by ID.</summary>
    public OrderRecord? GetOrder(string orderId)
    {
        lock (_lock) return _orders.GetValueOrDefault(orderId);
    }

    /// <summary>Updates order status.</summary>
    public void UpdateStatus(string orderId, OrderStatus status)
    {
        lock (_lock)
        {
            if (_orders.TryGetValue(orderId, out var order))
            {
                var oldStatus = order.Status;
                order.Status = status;
                if (status == OrderStatus.Filled)
                    order.FilledAt = DateTime.UtcNow;

                logger.LogInformation("Order {OrderId} status: {Old} -> {New}",
                    orderId, oldStatus, status);
            }
        }
    }

    /// <summary>Removes a completed/cancelled order from tracking.</summary>
    public void RemoveOrder(string orderId)
    {
        lock (_lock) _orders.Remove(orderId);
    }

    /// <summary>Gets active buy orders for a specific product.</summary>
    public List<OrderRecord> GetActiveBuyOrdersForProduct(string productKey)
    {
        lock (_lock)
            return _orders.Values
                .Where(o => o.ProductKey == productKey
                            && o.Side == OrderSide.Buy
                            && o.Status is OrderStatus.Active or OrderStatus.Pending)
                .ToList();
    }

    /// <summary>Gets stale orders that may need walking.</summary>
    public List<OrderRecord> GetStaleOrders()
    {
        var threshold = DateTime.UtcNow - _config.OrderStaleThreshold;
        lock (_lock)
            return _orders.Values
                .Where(o => o.Status == OrderStatus.Active
                            && o.PlacedAt < threshold
                            && o.WalkCount < _config.MaxWalksPerOrder)
                .ToList();
    }
}
