using System;
using System.Collections.Generic;
using Dalamud.Configuration;

namespace MoreMacros;

[Serializable]
public sealed class SavedMacro {
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New Macro";

    /// <summary>ClassJob row id this macro is restricted to, or 0 for any job.</summary>
    public uint Job { get; set; }

    /// <summary>Position in the grid: page * 100 + position on the page. -1 until placed.</summary>
    public int Slot { get; set; } = -1;

    public uint IconId { get; set; } = DefaultIcon;

    public const uint DefaultIcon = 66001;

    /// <summary>Macro text, one command per line.</summary>
    public string Body { get; set; } = string.Empty;

    public List<string> GetLines() {
        var lines = new List<string>();
        foreach (var raw in Body.Split('\n')) {
            var line = raw.Trim('\r', ' ', '\t');
            if (line.Length > 0) lines.Add(line);
        }

        return lines;
    }
}

[Serializable]
public sealed class Configuration : IPluginConfiguration {
    public int Version { get; set; } = 1;
    public List<SavedMacro> Macros { get; set; } = [];

    public List<string> PageNames { get; set; } = [];

    public const int SlotsPerPage = 100;

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);

    public SavedMacro? InSlot(int slot) => Macros.Find(m => m.Slot == slot);

    public int FirstEmptySlot(int from = 0) {
        var used = new HashSet<int>();
        foreach (var macro in Macros) used.Add(macro.Slot);
        var slot = Math.Max(from, 0);
        while (used.Contains(slot)) slot++;
        return slot;
    }

    /// <summary>Gives every macro a unique slot and makes sure there is a page for each of them.</summary>
    public void Normalize() {
        var used = new HashSet<int>();
        foreach (var macro in Macros) {
            if (macro.Slot < 0 || !used.Add(macro.Slot)) macro.Slot = -1;
        }

        foreach (var macro in Macros) {
            if (macro.Slot >= 0) continue;
            var slot = 0;
            while (!used.Add(slot)) slot++;
            macro.Slot = slot;
        }

        var pages = 1;
        foreach (var macro in Macros) pages = Math.Max(pages, macro.Slot / SlotsPerPage + 1);
        while (PageNames.Count < pages) PageNames.Add($"Page {PageNames.Count + 1}");
    }

    /// <summary>
    /// Finds a macro by name. A macro restricted to the current job wins over an "any job" macro of the same name;
    /// macros restricted to a different job are never returned.
    /// </summary>
    public SavedMacro? Find(string name, uint currentJob) {
        SavedMacro? fallback = null;
        foreach (var macro in Macros) {
            if (!string.Equals(macro.Name.Trim(), name, StringComparison.OrdinalIgnoreCase)) continue;
            if (macro.Job != 0 && macro.Job == currentJob) return macro;
            if (macro.Job == 0) fallback ??= macro;
        }

        return fallback;
    }
}
