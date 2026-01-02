using Bot.Webcore.Components;
using Bot.Webcore.Services;
using MinecraftProtoNet.Services;
using MinecraftProtoNet.State;
using MinecraftProtoNet.Utilities;
using MinecraftProtoNet.Baritone.Infrastructure;
using MinecraftProtoNet.Baritone.Pathfinding;
using MinecraftProtoNet.Commands;

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
        builder.Services.AddMinecraftClient();
        
        // Add Baritone pathfinding and physics
        builder.Services.AddBaritone();
        
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
        
        // Register Baritone commands
        var commandRegistry = app.Services.GetRequiredService<CommandRegistry>();
        commandRegistry.AutoRegisterCommands(app.Services, typeof(PathingService).Assembly);

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

        await app.RunAsync();
    }
}
