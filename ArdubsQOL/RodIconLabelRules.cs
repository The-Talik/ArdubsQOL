using System;

namespace ArdubsQOL;

internal static class RodIconLabelRules
{
    // The old bitmap capitals were about one-third of the icon's height. A
    // 16-point game font in a 64-unit icon has capitals roughly 60% that tall.
    internal static float FontSize(float iconHeight)
    {
        if (float.IsNaN(iconHeight) || float.IsInfinity(iconHeight) || iconHeight <= 0f)
            iconHeight = 64f;
        return Math.Max(8f, (float)Math.Round(iconHeight / 8f, MidpointRounding.AwayFromZero) * 2f);
    }
}
