using System;

namespace ArdubsQOL;

internal static class AuraHoverPreviewRules
{
    internal static bool CanHighlightBuff(
        bool attached,
        bool permanent,
        bool eligibleForAura,
        bool sameCaster,
        TimeSpan endTime,
        TimeSpan now,
        TimeSpan frozenRemaining)
    {
        return attached && !permanent && eligibleForAura && sameCaster
            && (endTime > now || frozenRemaining > TimeSpan.Zero);
    }

    internal static bool ShouldClearForHover(object? owner, object? slot, bool state)
    {
        return owner != null && (state ? !ReferenceEquals(owner, slot) : ReferenceEquals(owner, slot));
    }
}
