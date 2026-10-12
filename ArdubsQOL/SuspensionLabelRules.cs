using System;

namespace ArdubsQOL;

internal enum AuraSuspensionKind
{
    None,
    Chant,
    Cackle
}

internal static class SuspensionLabelRules
{
    private const string ChantSuffix = " (Chant)";
    private const string CackleSuffix = " (Cackle)";

    internal static string Apply(string name, AuraSuspensionKind kind)
    {
        var baseName = RemoveExistingSuffix(name);
        return kind switch
        {
            AuraSuspensionKind.Chant => baseName + ChantSuffix,
            AuraSuspensionKind.Cackle => baseName + CackleSuffix,
            _ => baseName
        };
    }

    private static string RemoveExistingSuffix(string name)
    {
        if (name.EndsWith(ChantSuffix, StringComparison.Ordinal))
        {
            return name.Substring(0, name.Length - ChantSuffix.Length);
        }

        if (name.EndsWith(CackleSuffix, StringComparison.Ordinal))
        {
            return name.Substring(0, name.Length - CackleSuffix.Length);
        }

        return name;
    }
}
