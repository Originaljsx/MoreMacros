using System.Numerics;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace MoreMacros;

/// <summary>Places macros onto the game's normal hotbars (not the cross hotbar).</summary>
internal static unsafe class Hotbars {
    private static readonly string[] AddonNames = [
        "_ActionBar", "_ActionBar01", "_ActionBar02", "_ActionBar03", "_ActionBar04",
        "_ActionBar05", "_ActionBar06", "_ActionBar07", "_ActionBar08", "_ActionBar09",
    ];

    /// <summary>
    /// Puts the macro on the hotbar slot under the given screen position.
    /// Returns a message for the user, or null if the position is not over a hotbar.
    /// </summary>
    public static string? DropAt(Vector2 imguiPos, SavedMacro macro) {
        if (!FindSlotAt(imguiPos, out var addon, out var hotbarId, out var slotIndex)) return null;

        if (addon->IsLocked) return "That hotbar is locked.";

        var commandId = GameMacros.FindOrCreateLauncher(macro);
        if (commandId == null) return "No empty Individual macro slot is available for the hotbar button.";

        var module = RaptureHotbarModule.Instance();
        if (module == null) return "The game is not ready yet.";

        module->SetAndSaveSlot(hotbarId, slotIndex, RaptureHotbarModule.HotbarSlotType.Macro, commandId.Value);
        addon->PulseActionBarSlot((int)slotIndex);
        return $"Placed \"{macro.Name.Trim()}\" on hotbar {hotbarId + 1}, slot {slotIndex + 1}.";
    }

    private static bool FindSlotAt(Vector2 imguiPos, out AddonActionBarBase* hit, out uint hotbarId, out uint slotIndex) {
        hit = null;
        hotbarId = 0;
        slotIndex = 0;
        var pos = imguiPos - ImGuiHelpers.MainViewport.Pos;

        foreach (var name in AddonNames) {
            var addon = (AddonActionBarBase*)Plugin.GameGui.GetAddonByName(name).Address;
            if (addon == null || !addon->IsVisible) continue;

            var slots = addon->ActionBarSlotVector;
            var count = (int)slots.LongCount;
            for (var i = 0; i < count && i < addon->SlotCount; i++) {
                var slot = slots[i];
                if (slot.ComponentDragDrop == null) continue;
                var node = (AtkResNode*)slot.ComponentDragDrop->OwnerNode;
                if (node == null || !node->IsVisible()) continue;

                var scale = TotalScale(node);
                var min = new Vector2(node->ScreenX, node->ScreenY);
                var max = min + new Vector2(node->Width, node->Height) * scale;
                if (pos.X < min.X || pos.Y < min.Y || pos.X >= max.X || pos.Y >= max.Y) continue;

                hit = addon;
                hotbarId = addon->RaptureHotbarId;
                slotIndex = (uint)i;
                return true;
            }
        }

        return false;
    }

    private static float TotalScale(AtkResNode* node) {
        var scale = 1f;
        for (var n = node; n != null; n = n->ParentNode) scale *= n->ScaleX;
        return scale;
    }
}
