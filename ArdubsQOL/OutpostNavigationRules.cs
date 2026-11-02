using System;

namespace ArdubsQOL;

internal static class OutpostNavigationRules
{
    internal static bool CanSwitch(
        bool enabled, bool inConstruction, bool loading, bool saving, bool pending,
        bool currentOwned, bool targetOwned, bool sameSettlement, bool targetIsConstruction) =>
        enabled && inConstruction && !loading && !saving && !pending
        && currentOwned && targetOwned && !sameSettlement && targetIsConstruction;

    internal static int StepIndex(int index, int direction, int count)
    {
        if (count <= 0) return -1;
        if (index < 0 || index >= count) return direction < 0 ? count - 1 : 0;
        return (int)(((long)index + Math.Sign(direction) + count) % count);
    }

    internal static int VisibleRows(int count) => Math.Max(1, Math.Min(count, 8));
}
