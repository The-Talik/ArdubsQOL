namespace ArdubsQOL;

internal enum OutpostAreaKind
{
    Other,
    GlobalMap,
    Construction
}

internal static class OutpostLoadingRules
{
    internal static bool CanDeferAssetCleanup(
        bool enabled,
        OutpostAreaKind source,
        OutpostAreaKind destination,
        bool loadingSave,
        bool forceUnload,
        bool saveBetweenCleanups)
    {
        if (!enabled || loadingSave || forceUnload || saveBetweenCleanups) return false;
        return (source == OutpostAreaKind.GlobalMap && destination == OutpostAreaKind.Construction)
            || (source == OutpostAreaKind.Construction
                && (destination == OutpostAreaKind.GlobalMap || destination == OutpostAreaKind.Construction));
    }
}
