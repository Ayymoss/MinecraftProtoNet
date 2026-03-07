using System.Globalization;
using System.Text.RegularExpressions;

namespace MinecraftProtoNet.Bazaar.Services;

/// <summary>
/// Parses Hypixel Bazaar system chat messages for order confirmations,
/// fill notifications, and error messages.
/// </summary>
public static partial class ChatMessageParser
{
    /// <summary>
    /// Attempts to parse a Bazaar-related message from system chat text parts.
    /// </summary>
    public static BazaarChatMessage? Parse(string? translateKey, List<string> textParts)
    {
        var fullText = string.Join("", textParts).Trim();
        if (string.IsNullOrEmpty(fullText))
            return null;

        // Buy Order placed confirmation
        // "[Bazaar] Buy Order Setup! 64x Enchanted Diamond at 1,234.5 coins each"
        var buyMatch = BuyOrderPlacedRegex().Match(fullText);
        if (buyMatch.Success)
        {
            return new BazaarChatMessage(
                Type: BazaarMessageType.BuyOrderPlaced,
                Quantity: int.Parse(buyMatch.Groups["qty"].Value, CultureInfo.InvariantCulture),
                ProductName: buyMatch.Groups["product"].Value,
                PricePerUnit: ParseCoinValue(buyMatch.Groups["price"].Value));
        }

        // Sell Offer placed confirmation
        // "[Bazaar] Sell Offer Setup! 64x Enchanted Diamond at 1,234.5 coins each"
        var sellMatch = SellOfferPlacedRegex().Match(fullText);
        if (sellMatch.Success)
        {
            return new BazaarChatMessage(
                Type: BazaarMessageType.SellOfferPlaced,
                Quantity: int.Parse(sellMatch.Groups["qty"].Value, CultureInfo.InvariantCulture),
                ProductName: sellMatch.Groups["product"].Value,
                PricePerUnit: ParseCoinValue(sellMatch.Groups["price"].Value));
        }

        // Order filled notification
        // "[Bazaar] Your Buy Order for 64x Enchanted Diamond was filled!"
        var buyFilledMatch = BuyOrderFilledRegex().Match(fullText);
        if (buyFilledMatch.Success)
        {
            return new BazaarChatMessage(
                Type: BazaarMessageType.BuyOrderFilled,
                Quantity: int.Parse(buyFilledMatch.Groups["qty"].Value, CultureInfo.InvariantCulture),
                ProductName: buyFilledMatch.Groups["product"].Value);
        }

        // Sell offer filled notification
        // "[Bazaar] Your Sell Offer for 64x Enchanted Diamond was filled!"
        var sellFilledMatch = SellOfferFilledRegex().Match(fullText);
        if (sellFilledMatch.Success)
        {
            return new BazaarChatMessage(
                Type: BazaarMessageType.SellOfferFilled,
                Quantity: int.Parse(sellFilledMatch.Groups["qty"].Value, CultureInfo.InvariantCulture),
                ProductName: sellFilledMatch.Groups["product"].Value);
        }

        // Order cancelled
        // "[Bazaar] Cancelled! Refunded 1,234.5 coins!"
        var cancelledMatch = OrderCancelledRegex().Match(fullText);
        if (cancelledMatch.Success)
        {
            return new BazaarChatMessage(
                Type: BazaarMessageType.OrderCancelled,
                TotalCoins: ParseCoinValue(cancelledMatch.Groups["coins"].Value));
        }

        // Coins claimed
        // "[Bazaar] Claimed 1,234.5 coins from selling ..."
        var claimedMatch = CoinsClaimedRegex().Match(fullText);
        if (claimedMatch.Success)
        {
            return new BazaarChatMessage(
                Type: BazaarMessageType.CoinsClaimed,
                TotalCoins: ParseCoinValue(claimedMatch.Groups["coins"].Value));
        }

        // Items claimed
        // "[Bazaar] Claimed 64x Enchanted Diamond worth ..."
        var itemsClaimedMatch = ItemsClaimedRegex().Match(fullText);
        if (itemsClaimedMatch.Success)
        {
            return new BazaarChatMessage(
                Type: BazaarMessageType.ItemsClaimed,
                Quantity: int.Parse(itemsClaimedMatch.Groups["qty"].Value, CultureInfo.InvariantCulture),
                ProductName: itemsClaimedMatch.Groups["product"].Value);
        }

        return null;
    }

    private static double ParseCoinValue(string value)
    {
        return double.Parse(value.Replace(",", ""), CultureInfo.InvariantCulture);
    }

    [GeneratedRegex(@"\[Bazaar\]\s*Buy Order Setup!\s*(?<qty>\d+)x\s+(?<product>.+?)\s+at\s+(?<price>[\d,.]+)\s+coins?\s+each", RegexOptions.IgnoreCase)]
    private static partial Regex BuyOrderPlacedRegex();

    [GeneratedRegex(@"\[Bazaar\]\s*Sell Offer Setup!\s*(?<qty>\d+)x\s+(?<product>.+?)\s+at\s+(?<price>[\d,.]+)\s+coins?\s+each", RegexOptions.IgnoreCase)]
    private static partial Regex SellOfferPlacedRegex();

    [GeneratedRegex(@"\[Bazaar\]\s*Your Buy Order for\s+(?<qty>\d+)x\s+(?<product>.+?)\s+was filled", RegexOptions.IgnoreCase)]
    private static partial Regex BuyOrderFilledRegex();

    [GeneratedRegex(@"\[Bazaar\]\s*Your Sell Offer for\s+(?<qty>\d+)x\s+(?<product>.+?)\s+was filled", RegexOptions.IgnoreCase)]
    private static partial Regex SellOfferFilledRegex();

    [GeneratedRegex(@"\[Bazaar\]\s*Cancelled!.*?Refunded\s+(?<coins>[\d,.]+)\s+coins", RegexOptions.IgnoreCase)]
    private static partial Regex OrderCancelledRegex();

    [GeneratedRegex(@"\[Bazaar\]\s*Claimed\s+(?<coins>[\d,.]+)\s+coins?\s+from\s+selling", RegexOptions.IgnoreCase)]
    private static partial Regex CoinsClaimedRegex();

    [GeneratedRegex(@"\[Bazaar\]\s*Claimed\s+(?<qty>\d+)x\s+(?<product>.+?)\s+worth", RegexOptions.IgnoreCase)]
    private static partial Regex ItemsClaimedRegex();
}

/// <summary>
/// Parsed Bazaar chat message with extracted fields.
/// </summary>
public record BazaarChatMessage(
    BazaarMessageType Type,
    int? Quantity = null,
    string? ProductName = null,
    double? PricePerUnit = null,
    double? TotalCoins = null
);

public enum BazaarMessageType
{
    BuyOrderPlaced,
    SellOfferPlaced,
    BuyOrderFilled,
    SellOfferFilled,
    OrderCancelled,
    CoinsClaimed,
    ItemsClaimed
}
