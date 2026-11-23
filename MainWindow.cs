using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;

namespace MoreMacros;

/// <summary>Macro window laid out like the game's own: a 10x10 grid of icon slots per page, with an editor beside it.</summary>
internal sealed class MainWindow : Window {
    private const int Columns = 10;
    private const int PerPage = Configuration.SlotsPerPage;
    private const int MaxBodyLength = 64 * 1024;
    private static readonly string[] SetNames = ["Individual", "Shared"];

    private const uint ColorSlot = 0x60000000;
    private const uint ColorBorder = 0x50FFFFFF;
    private const uint ColorHover = 0xC0FFFFFF;
    private const uint ColorSelected = 0xFF40C8FF;
    private const uint ColorLabel = 0xFFFFFFFF;
    private const uint ColorLabelShadow = 0xFF000000;

    private readonly Plugin plugin;
    private readonly List<(uint Id, string Abbreviation)> jobs;
    private readonly List<uint> icons;
    private readonly Dictionary<int, SavedMacro> bySlot = [];

    private int page;
    private int jumpToPage = -1;
    private int selectedSlot;
    private int dragFrom = -1;
    private string search = string.Empty;
    private int importSet;
    private int importIndex;
    private string status = string.Empty;

    public MainWindow(Plugin plugin) : base("More Macros##MoreMacrosMain") {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints {
            MinimumSize = new Vector2(820, 560),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
        Size = new Vector2(900, 600);
        SizeCondition = ImGuiCond.FirstUseEver;

        jobs = Plugin.Data.GetExcelSheet<ClassJob>()
            .Where(row => row.RowId != 0)
            .Select(row => (row.RowId, row.Abbreviation.ExtractText()))
            .Where(job => job.Item2.Length > 0)
            .OrderBy(job => job.Item2, StringComparer.Ordinal)
            .ToList();

        icons = Plugin.Data.GetExcelSheet<MacroIcon>()
            .Select(row => (uint)row.Icon)
            .Where(icon => icon != 0)
            .Distinct()
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
        bySlot.Clear();
        foreach (var macro in Config.Macros) bySlot[macro.Slot] = macro;

        var scale = ImGuiHelpers.GlobalScale;
        var slotSize = 40 * scale;
        var gap = 4 * scale;
        var gridWidth = Columns * slotSize + (Columns - 1) * gap;

        if (ImGui.BeginChild("##left", new Vector2(gridWidth, 0), false)) {
            DrawPageTabs();
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##search", "Search all pages", ref search, 64);
            if (search.Length > 0) {
                DrawSearchResults();
            } else {
                DrawGrid(slotSize, gap);
            }
        }

        ImGui.EndChild();
        ImGui.SameLine();

        if (ImGui.BeginChild("##editor", Vector2.Zero, false)) DrawEditor(slotSize);
        ImGui.EndChild();
    }

    private void DrawPageTabs() {
        var deletePage = -1;
        if (ImGui.BeginTabBar("##pages", ImGuiTabBarFlags.FittingPolicyScroll)) {
            for (var i = 0; i < Config.PageNames.Count; i++) {
                var flags = jumpToPage == i ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
                if (ImGui.BeginTabItem($"{Config.PageNames[i]}###page{i}", flags)) {
                    if (jumpToPage < 0 || jumpToPage == i) page = i;
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginPopupContextItem($"##pagemenu{i}")) {
                    var pageName = Config.PageNames[i];
                    ImGui.SetNextItemWidth(160 * ImGuiHelpers.GlobalScale);
                    if (ImGui.InputText("Name", ref pageName, 24) && pageName.Length > 0) {
                        Config.PageNames[i] = pageName;
                        Config.Save();
                    }

                    var hasMacros = Config.Macros.Exists(m => m.Slot / PerPage == i);
                    ImGui.BeginDisabled(hasMacros || Config.PageNames.Count <= 1);
                    if (ImGui.Button("Delete page")) {
                        deletePage = i;
                        ImGui.CloseCurrentPopup();
                    }

                    ImGui.EndDisabled();
                    if (hasMacros) ImGui.TextDisabled("Only an empty page can be deleted.");
                    ImGui.EndPopup();
                }
            }

            if (ImGui.TabItemButton("+", ImGuiTabItemFlags.Trailing)) {
                Config.PageNames.Add($"Page {Config.PageNames.Count + 1}");
                Config.Save();
                jumpToPage = Config.PageNames.Count - 1;
            } else {
                jumpToPage = -1;
            }

            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Add a page. Right-click a page to rename or delete it.");
            ImGui.EndTabBar();
        }

        if (deletePage >= 0) {
            Config.PageNames.RemoveAt(deletePage);
            foreach (var macro in Config.Macros) {
                if (macro.Slot / PerPage > deletePage) macro.Slot -= PerPage;
            }

            if (selectedSlot / PerPage >= deletePage) selectedSlot = 0;
            page = Math.Min(page, Config.PageNames.Count - 1);
            Config.Save();
        }
    }

    private void DrawSearchResults() {
        foreach (var macro in Config.Macros.OrderBy(m => m.Slot)) {
            if (!macro.Name.Contains(search, StringComparison.OrdinalIgnoreCase)) continue;
            var macroPage = macro.Slot / PerPage;
            var pageName = macroPage < Config.PageNames.Count ? Config.PageNames[macroPage] : $"Page {macroPage + 1}";
            var job = macro.Job == 0 ? string.Empty : $"[{JobLabel(macro.Job)}] ";
            if (ImGui.Selectable($"{job}{macro.Name}  ({pageName})##{macro.Id}", macro.Slot == selectedSlot)) {
                selectedSlot = macro.Slot;
                jumpToPage = macroPage;
                search = string.Empty;
                status = string.Empty;
            }
        }
    }

    private static void DrawIcon(ImDrawListPtr drawList, uint iconId, Vector2 min, Vector2 max) {
        var texture = Plugin.Textures.GetFromGameIcon(new GameIconLookup(iconId == 0 ? SavedMacro.DefaultIcon : iconId)).GetWrapOrDefault();
        if (texture != null) drawList.AddImage(texture.Handle, min, max);
    }

    private void DrawGrid(float slotSize, float gap) {
        var size = new Vector2(slotSize);
        var drawList = ImGui.GetWindowDrawList();
        var rounding = 4 * ImGuiHelpers.GlobalScale;
        var released = ImGui.IsMouseReleased(ImGuiMouseButton.Left);

        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(gap));
        for (var position = 0; position < PerPage; position++) {
            var slot = page * PerPage + position;
            bySlot.TryGetValue(slot, out var macro);
            if (position % Columns != 0) ImGui.SameLine();

            var min = ImGui.GetCursorScreenPos();
            var max = min + size;
            if (ImGui.InvisibleButton($"##slot{position}", size)) {
                selectedSlot = slot;
                status = string.Empty;
            }

            var hovered = ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem);
            if (macro != null && ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left)) dragFrom = slot;

            drawList.AddRectFilled(min, max, ColorSlot, rounding);
            if (macro != null && slot != dragFrom) {
                DrawIcon(drawList, macro.IconId, min, max);
                if (macro.Job != 0) {
                    var label = JobLabel(macro.Job);
                    var textPos = new Vector2(min.X + 2, max.Y - ImGui.GetTextLineHeight());
                    drawList.AddText(textPos + Vector2.One, ColorLabelShadow, label);
                    drawList.AddText(textPos, ColorLabel, label);
                }
            }

            if (slot == selectedSlot) {
                drawList.AddRect(min, max, ColorSelected, rounding, ImDrawFlags.None, 2 * ImGuiHelpers.GlobalScale);
            } else {
                drawList.AddRect(min, max, hovered ? ColorHover : ColorBorder, rounding);
            }

            if (hovered && dragFrom >= 0 && released && dragFrom != slot) {
                MoveOrSwap(dragFrom, slot);
                selectedSlot = slot;
            } else if (hovered && dragFrom < 0 && macro != null) {
                ImGui.SetTooltip($"{macro.Name}\n/mm {macro.Name.Trim()}");
            }
        }

        ImGui.PopStyleVar();

        if (dragFrom >= 0) {
            if (bySlot.TryGetValue(dragFrom, out var dragged)) {
                var mouse = ImGui.GetMousePos();
                DrawIcon(ImGui.GetForegroundDrawList(), dragged.IconId, mouse - size / 2, mouse + size / 2);
            }

            if (released || !ImGui.IsMouseDown(ImGuiMouseButton.Left)) dragFrom = -1;
        }
    }

    private void MoveOrSwap(int from, int to) {
        bySlot.TryGetValue(from, out var moving);
        bySlot.TryGetValue(to, out var target);
        if (moving == null) return;
        moving.Slot = to;
        if (target != null) target.Slot = from;
        Config.Save();
    }

    private SavedMacro CreateInSelectedSlot() {
        var macro = new SavedMacro { Name = string.Empty, Slot = selectedSlot };
        Config.Macros.Add(macro);
        bySlot[selectedSlot] = macro;
        return macro;
    }

    private void DrawEditor(float slotSize) {
        var scale = ImGuiHelpers.GlobalScale;
        bySlot.TryGetValue(selectedSlot, out var macro);
        var changed = false;

        // Icon, click to change.
        var iconSize = new Vector2(slotSize * 1.5f);
        var iconMin = ImGui.GetCursorScreenPos();
        if (ImGui.InvisibleButton("##icon", iconSize)) ImGui.OpenPopup("##iconpicker");
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(iconMin, iconMin + iconSize, ColorSlot, 4 * scale);
        DrawIcon(drawList, macro?.IconId ?? SavedMacro.DefaultIcon, iconMin, iconMin + iconSize);
        drawList.AddRect(iconMin, iconMin + iconSize, ImGui.IsItemHovered() ? ColorHover : ColorBorder, 4 * scale);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Click to change the icon.");
        var pickedIcon = DrawIconPicker(slotSize);
        if (pickedIcon != 0) {
            macro ??= CreateInSelectedSlot();
            macro.IconId = pickedIcon;
            changed = true;
        }

        ImGui.SameLine();
        ImGui.BeginGroup();
        var name = macro?.Name ?? string.Empty;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputTextWithHint("##name", "Macro name", ref name, 64)) {
            macro ??= CreateInSelectedSlot();
            macro.Name = name;
            changed = true;
        }

        ImGui.SetNextItemWidth(120 * scale);
        if (ImGui.BeginCombo("Job", JobLabel(macro?.Job ?? 0))) {
            if (ImGui.Selectable("Any job", (macro?.Job ?? 0) == 0) && macro != null) {
                macro.Job = 0;
                changed = true;
            }

            foreach (var (id, abbreviation) in jobs) {
                if (!ImGui.Selectable(abbreviation, macro?.Job == id)) continue;
                macro ??= CreateInSelectedSlot();
                macro.Job = id;
                changed = true;
            }

            ImGui.EndCombo();
        }

        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("Macros with the same name can be set to different jobs.\n/mm runs the one for your current job.");
        }

        ImGui.EndGroup();

        var footer = ImGui.GetFrameHeightWithSpacing() + ImGui.GetTextLineHeightWithSpacing() * 3;
        var body = macro?.Body ?? string.Empty;
        if (ImGui.InputTextMultiline("##body", ref body, MaxBodyLength, new Vector2(-1, -footer))) {
            macro ??= CreateInSelectedSlot();
            macro.Body = body;
            changed = true;
        }

        if (changed) Config.Save();

        var lineCount = macro?.GetLines().Count ?? 0;
        var chunks = MacroRunner.ChunkCount(lineCount);
        ImGui.TextDisabled(chunks > 1 ? $"Lines: {lineCount}  (runs as {chunks} linked macros)" : $"Lines: {lineCount}");

        ImGui.BeginDisabled(macro == null);
        if (ImGui.Button("Run") && macro != null) plugin.Run(macro);

        ImGui.SameLine();
        if (ImGui.Button("Add to hotbar macros") && macro != null) {
            var slot = GameMacros.CreateHotbarStub(macro);
            status = slot < 0
                ? "No empty Individual macro slot is available."
                : $"Created Individual macro #{slot}. Drag it from the game's macro window to a hotbar.";
        }

        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) {
            ImGui.SetTooltip(
                "Writes a one-line macro (/mm <name>) into the first empty Individual slot\n" +
                "of the game's macro window, so this macro can be placed on a hotbar.");
        }

        ImGui.SameLine();
        if (ImGui.Button("Import...")) ImGui.OpenPopup("##import");
        DrawImportPopup();

        ImGui.SameLine();
        var ctrl = ImGui.GetIO().KeyCtrl;
        ImGui.BeginDisabled(macro == null || !ctrl);
        if (ImGui.Button("Delete") && macro != null) {
            Config.Macros.Remove(macro);
            Config.Save();
            status = string.Empty;
        }

        ImGui.EndDisabled();
        if (macro != null && !ctrl && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) {
            ImGui.SetTooltip("Hold Ctrl to delete.");
        }

        var runName = macro?.Name.Trim() ?? string.Empty;
        ImGui.TextDisabled(runName.Length > 0 ? $"/mm {runName}" : "Click a slot, then type a name and the macro text.");
        if (status.Length > 0) ImGui.TextWrapped(status);
    }

    /// <summary>Returns the icon chosen this frame, or 0.</summary>
    private uint DrawIconPicker(float slotSize) {
        if (!ImGui.BeginPopup("##iconpicker")) return 0;

        uint picked = 0;
        const int perRow = 10;
        var size = new Vector2(slotSize);
        var gap = 4 * ImGuiHelpers.GlobalScale;
        var width = perRow * slotSize + (perRow - 1) * gap + ImGui.GetStyle().ScrollbarSize;
        if (ImGui.BeginChild("##icons", new Vector2(width, slotSize * 8), false)) {
            var drawList = ImGui.GetWindowDrawList();
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(gap));
            for (var i = 0; i < icons.Count; i++) {
                if (i % perRow != 0) ImGui.SameLine();
                var min = ImGui.GetCursorScreenPos();
                if (ImGui.InvisibleButton($"##pick{i}", size)) picked = icons[i];
                if (!ImGui.IsItemVisible()) continue;
                DrawIcon(drawList, icons[i], min, min + size);
                if (ImGui.IsItemHovered()) drawList.AddRect(min, min + size, ColorHover);
            }

            ImGui.PopStyleVar();
        }

        ImGui.EndChild();
        if (picked != 0) ImGui.CloseCurrentPopup();
        ImGui.EndPopup();
        return picked;
    }

    private void DrawImportPopup() {
        if (!ImGui.BeginPopup("##import")) return;

        ImGui.TextUnformatted("Copy one of the game's macros into the selected slot");
        ImGui.SetNextItemWidth(140 * ImGuiHelpers.GlobalScale);
        ImGui.Combo("##set", ref importSet, SetNames, SetNames.Length);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(100 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("Slot (0-99)", ref importIndex)) {
            importIndex = Math.Clamp(importIndex, 0, GameMacros.SlotsPerSet - 1);
        }

        if (ImGui.Button("Import")) {
            var macro = GameMacros.Import((uint)importSet, (uint)importIndex);
            if (macro == null) {
                status = $"{SetNames[importSet]} slot {importIndex} is empty.";
            } else {
                // Never overwrite: if the selected slot is taken, use the next empty one.
                macro.Slot = Config.FirstEmptySlot(selectedSlot);
                Config.Macros.Add(macro);
                Config.Normalize();
                Config.Save();
                selectedSlot = macro.Slot;
                jumpToPage = macro.Slot / PerPage;
                status = $"Imported {SetNames[importSet]} slot {importIndex}.";
            }

            ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();
    }
}
