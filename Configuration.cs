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

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);

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
