using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace MoreMacros;

internal sealed class MainWindow : Window {
    private readonly Plugin plugin;

    public MainWindow(Plugin plugin) : base("More Macros##MoreMacrosMain") {
        this.plugin = plugin;
        Size = new Vector2(420, 420);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw() {
        var config = plugin.Config;
        var sets = plugin.Sets;

        var enabled = config.Enabled;
        if (ImGui.Checkbox("Separate Individual macros for each job", ref enabled)) {
            config.Enabled = enabled;
            config.Save();
        }

        ImGui.TextWrapped(
            "Each job gets its own 100 Individual macros, swapped in when you change job. " +
            "Edit them in the game's own macro window. Shared macros are not touched.");
        ImGui.Separator();

        var data = sets.Current;
        if (data == null) {
            ImGui.TextDisabled("Log in to a character to see its macro sets.");
            return;
        }

        ImGui.TextUnformatted(data.LoadedJob == 0
            ? "No set loaded yet. It loads a couple of seconds after login."
            : $"Loaded set: {Jobs.Name(data.LoadedJob)}");
        if (sets.LastMessage.Length > 0) ImGui.TextDisabled(sets.LastMessage);

        // Keep the list current with edits made in the game's macro window.
        if (ImGui.IsWindowAppearing()) sets.SaveCurrent();
        // Copy and restore write into the slots, so they are blocked while the game's macro window is open.
        var blocked = sets.IsMacroWindowOpen || !sets.CanEditSets(data);
        if (sets.IsMacroWindowOpen) {
            ImGui.TextColored(new Vector4(1, 0.8f, 0.3f, 1), "Close the game's macro window to copy or restore sets.");
        } else if (!sets.CanEditSets(data)) {
            ImGui.TextColored(new Vector4(1, 0.8f, 0.3f, 1), "Copy and restore need the option above on, and your current job's set loaded.");
        }

        ImGui.Spacing();
        var ctrl = ImGui.GetIO().KeyCtrl;
        if (ImGui.BeginTable("##sets", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH)) {
            ImGui.TableSetupColumn("Job");
            ImGui.TableSetupColumn("Macros");
            ImGui.TableSetupColumn("##actions", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableHeadersRow();

            foreach (var (job, set) in data.Sets.OrderBy(s => Jobs.Name(s.Key)).ToList()) {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(job == data.LoadedJob ? $"{Jobs.Name(job)} (current)" : Jobs.Name(job));
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{MacroSets.CountUsed(set)} / {MacroSets.Slots}");
                ImGui.TableNextColumn();
                if (job == data.LoadedJob) continue;

                ImGui.BeginDisabled(!ctrl || blocked);
                if (ImGui.SmallButton($"Copy to current##{job}")) sets.CopyFrom(job);
                ImGui.EndDisabled();
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) {
                    ImGui.SetTooltip($"Hold Ctrl and click to replace {Jobs.Name(data.LoadedJob)}'s macros with {Jobs.Name(job)}'s.");
                }
            }

            ImGui.EndTable();
        }

        ImGui.Spacing();
        ImGui.BeginDisabled(!ctrl || blocked || data.OriginalBackup == null || data.LoadedJob == 0);
        if (ImGui.Button("Restore original macros to current job")) sets.RestoreOriginal();
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) {
            ImGui.SetTooltip("Hold Ctrl and click to put back the Individual macros you had before installing More Macros.");
        }
    }
}
