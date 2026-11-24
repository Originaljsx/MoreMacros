using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace MoreMacros;

public sealed class Plugin : IDalamudPlugin {
    private const string WindowCommand = "/moremacros";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager Commands { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IDataManager Data { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;

    internal Configuration Config { get; }
    internal MacroSets Sets { get; }

    private readonly WindowSystem windows = new("MoreMacros");
    private readonly MainWindow mainWindow;

    public Plugin() {
        Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        if (Config.Version < 2) Config = new Configuration();
        Sets = new MacroSets(Config);
        mainWindow = new MainWindow(this);
        windows.AddWindow(mainWindow);

        Commands.AddHandler(WindowCommand, new CommandInfo((_, _) => mainWindow.Toggle()) {
            HelpMessage = "Open the More Macros window.",
        });

        Framework.Update += OnUpdate;
        PluginInterface.UiBuilder.Draw += windows.Draw;
        PluginInterface.UiBuilder.OpenMainUi += mainWindow.Toggle;
        PluginInterface.UiBuilder.OpenConfigUi += mainWindow.Toggle;
    }

    private void OnUpdate(IFramework framework) {
        try {
            Sets.Update();
        } catch (System.Exception ex) {
            Log.Error(ex, "Macro swap failed");
        }
    }

    public void Dispose() {
        Framework.Update -= OnUpdate;
        PluginInterface.UiBuilder.Draw -= windows.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= mainWindow.Toggle;
        PluginInterface.UiBuilder.OpenConfigUi -= mainWindow.Toggle;
        Commands.RemoveHandler(WindowCommand);
        windows.RemoveAllWindows();
    }
}
