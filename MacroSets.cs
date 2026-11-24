using System;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace MoreMacros;

/// <summary>
/// Keeps a separate set of the game's 100 Individual macros for every job, and swaps them in when the job changes.
/// Shared macros are never touched.
/// </summary>
internal sealed unsafe class MacroSets {
    public const int Slots = 100;
    private const int LinesPerMacro = 15;
    private const uint IndividualSet = 0;

    /// <summary>Wait this long after the game's macro file is ready before the first swap of a session.</summary>
    private static readonly TimeSpan SettleTime = TimeSpan.FromSeconds(2);

    private readonly Configuration config;
    private ulong readyFor;
    private DateTime readySince;

    public MacroSets(Configuration config) => this.config = config;

    public string LastMessage { get; private set; } = string.Empty;

    /// <summary>The current character's data, or null when not logged in or the macro file is not loaded yet.</summary>
    public CharacterData? Current {
        get {
            if (!IsReady(out var contentId)) return null;
            if (!config.Characters.TryGetValue(contentId, out var data)) {
                data = new CharacterData();
                config.Characters[contentId] = data;
            }

            var name = Plugin.ObjectTable.LocalPlayer?.Name.TextValue;
            if (!string.IsNullOrEmpty(name)) data.CharacterName = name;
            return data;
        }
    }

    public bool IsMacroWindowOpen => MacroWindowOpen;

    private static bool MacroWindowOpen {
        get {
            var addon = (FFXIVClientStructs.FFXIV.Component.GUI.AtkUnitBase*)Plugin.GameGui.GetAddonByName("Macro").Address;
            return addon != null && addon->IsVisible;
        }
    }

    public static uint CurrentJob => Plugin.PlayerState.IsLoaded ? Plugin.PlayerState.ClassJob.RowId : 0;

    private static bool IsReady(out ulong contentId) {
        contentId = 0;
        if (!Plugin.ClientState.IsLoggedIn || !Plugin.PlayerState.IsLoaded) return false;
        contentId = Plugin.PlayerState.ContentId;
        var module = RaptureMacroModule.Instance();
        return contentId != 0 && module != null && module->CharacterContentId == contentId;
    }

    /// <summary>Called every frame. Cheap unless a swap is needed.</summary>
    public void Update() {
        if (!config.Enabled || !IsReady(out var contentId)) {
            readyFor = 0;
            return;
        }

        if (readyFor != contentId) {
            readyFor = contentId;
            readySince = DateTime.UtcNow;
        }

        if (DateTime.UtcNow - readySince < SettleTime) return;

        var job = CurrentJob;
        var data = Current;
        if (job == 0 || data == null || data.LoadedJob == job) return;

        // The game's macro window keeps its own copy of the macro being edited. Wait until it is closed so it
        // can't write that copy over the new job's macros.
        if (MacroWindowOpen) {
            LastMessage = "Close the game's macro window to load this job's macros.";
            return;
        }

        SwapTo(data, job);
    }

    private void SwapTo(CharacterData data, uint job) {
        var current = Snapshot();
        data.OriginalBackup ??= current;

        // Keep whatever is in the slots now (including edits made in the game's macro window) for the job it belongs to.
        if (data.LoadedJob != 0) data.Sets[data.LoadedJob] = current;

        if (data.Sets.TryGetValue(job, out var set)) {
            Write(set);
        } else {
            // First time on this job: it starts with a copy of the macros you have now.
            data.Sets[job] = Clone(current);
        }

        data.LoadedJob = job;
        config.Save();
        LastMessage = $"Loaded the macro set for {Jobs.Name(job)}.";
    }

    /// <summary>Replaces the current job's macros with a copy of another job's set.</summary>
    public bool CopyFrom(uint sourceJob) {
        var data = Current;
        if (data == null || data.LoadedJob == 0 || !data.Sets.TryGetValue(sourceJob, out var source)) return false;
        if (sourceJob == data.LoadedJob) return true;
        Write(source);
        data.Sets[data.LoadedJob] = Clone(source);
        config.Save();
        LastMessage = $"Copied {Jobs.Name(sourceJob)}'s macros to {Jobs.Name(data.LoadedJob)}.";
        return true;
    }

    /// <summary>Writes the macros from before More Macros was installed back into the slots, for the current job.</summary>
    public bool RestoreOriginal() {
        var data = Current;
        if (data?.OriginalBackup == null || data.LoadedJob == 0) return false;
        Write(data.OriginalBackup);
        data.Sets[data.LoadedJob] = Clone(data.OriginalBackup);
        config.Save();
        LastMessage = $"Restored your original macros on {Jobs.Name(data.LoadedJob)}.";
        return true;
    }

    /// <summary>Saves edits made in the game's macro window into the current job's set.</summary>
    public void SaveCurrent() {
        var data = Current;
        if (data == null || data.LoadedJob == 0) return;
        data.Sets[data.LoadedJob] = Snapshot();
        config.Save();
    }

    public static int CountUsed(StoredMacro[] set) => Array.FindAll(set, m => !m.IsEmpty).Length;

    private static StoredMacro[] Snapshot() {
        var module = RaptureMacroModule.Instance();
        var result = new StoredMacro[Slots];
        for (uint i = 0; i < Slots; i++) {
            var macro = module->GetMacro(IndividualSet, i);
            var lines = new byte[LinesPerMacro][];
            for (var l = 0; l < LinesPerMacro; l++) lines[l] = macro->Lines[l].AsSpan().ToArray();
            result[i] = new StoredMacro {
                IconId = macro->IconId,
                MacroIconRowId = macro->MacroIconRowId,
                Name = macro->Name.AsSpan().ToArray(),
                Lines = lines,
            };
        }

        return result;
    }

    private static void Write(StoredMacro[] set) {
        var module = RaptureMacroModule.Instance();
        for (uint i = 0; i < Slots; i++) {
            var macro = module->GetMacro(IndividualSet, i);
            var stored = i < set.Length ? set[i] : new StoredMacro();
            macro->IconId = stored.IconId;
            macro->MacroIconRowId = stored.MacroIconRowId;
            macro->Name.SetString(stored.Name);
            for (var l = 0; l < LinesPerMacro; l++) {
                macro->Lines[l].SetString(l < stored.Lines.Length ? stored.Lines[l] : []);
            }
        }

        module->SetSavePendingFlag(true, IndividualSet);

        // Refresh hotbar icons and names that point at these macros.
        var hotbars = RaptureHotbarModule.Instance();
        if (hotbars != null) hotbars->ReloadAllMacroSlots();
    }

    private static StoredMacro[] Clone(StoredMacro[] set) =>
        Array.ConvertAll(set, m => new StoredMacro {
            IconId = m.IconId,
            MacroIconRowId = m.MacroIconRowId,
            Name = (byte[])m.Name.Clone(),
            Lines = Array.ConvertAll(m.Lines, l => (byte[])l.Clone()),
        });
}
