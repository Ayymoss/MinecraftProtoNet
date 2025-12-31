using Bot_Web.Components;
using Bot_Web.Services;
using MinecraftProtoNet.Services;
using MinecraftProtoNet.State;
using MinecraftProtoNet.Utilities;

namespace Bot_Web;

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
        
        // Add BotService as singleton for UI state
        builder.Services.AddSingleton<BotService>();

        var app = builder.Build();

        // Initialize registry services on startup
        var registryService = app.Services.GetRequiredService<IItemRegistryService>();
        await registryService.InitializeAsync();
        
        // Set static registry in EntityInventory
        EntityInventory.SetRegistryService(registryService);

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
