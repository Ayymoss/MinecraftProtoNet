using Bot.Webcore.Components;
using Bot.Webcore.Services;
using MinecraftProtoNet.Baritone.Core;
using MinecraftProtoNet.Baritone.Utilities;
using MinecraftProtoNet.Bazaar.Utilities;
using MinecraftProtoNet.Core.Commands;
using MinecraftProtoNet.Core.Services;
using MinecraftProtoNet.Core.State;
using MinecraftProtoNet.Core.Utilities;

namespace Bot.Webcore;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add Blazor services
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        // Add MinecraftClient services from core library
        builder.Services.AddMinecraftClient(builder.Configuration);
        
        // Add Baritone pathfinding and physics
        builder.Services.AddBaritone();

        // Add Bazaar autonomous trading
        builder.Services.AddBazaarTrading(builder.Configuration);

        // Add BotService as singleton for UI state
        builder.Services.AddSingleton<BotService>();
        
        // Add DragDropState for cross-component drag/drop
        builder.Services.AddSingleton<DragDropState>();

        var app = builder.Build();

        // Initialize registry services on startup
        var registryService = app.Services.GetRequiredService<IItemRegistryService>();
        await registryService.InitializeAsync();
        
        // Set static registry in EntityInventory
        EntityInventory.SetRegistryService(registryService);
        
        // Set static registry in Baritone
        Baritone.SetItemRegistryService(registryService);
        
        // Ensure BaritoneGameLoopHook is constructed to attach the hook
        // This forces the singleton to be created and hook to the game loop
        app.Services.GetRequiredService<MinecraftProtoNet.Baritone.Utilities.ServiceCollectionExtensions.BaritoneGameLoopHook>();
        
        // Ensure BazaarGameLoopHook is constructed to attach the hook
        app.Services.GetRequiredService<MinecraftProtoNet.Bazaar.Utilities.ServiceCollectionExtensions.BazaarGameLoopHook>();

        // Ensure HumanizerGameLoopHook is constructed to attach idle behavior
        app.Services.GetRequiredService<HumanizerGameLoopHook>();

        // Register Baritone commands
        var commandRegistry = app.Services.GetRequiredService<CommandRegistry>();
        commandRegistry.AutoRegisterCommands(app.Services, typeof(MinecraftProtoNet.Baritone.Commands.BaritoneCommand).Assembly);

        // Register Bazaar commands
        commandRegistry.AutoRegisterCommands(app.Services, typeof(MinecraftProtoNet.Bazaar.Commands.BazaarCommand).Assembly);

        // Configure the HTTP request pipeline
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        app.UseHttpsRedirection();

        app.UseAntiforgery();

        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        // Chat redirection endpoint
        app.MapPost("/api/chat/redirect", (MinecraftProtoNet.Core.Dtos.ChatRedirectRequest request, BotService botService) =>
        {
            botService.AddRedirectedChat(request);
            return Results.Ok();
        });

        await app.RunAsync();
    }
}
