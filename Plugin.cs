using Dalamud.Game.Command;
using Dalamud.Plugin;

namespace ArenaPilot;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/斗兽塔";
    private readonly MainWindow window;
    private readonly ArenaController controller;

    public string Name => "ArenaPilot-斗兽塔助手";
    public Configuration Configuration { get; }

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        DalamudApi.Initialize(pluginInterface);
        Configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize(pluginInterface);

        var diagnosticsDirectory = Path.Combine(pluginInterface.GetPluginConfigDirectory(), "diagnostics");
        controller = new ArenaController(diagnosticsDirectory, pluginInterface, Configuration);
        window = new MainWindow(controller, Configuration);

        DalamudApi.Commands.AddHandler(CommandName, new CommandInfo((_, _) => window.Open())
        {
            HelpMessage = "打开斗兽塔自动爬塔窗口。",
        });
        pluginInterface.UiBuilder.Draw += window.Draw;
        pluginInterface.UiBuilder.OpenMainUi += window.Open;
        pluginInterface.UiBuilder.OpenConfigUi += window.Open;
        DalamudApi.Log.Information("ArenaPilot loaded.");
    }

    public void Dispose()
    {
        DalamudApi.PluginInterface.UiBuilder.Draw -= window.Draw;
        DalamudApi.PluginInterface.UiBuilder.OpenMainUi -= window.Open;
        DalamudApi.PluginInterface.UiBuilder.OpenConfigUi -= window.Open;
        DalamudApi.Commands.RemoveHandler(CommandName);
        controller.Dispose();
        Configuration.Save();
    }
}
