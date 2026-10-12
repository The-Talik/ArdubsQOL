using System;

namespace ArdubsQOL;

internal static class HoverDurationRules
{
    internal static string Append(string name, bool isPermanent, TimeSpan timeLeft, string compactTime)
    {
        if (isPermanent)
        {
            return name;
        }

        var duration = timeLeft <= TimeSpan.Zero ? "0s" : compactTime;
        return string.IsNullOrWhiteSpace(duration) ? name : $"{name} {duration}";
    }
}
