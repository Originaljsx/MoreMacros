using System;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace MoreMacros;

/// <summary>Reads from and writes to the game's own 100+100 macro slots.</summary>
internal static unsafe class GameMacros {
    public const int SlotsPerSet = 100;
    private const int NativeNameLength = 20;
    private const uint DefaultMacroIcon = 66001;

    public static uint CurrentJob {
        get {
            var state = PlayerState.Instance();
            return state == null ? 0u : state->CurrentClassJobId;
        }
    }

    /// <summary>Copies a game macro into a new plugin macro. Returns null if the slot is empty.</summary>
    public static SavedMacro? Import(uint set, uint index) {
        var module = RaptureMacroModule.Instance();
        if (module == null || index >= SlotsPerSet) return null;
        var macro = module->GetMacro(set, index);
        if (macro == null || !macro->IsNotEmpty()) return null;

        var lines = new List<string>();
        for (var i = 0; i < MacroRunner.EngineLines; i++) {
            var line = macro->Lines[i].ToString();
            if (!string.IsNullOrWhiteSpace(line)) lines.Add(line);
        }

        var name = macro->Name.ToString();
        return new SavedMacro {
            Name = string.IsNullOrWhiteSpace(name) ? $"Macro {index}" : name,
            Body = string.Join('\n', lines),
        };
    }

    /// <summary>
    /// Writes a one-line game macro that calls the given plugin macro, into the first empty Individual slot,
    /// so it can be dragged onto a hotbar. Returns the slot number, or -1 if every slot is in use.
    /// </summary>
    public static int CreateHotbarStub(SavedMacro source) {
        var module = RaptureMacroModule.Instance();
        if (module == null) return -1;

        for (uint index = 0; index < SlotsPerSet; index++) {
            var macro = module->GetMacro(0, index);
            if (macro == null || macro->IsNotEmpty()) continue;

            var name = source.Name.Trim();
            macro->Clear();
            macro->SetIcon(DefaultMacroIcon);
            macro->Name.SetString(name.Length > NativeNameLength ? name[..NativeNameLength] : name);
            macro->Lines[0].SetString($"/mm {name}");

            // Carry over the macro's own /micon line so the hotbar button shows the same icon.
            var iconLine = source.GetLines().Find(l =>
                l.StartsWith("/micon ", StringComparison.OrdinalIgnoreCase) ||
                l.StartsWith("/macroicon ", StringComparison.OrdinalIgnoreCase));
            if (iconLine != null) macro->Lines[1].SetString(iconLine);

            module->SetSavePendingFlag(true, 0);
            return (int)index;
        }

        return -1;
    }
}
