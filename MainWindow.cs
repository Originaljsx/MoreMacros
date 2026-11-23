using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;

namespace MoreMacros;

internal sealed class MainWindow : Window {
    private const int MaxBodyLength = 64 * 1024;
    private static readonly string[] SetNames = ["Individual", "Shared"];

    private readonly Plugin plugin;
    private readonly List<(uint Id, string Abbreviation)> jobs;

    private Guid selectedId;
    private string filter = string.Empty;
    private bool currentJobOnly;
    private int importSet;
    private int importIndex;
    private string status = string.Empty;

    public MainWindow(Plugin plugin) : base("More Macros##MoreMacrosMain") {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints {
            MinimumSize = new Vector2(620, 380),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
        Size = new Vector2(760, 480);
        SizeCondition = ImGuiCond.FirstUseEver;

        jobs = Plugin.Data.GetExcelSheet<ClassJob>()
            .Where(row => row.RowId != 0)
            .Select(row => (row.RowId, row.Abbreviation.ExtractText()))
            .Where(job => job.Item2.Length > 0)
            .OrderBy(job => job.Item2, StringComparer.Ordinal)
            .ToList();
    }

    private Configuration Config => plugin.Config;

    private string JobLabel(uint job) {
        if (job == 0) return "Any job";
        foreach (var (id, abbreviation) in jobs) {
            if (id == job) return abbreviation;
        }

        return $"Job {job}";
    }

    public override void Draw() {
        var listWidth = 230 * ImGui.GetIO().FontGlobalScale;
        if (ImGui.BeginChild("##list", new Vector2(listWidth, 0), true)) DrawList();
        ImGui.EndChild();

        ImGui.SameLine();

        if (ImGui.BeginChild("##editor", Vector2.Zero, false)) {
            var selected = Config.Macros.Find(m => m.Id == selectedId);
            if (selected == null) {
                DrawHelp();
            } else {
                DrawEditor(selected);
            }
        }

        ImGui.EndChild();
    }

    private void DrawList() {
        if (ImGui.Button("New macro")) {
            var macro = new SavedMacro { Job = currentJobOnly ? GameMacros.CurrentJob : 0 };
            Config.Macros.Add(macro);
            Config.Save();
            selectedId = macro.Id;
        }

        ImGui.SameLine();
        if (ImGui.Button("Import...")) ImGui.OpenPopup("##import");
        DrawImportPopup();

        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##filter", "Search", ref filter, 64);
        ImGui.Checkbox("Current job only", ref currentJobOnly);
        ImGui.Separator();

        var currentJob = GameMacros.CurrentJob;
        foreach (var macro in Config.Macros) {
            if (filter.Length > 0 && !macro.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
            if (currentJobOnly && macro.Job != 0 && macro.Job != currentJob) continue;

            var label = macro.Job == 0 ? macro.Name : $"[{JobLabel(macro.Job)}] {macro.Name}";
            if (ImGui.Selectable($"{label}##{macro.Id}", macro.Id == selectedId)) {
                selectedId = macro.Id;
                status = string.Empty;
            }
        }
    }

    private void DrawImportPopup() {
        if (!ImGui.BeginPopup("##import")) return;

        ImGui.TextUnformatted("Copy one of the game's macros into More Macros");
        ImGui.SetNextItemWidth(140);
        ImGui.Combo("##set", ref importSet, SetNames, SetNames.Length);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(100);
        if (ImGui.InputInt("Slot (0-99)", ref importIndex)) {
            importIndex = Math.Clamp(importIndex, 0, GameMacros.SlotsPerSet - 1);
        }

        if (ImGui.Button("Import")) {
            var macro = GameMacros.Import((uint)importSet, (uint)importIndex);
            if (macro == null) {
                status = $"{SetNames[importSet]} slot {importIndex} is empty.";
            } else {
                Config.Macros.Add(macro);
                Config.Save();
                selectedId = macro.Id;
                status = $"Imported {SetNames[importSet]} slot {importIndex}.";
            }

            ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();
    }

    private void DrawHelp() {
        ImGui.TextWrapped(
            "Macros stored here live outside the game's 100 slots, so you can have as many as you like, " +
            "and each one can be longer than 15 lines.");
        ImGui.Spacing();
        ImGui.BulletText("Run one with:  /mm <name>");
        ImGui.BulletText("Put it on a hotbar with the \"Add to game macros\" button.");
        ImGui.BulletText("Give two macros the same name but different jobs, and /mm picks the one for your job.");
        if (status.Length > 0) {
            ImGui.Spacing();
            ImGui.TextWrapped(status);
        }
    }

    private void DrawEditor(SavedMacro macro) {
        var changed = false;

        var name = macro.Name;
        ImGui.SetNextItemWidth(260 * ImGui.GetIO().FontGlobalScale);
        if (ImGui.InputText("Name", ref name, 64)) {
            macro.Name = name;
            changed = true;
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(110 * ImGui.GetIO().FontGlobalScale);
        if (ImGui.BeginCombo("Job", JobLabel(macro.Job))) {
            if (ImGui.Selectable("Any job", macro.Job == 0)) {
                macro.Job = 0;
                changed = true;
            }

            foreach (var (id, abbreviation) in jobs) {
                if (ImGui.Selectable(abbreviation, macro.Job == id)) {
                    macro.Job = id;
                    changed = true;
                }
            }

            ImGui.EndCombo();
        }

        var lineCount = macro.GetLines().Count;
        var chunks = MacroRunner.ChunkCount(lineCount);
        ImGui.TextDisabled(chunks > 1
            ? $"{lineCount} lines - runs as {chunks} linked macros of up to {MacroRunner.EngineLines} lines"
            : $"{lineCount} lines");

        var footer = ImGui.GetFrameHeightWithSpacing() * 2 + ImGui.GetTextLineHeightWithSpacing();
        var body = macro.Body;
        if (ImGui.InputTextMultiline("##body", ref body, MaxBodyLength, new Vector2(-1, -footer))) {
            macro.Body = body;
            changed = true;
        }

        if (changed) Config.Save();

        if (ImGui.Button("Run")) plugin.Run(macro);

        ImGui.SameLine();
        if (ImGui.Button("Add to game macros")) {
            var slot = GameMacros.CreateHotbarStub(macro);
            status = slot < 0
                ? "No empty Individual macro slot is available."
                : $"Created Individual macro #{slot}. Open the game's macro window and drag it to a hotbar.";
        }

        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip(
                "Writes a one-line macro (/mm <name>) into the first empty Individual slot,\n" +
                "so this macro can be placed on a hotbar.");
        }

        ImGui.SameLine();
        if (ImGui.Button("Duplicate")) {
            var copy = new SavedMacro { Name = macro.Name, Job = macro.Job, Body = macro.Body };
            Config.Macros.Insert(Config.Macros.IndexOf(macro) + 1, copy);
            Config.Save();
            selectedId = copy.Id;
        }

        ImGui.SameLine();
        var ctrl = ImGui.GetIO().KeyCtrl;
        ImGui.BeginDisabled(!ctrl);
        if (ImGui.Button("Delete")) {
            Config.Macros.Remove(macro);
            Config.Save();
            selectedId = Guid.Empty;
            status = string.Empty;
        }

        ImGui.EndDisabled();
        if (!ctrl && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip("Hold Ctrl to delete.");

        ImGui.TextDisabled($"/mm {macro.Name.Trim()}");
        if (status.Length > 0) {
            ImGui.SameLine();
            ImGui.TextUnformatted(status);
        }
    }
}
