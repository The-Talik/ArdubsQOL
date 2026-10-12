namespace ArdubsQOL;

internal static class AccursedHexLabelRules
{
    internal static bool ShouldDisplayRetry(bool passedSave, bool hasPerTargetRestriction) =>
        passedSave && hasPerTargetRestriction;

    internal static string Format(string hexName, string casterName) =>
        $"Accursed Hex retry: {hexName} ({casterName})";
}
