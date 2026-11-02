using System;

namespace ArdubsQOL;

internal static class InfirmaryBreakdownRules
{
    internal static int MissingCount(int original, int current) =>
        Math.Max(0, original) - Math.Max(0, Math.Min(Math.Max(0, original), current));

    internal static long Points(int count, int unitValue) => (long)Math.Max(0, count) * Math.Max(0, unitValue);

    internal static string FormatCost(string name, int count, int unitValue) =>
        $"{name}: {Math.Max(0, count):N0} × {Math.Max(0, unitValue):N0} = {Points(count, unitValue):N0} points";
}
