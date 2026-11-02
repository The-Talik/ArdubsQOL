using System;

namespace ArdubsQOL;

internal static class ShifterAspectRules
{
    internal static bool ShouldStopAfterCombat(bool isShifterAspect, bool isOn, bool isStarted) =>
        isShifterAspect && (isOn || isStarted);

    internal static TimeSpan? GetPaidIntervalEnd(
        bool enabled,
        bool isShifterAspect,
        bool spendsOncePerMinute,
        bool hasNextResourceSpendingTime,
        TimeSpan now,
        TimeSpan nextResourceSpendingTime)
    {
        if (!enabled
            || !isShifterAspect
            || !spendsOncePerMinute
            || !hasNextResourceSpendingTime
            || nextResourceSpendingTime <= now)
        {
            return null;
        }

        return nextResourceSpendingTime;
    }
}
