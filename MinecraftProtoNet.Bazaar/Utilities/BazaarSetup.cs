using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Bazaar.Api;
using MinecraftProtoNet.Bazaar.Configuration;
using MinecraftProtoNet.Bazaar.Engine;
using MinecraftProtoNet.Bazaar.Gui;
using MinecraftProtoNet.Bazaar.Orders;
using MinecraftProtoNet.Bazaar.Safety;
using MinecraftProtoNet.Bazaar.Services;
using MinecraftProtoNet.Core.Core.Abstractions;
using Refit;

namespace MinecraftProtoNet.Bazaar.Utilities;

/// <summary>
/// Extension methods for registering Bazaar trading services with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Bazaar trading services to the service collection.
    /// </summary>
    public static IServiceCollection AddBazaarTrading(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind configuration
        services.Configure<BazaarTradingConfig>(configuration.GetSection(BazaarTradingConfig.SectionName));

        // Refit API client for BazaarCompanion
        var baseUrl = configuration[$"{BazaarTradingConfig.SectionName}:BazaarCompanionBaseUrl"] ?? "http://localhost:5000";
        var apiKey = configuration[$"{BazaarTradingConfig.SectionName}:BazaarCompanionApiKey"] ?? "";

        // Configure JSON serialization to handle enums as strings (but robust enough for numbers)
        var refitSettings = new RefitSettings
        {
            ContentSerializer = new SystemTextJsonContentSerializer(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            })
        };

        services.AddRefitClient<IBazaarCompanionApi>(refitSettings)
            .ConfigureHttpClient(c =>
            {
                c.BaseAddress = new Uri(baseUrl);
                if (!string.IsNullOrEmpty(apiKey))
                    c.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
            });

        // Services
        services.AddSingleton<MarketDataService>();
        services.AddSingleton<CoinTracker>();

        // Orders
        services.AddSingleton<OrderManager>();
        services.AddSingleton<OrderWalker>();

        // Safety
        services.AddSingleton<TradingSafetyGuard>();

        // GUI
        services.AddSingleton<BazaarGuiNavigator>();

        // Engine
        services.AddSingleton<BazaarTradingEngine>();

        // Game loop hook
        services.AddSingleton<BazaarGameLoopHook>();

        return services;
    }

    /// <summary>
    /// Service that hooks the Bazaar trading engine to the game loop during construction.
    /// </summary>
    public class BazaarGameLoopHook
    {
        public BazaarGameLoopHook(
            IGameLoop gameLoop,
            BazaarTradingEngine engine,
            ILogger<BazaarGameLoopHook> logger)
        {
            logger.LogInformation("BazaarGameLoopHook: Hooking trading engine to game loop");
            gameLoop.PostTick += engine.OnTick;
            logger.LogInformation("BazaarGameLoopHook: Successfully hooked trading engine to game loop");
        }
    }
}
