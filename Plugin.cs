using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace MoreMacros;

public sealed class Plugin : IDalamudPlugin {
    private const string RunCommand = "/mm";
    private const string WindowCommand = "/moremacros";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager Commands { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IChatGui Chat { get; private set; } = null!;
    [PluginService] internal static IDataManager Data { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static ITextureProvider Textures { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;

    internal Configuration Config { get; }
    internal MacroRunner Runner { get; }

    private readonly WindowSystem windows = new("MoreMacros");
    private readonly MainWindow mainWindow;

    public Plugin() {
        Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Config.Normalize();
        Runner = new MacroRunner();
        mainWindow = new MainWindow(this);
        windows.AddWindow(mainWindow);

        Commands.AddHandler(RunCommand, new CommandInfo(OnRunCommand) {
            HelpMessage = "Run a More Macros macro: /mm <name>. With no name, opens the macro window.",
        });
        Commands.AddHandler(WindowCommand, new CommandInfo((_, _) => mainWindow.Toggle()) {
            HelpMessage = "Open the More Macros window.",
        });
        Commands.AddHandler(MacroRunner.ContinueCommand, new CommandInfo((_, args) => Runner.Continue(args)) {
            ShowInHelp = false,
        });

        PluginInterface.UiBuilder.Draw += windows.Draw;
        PluginInterface.UiBuilder.OpenMainUi += mainWindow.Toggle;
        PluginInterface.UiBuilder.OpenConfigUi += mainWindow.Toggle;
    }

    private void OnRunCommand(string command, string args) {
        var name = args.Trim().Trim('"').Trim();
        if (name.Length == 0) {
            mainWindow.Toggle();
            return;
        }

        var macro = Config.Find(name, GameMacros.CurrentJob);
        if (macro == null) {
            Chat.PrintError($"[More Macros] No macro named \"{name}\" for your current job.");
            return;
        }

        Run(macro);
    }

    /// <summary>
    /// Runs a macro on the next frame. Deferring keeps us out of the engine's own line processing when /mm is
    /// itself used from inside a macro.
    /// </summary>
    internal void Run(SavedMacro macro) {
        var name = macro.Name.Trim();
        var lines = macro.GetLines();
        Framework.RunOnTick(() => {
            if (!Runner.Start(name, lines, out var error)) Chat.PrintError($"[More Macros] {error}");
        });
    }

    public void Dispose() {
        PluginInterface.UiBuilder.Draw -= windows.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= mainWindow.Toggle;
        PluginInterface.UiBuilder.OpenConfigUi -= mainWindow.Toggle;
        Commands.RemoveHandler(RunCommand);
        Commands.RemoveHandler(WindowCommand);
        Commands.RemoveHandler(MacroRunner.ContinueCommand);
        windows.RemoveAllWindows();
        Runner.Dispose();
    }
}
