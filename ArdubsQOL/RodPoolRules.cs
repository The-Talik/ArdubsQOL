using System;
using System.Collections.Generic;

namespace ArdubsQOL;

internal enum RodGrade
{
    Lesser,
    Regular,
    Greater
}

internal static class RodPoolRules
{
    private static readonly string[] Grades = { "Lesser", "Normal", "Greater" };
    private static readonly string[] Metamagics =
    {
        "Bolster", "Empower", "Extend", "Maximize", "Persistent",
        "Quicken", "Reach", "Selective", "Intensified", "Piercing"
    };

    internal static bool IsOrdinaryRod(string? toggleName) =>
        TryGetIconLabel(toggleName, out _, out _);

    internal static bool TryGetIconLabel(string? toggleName, out string label, out RodGrade grade)
    {
        label = string.Empty;
        grade = RodGrade.Regular;
        if (toggleName == null) return false;
        for (var gradeIndex = 0; gradeIndex < Grades.Length; gradeIndex++)
        foreach (var metamagic in Metamagics)
        {
            if (toggleName != $"MetamagicRod{Grades[gradeIndex]}{metamagic}ToggleAbility") continue;
            label = metamagic;
            grade = (RodGrade)gradeIndex;
            return true;
        }
        return false;
    }

    internal static int ChargesInStack(int count, int charges) =>
        (int)Math.Min(int.MaxValue, (long)Math.Max(0, count) * Math.Max(0, charges));

    internal static int TotalCharges<T>(IEnumerable<T> items, Func<T, int> count, Func<T, int> charges)
    {
        long total = 0;
        foreach (var item in items)
            total = Math.Min(int.MaxValue, total + ChargesInStack(count(item), charges(item)));
        return (int)total;
    }

    internal static T? FindChargeSource<T>(IEnumerable<T> items, Func<T, int> charges)
        where T : class
    {
        T? result = null;
        var lowestCharges = int.MaxValue;
        foreach (var item in items)
        {
            var available = charges(item);
            if (available > 0 && (result == null || available < lowestCharges))
            {
                result = item;
                lowestCharges = available;
            }
        }
        return result;
    }

    internal static bool ShouldSpendCharge(
        bool metamagicApplied, bool castOrSpellFailure, bool forcedFailure,
        bool umdFailure, bool duplicate, bool stickyTouch) =>
        metamagicApplied && castOrSpellFailure && !forcedFailure
        && !umdFailure && !duplicate && !stickyTouch;
}
