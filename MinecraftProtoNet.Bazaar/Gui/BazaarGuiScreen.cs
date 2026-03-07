namespace MinecraftProtoNet.Bazaar.Gui;

/// <summary>
/// Tracks which Bazaar GUI screen is currently open.
/// </summary>
public enum BazaarGuiScreen
{
    /// <summary>No Bazaar GUI open.</summary>
    Closed,

    /// <summary>Main Bazaar menu (category selection).</summary>
    MainMenu,

    /// <summary>Category listing (e.g., "Farming" items).</summary>
    CategoryListing,

    /// <summary>Product detail page (shows buy/sell options).</summary>
    ProductDetail,

    /// <summary>Buy Order creation screen.</summary>
    BuyOrderScreen,

    /// <summary>Sell Offer creation screen.</summary>
    SellOfferScreen,

    /// <summary>Order confirmation screen.</summary>
    ConfirmScreen,

    /// <summary>Order management (view/claim/cancel orders).</summary>
    OrderManagement
}
