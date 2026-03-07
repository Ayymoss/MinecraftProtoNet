using FluentAssertions;
using MinecraftProtoNet.Bazaar.Services;

namespace MinecraftProtoNet.Tests.Bazaar;

public class ChatMessageParserTests
{
    [Fact]
    public void Parse_BuyOrderPlaced_ExtractsCorrectly()
    {
        var parts = new List<string> { "[Bazaar] Buy Order Setup! 64x Enchanted Diamond at 1,234.5 coins each" };
        var result = ChatMessageParser.Parse(null, parts);

        result.Should().NotBeNull();
        result!.Type.Should().Be(BazaarMessageType.BuyOrderPlaced);
        result.Quantity.Should().Be(64);
        result.ProductName.Should().Be("Enchanted Diamond");
        result.PricePerUnit.Should().Be(1234.5);
    }

    [Fact]
    public void Parse_SellOfferPlaced_ExtractsCorrectly()
    {
        var parts = new List<string> { "[Bazaar] Sell Offer Setup! 128x Enchanted Iron at 500 coins each" };
        var result = ChatMessageParser.Parse(null, parts);

        result.Should().NotBeNull();
        result!.Type.Should().Be(BazaarMessageType.SellOfferPlaced);
        result.Quantity.Should().Be(128);
        result.ProductName.Should().Be("Enchanted Iron");
        result.PricePerUnit.Should().Be(500);
    }

    [Fact]
    public void Parse_BuyOrderFilled_ExtractsCorrectly()
    {
        var parts = new List<string> { "[Bazaar] Your Buy Order for 64x Enchanted Diamond was filled!" };
        var result = ChatMessageParser.Parse(null, parts);

        result.Should().NotBeNull();
        result!.Type.Should().Be(BazaarMessageType.BuyOrderFilled);
        result.Quantity.Should().Be(64);
        result.ProductName.Should().Be("Enchanted Diamond");
    }

    [Fact]
    public void Parse_SellOfferFilled_ExtractsCorrectly()
    {
        var parts = new List<string> { "[Bazaar] Your Sell Offer for 32x Enchanted Gold was filled!" };
        var result = ChatMessageParser.Parse(null, parts);

        result.Should().NotBeNull();
        result!.Type.Should().Be(BazaarMessageType.SellOfferFilled);
        result.Quantity.Should().Be(32);
        result.ProductName.Should().Be("Enchanted Gold");
    }

    [Fact]
    public void Parse_OrderCancelled_ExtractsCoins()
    {
        var parts = new List<string> { "[Bazaar] Cancelled! Refunded 1,234.5 coins!" };
        var result = ChatMessageParser.Parse(null, parts);

        result.Should().NotBeNull();
        result!.Type.Should().Be(BazaarMessageType.OrderCancelled);
        result.TotalCoins.Should().Be(1234.5);
    }

    [Fact]
    public void Parse_CoinsClaimed_ExtractsCoins()
    {
        var parts = new List<string> { "[Bazaar] Claimed 50,000.3 coins from selling 64x Enchanted Diamond" };
        var result = ChatMessageParser.Parse(null, parts);

        result.Should().NotBeNull();
        result!.Type.Should().Be(BazaarMessageType.CoinsClaimed);
        result.TotalCoins.Should().Be(50000.3);
    }

    [Fact]
    public void Parse_ItemsClaimed_ExtractsCorrectly()
    {
        var parts = new List<string> { "[Bazaar] Claimed 64x Enchanted Diamond worth 1,234 coins" };
        var result = ChatMessageParser.Parse(null, parts);

        result.Should().NotBeNull();
        result!.Type.Should().Be(BazaarMessageType.ItemsClaimed);
        result.Quantity.Should().Be(64);
        result.ProductName.Should().Be("Enchanted Diamond");
    }

    [Fact]
    public void Parse_UnrelatedMessage_ReturnsNull()
    {
        var parts = new List<string> { "Welcome to Hypixel!" };
        var result = ChatMessageParser.Parse(null, parts);

        result.Should().BeNull();
    }

    [Fact]
    public void Parse_EmptyParts_ReturnsNull()
    {
        var result = ChatMessageParser.Parse(null, []);
        result.Should().BeNull();
    }

    [Fact]
    public void Parse_MultiPartMessage_JoinsParts()
    {
        var parts = new List<string> { "[Bazaar] Buy Order Setup! ", "64x Enchanted Diamond at 1,000 coins each" };
        var result = ChatMessageParser.Parse(null, parts);

        result.Should().NotBeNull();
        result!.Type.Should().Be(BazaarMessageType.BuyOrderPlaced);
        result.Quantity.Should().Be(64);
    }
}
