using System.Collections.Generic;
using Lumina.Excel.Sheets;

namespace MoreMacros;

internal static class Jobs {
    private static Dictionary<uint, string>? names;

    public static string Name(uint job) {
        names ??= Load();
        return names.TryGetValue(job, out var name) ? name : $"Job {job}";
    }

    private static Dictionary<uint, string> Load() {
        var result = new Dictionary<uint, string>();
        foreach (var row in Plugin.Data.GetExcelSheet<ClassJob>()) {
            var abbreviation = row.Abbreviation.ExtractText();
            if (abbreviation.Length > 0) result[row.RowId] = abbreviation;
        }

        return result;
    }
}
