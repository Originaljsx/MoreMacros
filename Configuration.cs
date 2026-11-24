using System;
using System.Collections.Generic;
using Dalamud.Configuration;

namespace MoreMacros;

/// <summary>One game macro, stored byte for byte so auto-translate phrases and icons survive.</summary>
[Serializable]
public sealed class StoredMacro {
    public uint IconId { get; set; }
    public uint MacroIconRowId { get; set; }
    public byte[] Name { get; set; } = [];
    public byte[][] Lines { get; set; } = [];

    public bool IsEmpty => IconId == 0 && Name.Length == 0 && Array.TrueForAll(Lines, l => l.Length == 0);
}

[Serializable]
public sealed class CharacterData {
    public string CharacterName { get; set; } = string.Empty;

    /// <summary>The job whose set is currently written into the game's Individual slots. 0 = none yet.</summary>
    public uint LoadedJob { get; set; }

    /// <summary>100 Individual macros per ClassJob id.</summary>
    public Dictionary<uint, StoredMacro[]> Sets { get; set; } = [];

    /// <summary>The Individual macros as they were before More Macros first touched them.</summary>
    public StoredMacro[]? OriginalBackup { get; set; }
}

[Serializable]
public sealed class Configuration : IPluginConfiguration {
    public int Version { get; set; } = 2;
    public bool Enabled { get; set; } = true;
    public Dictionary<ulong, CharacterData> Characters { get; set; } = [];

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
