namespace ArdubsQOL;

internal static class CasterLabelRules
{
    internal static bool ShouldAnnotate(
        bool isKnownEvilEye,
        int sameNameCount,
        bool hasHostileCaster)
    {
        return hasHostileCaster && (isKnownEvilEye || sameNameCount > 1);
    }
}
