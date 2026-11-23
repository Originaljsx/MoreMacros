using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;

namespace MoreMacros;

/// <summary>
/// Runs arbitrary text through the game's own macro engine.
///
/// The engine only holds 15 lines at a time, so longer macros are split into chunks. Every chunk except the last
/// ends with a hidden <c>/mmcontinue</c> line; when the engine reaches it, the next chunk is started. Because the
/// engine itself triggers the continuation, cancelling the macro (or starting another one) stops the chain naturally.
/// </summary>
internal sealed unsafe class MacroRunner : IDisposable {
    public const string ContinueCommand = "/mmcontinue";
    public const int EngineLines = 15;

    private readonly RaptureMacroModule.Macro* buffer;

    private string name = string.Empty;
    private List<string>? lines;
    private int nextLine;
    private int token;
    private bool wantsLock;
    private bool wantsErrorsOff;

    public MacroRunner() {
        buffer = (RaptureMacroModule.Macro*)NativeMemory.AllocZeroed((nuint)sizeof(RaptureMacroModule.Macro));
        buffer->Name.Ctor();
        for (var i = 0; i < EngineLines; i++) buffer->Lines[i].Ctor();
    }

    /// <summary>How many engine macros a macro of this many lines is split into.</summary>
    public static int ChunkCount(int lineCount) {
        if (lineCount <= EngineLines) return 1;
        // Every chunk but the last carries 14 real lines; the last carries up to 15.
        return (lineCount - EngineLines + (EngineLines - 2)) / (EngineLines - 1) + 1;
    }

    /// <summary>Starts a macro. Must be called on the framework thread, outside of macro execution.</summary>
    public bool Start(string macroName, List<string> macroLines, out string error) {
        error = string.Empty;
        var shell = RaptureShellModule.Instance();
        if (shell == null) {
            error = "The game is not ready to run macros yet.";
            return false;
        }

        if (shell->MacroLocked) {
            error = "Another macro is running with /macrolock.";
            return false;
        }

        if (macroLines.Count == 0) {
            error = $"Macro \"{macroName}\" is empty.";
            return false;
        }

        name = macroName;
        lines = macroLines;
        nextLine = 0;
        wantsLock = false;
        wantsErrorsOff = false;
        token = unchecked(token + 1);
        ExecuteNextChunk(shell);
        return true;
    }

    /// <summary>Called when the engine reaches a <c>/mmcontinue</c> line.</summary>
    public void Continue(string args) {
        if (lines == null || !int.TryParse(args.Trim(), out var requested) || requested != token) return;
        // We are inside the engine's line processing here, so start the next chunk on the following frame.
        Plugin.Framework.RunOnTick(() => {
            if (lines == null || requested != token) return;
            var shell = RaptureShellModule.Instance();
            if (shell == null) return;
            // The lock (if any) belongs to this chain; release it so the next chunk is allowed to start.
            if (wantsLock) shell->MacroLocked = false;
            if (shell->MacroLocked) return;
            ExecuteNextChunk(shell);
        });
    }

    private void ExecuteNextChunk(RaptureShellModule* shell) {
        var all = lines!;
        var remaining = all.Count - nextLine;
        var isLast = remaining <= EngineLines;
        var take = isLast ? remaining : EngineLines - 1;

        // Whatever earlier chunks of this macro asked for; starting a macro resets it in the engine.
        var isFirst = nextLine == 0;
        var carryLock = wantsLock;
        var carryErrorsOff = wantsErrorsOff;

        buffer->Name.SetString(name);
        var slot = 0;
        for (; slot < take; slot++) {
            var line = all[nextLine + slot];
            buffer->Lines[slot].SetString(line);
            NoteFlags(line);
        }

        if (!isLast) buffer->Lines[slot++].SetString($"{ContinueCommand} {token}");
        for (; slot < EngineLines; slot++) buffer->Lines[slot].SetString(string.Empty);

        nextLine += take;
        if (isLast) lines = null;

        shell->ExecuteMacro(buffer);

        if (!isFirst) {
            if (carryLock) shell->MacroLocked = true;
            if (carryErrorsOff) shell->SuppressMacroErrors = true;
        }
    }

    private void NoteFlags(string line) {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;
        switch (parts[0].ToLowerInvariant()) {
            case "/macrolock" or "/mlock":
                wantsLock = true;
                break;
            case "/macroerror" or "/merror" when parts.Length > 1:
                wantsErrorsOff = parts[1].Equals("off", StringComparison.OrdinalIgnoreCase);
                break;
        }
    }

    public void Dispose() {
        lines = null;
        token = unchecked(token + 1);
        buffer->Name.Dtor();
        for (var i = 0; i < EngineLines; i++) buffer->Lines[i].Dtor();
        NativeMemory.Free(buffer);
    }
}
