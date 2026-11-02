using System;

namespace ArdubsQOL;

internal static class InfirmaryPreviewRules
{
    internal static bool CanDisplay(bool enabled, bool healing, bool crusader, bool summoned) =>
        enabled && healing && crusader && !summoned;

    internal static void GetRange(int minRestored, int maxRestored, int originalCount,
        int currentCount, int unitExperience, out long low, out long high)
    {
        var original = Math.Max(0, originalCount);
        var current = Math.Max(0, Math.Min(original, currentCount));
        var missing = original - current;
        var first = (long)Math.Min(missing, Math.Max(0, minRestored)) * Math.Max(0, unitExperience);
        var second = (long)Math.Min(missing, Math.Max(0, maxRestored)) * Math.Max(0, unitExperience);
        low = Math.Min(first, second);
        high = Math.Max(first, second);
    }

    internal static string FormatRange(long low, long high) =>
        low == high ? $"<b>{low}</b>" : $"<b>{low} - {high}</b>";
}
