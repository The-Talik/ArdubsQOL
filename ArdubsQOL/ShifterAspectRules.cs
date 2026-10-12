using System;

namespace ArdubsQOL;

internal static class ShifterAspectRules
{
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
