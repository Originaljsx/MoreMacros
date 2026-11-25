using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace MoreMacros;

public sealed class Plugin : IDalamudPlugin {
    private const string WindowCommand = "/moremacros";
    private const string OldRunCommand = "/mm";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager Commands { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IDataManager Data { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IChatGui Chat { get; private set; } = null!;

    internal Configuration Config { get; }
    internal MacroSets Sets { get; }

    private readonly WindowSystem windows = new("MoreMacros");
    private readonly MainWindow mainWindow;

    public Plugin() {
        Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        if (Config.Version < 2) {
            // 0.3 and earlier stored a /mm macro library here. Keep a copy of that file before starting fresh.
            var file = PluginInterface.ConfigFile;
            if (file.Exists) {
                var backup = System.IO.Path.Combine(file.DirectoryName!, "MoreMacros.v0.3-library-backup.json");
                if (!System.IO.File.Exists(backup)) file.CopyTo(backup);
            }

            Config = new Configuration();
        }
        Sets = new MacroSets(Config);
        mainWindow = new MainWindow(this);
        windows.AddWindow(mainWindow);

        // Hotbar buttons made by 0.3 run "/mm <name>". Explain instead of failing silently.
        Commands.AddHandler(OldRunCommand, new CommandInfo((_, _) =>
            Chat.PrintError("[More Macros] /mm was removed in 0.4. Replace this macro with the actions it ran; each job now has its own Individual macros.")) {
            ShowInHelp = false,
        });
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
        try {
            // Keep edits made in the game's macro window since the last swap.
            Sets.SaveCurrent();
        } catch (System.Exception ex) {
            Log.Error(ex, "Saving macros on unload failed");
        }

        PluginInterface.UiBuilder.Draw -= windows.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= mainWindow.Toggle;
        PluginInterface.UiBuilder.OpenConfigUi -= mainWindow.Toggle;
        Commands.RemoveHandler(WindowCommand);
        Commands.RemoveHandler(OldRunCommand);
        windows.RemoveAllWindows();
    }
}
