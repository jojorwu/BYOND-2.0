using Microsoft.Extensions.DependencyInjection;
using Shared.Interfaces;
using Shared.Services;

namespace Editor;

/// <summary>
/// Registers core Editor services and UI components.
/// </summary>
public class EditorModule : IEngineModule
{
    public void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<EditorState>();
        services.AddSingleton<CommandHistory>();
        services.AddSingleton<EditorContext>();

        // UI Services
        services.AddSingleton<IEditorUIService, EditorUIService>();
        services.AddSingleton<MenuBarPanel>();
        services.AddSingleton<HierarchyPanel>();
        services.AddSingleton<InspectorPanel>();
        services.AddSingleton<AssetBrowserPanel>();
        services.AddSingleton<ViewportPanel>();
        services.AddSingleton<ConsolePanel>();

        // Tools
        services.AddSingleton<IToolManager, ToolManager>();
        services.AddSingleton<SelectionTool>();
        services.AddSingleton<PaintTool>();
        services.AddSingleton<EraserTool>();

        // Application
        services.AddSingleton<EditorApplication>();
        services.AddHostedService(sp => sp.GetRequiredService<EditorApplication>());
    }

    public void PreTick() { }
    public void PostTick() { }
}
