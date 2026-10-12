using System;

namespace ArdubsQOL;

public enum QuickbarTooltipSide
{
    Auto,
    Left,
    Right
}

internal static class QuickbarTooltipRules
{
    internal static QuickbarTooltipSide Normalize(QuickbarTooltipSide side)
    {
        return side == QuickbarTooltipSide.Left || side == QuickbarTooltipSide.Right
            ? side
            : QuickbarTooltipSide.Auto;
    }

    internal static QuickbarTooltipSide ResolveSide(QuickbarTooltipSide side, float? characterViewportX)
    {
        side = Normalize(side);
        if (side != QuickbarTooltipSide.Auto)
        {
            return side;
        }

        // If the character cannot be projected, use the right-hand side consistently.
        return characterViewportX.HasValue && !float.IsNaN(characterViewportX.Value)
            && !float.IsInfinity(characterViewportX.Value) && characterViewportX.Value > 0.5f
            ? QuickbarTooltipSide.Left
            : QuickbarTooltipSide.Right;
    }

    internal static float HorizontalOffset(
        QuickbarTooltipSide side, float tooltipLeft, float tooltipWidth,
        float viewportLeft, float viewportWidth, float margin)
    {
        var desiredLeft = side == QuickbarTooltipSide.Left
            ? viewportLeft + margin
            : viewportLeft + viewportWidth - margin - tooltipWidth;
        var latestLeft = viewportLeft + Math.Max(0f, viewportWidth - tooltipWidth);
        return Math.Max(viewportLeft, Math.Min(desiredLeft, latestLeft)) - tooltipLeft;
    }

    internal static float VerticalOffset(
        float tooltipBottom, float tooltipHeight, float viewportBottom, float viewportHeight)
    {
        var desiredBottom = viewportBottom + Math.Max(0f, (viewportHeight - tooltipHeight) / 2f);
        return desiredBottom - tooltipBottom;
    }
}
