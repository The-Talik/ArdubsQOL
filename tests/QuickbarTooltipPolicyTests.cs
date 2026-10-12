using ArdubsQOL;

internal static class QuickbarTooltipPolicyTests
{
    internal static void Run()
    {
        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        Check(QuickbarTooltipRules.ResolveSide(QuickbarTooltipSide.Auto, 0.2f) == QuickbarTooltipSide.Right,
            "A character left of center should put its tooltip on the right.");
        Check(QuickbarTooltipRules.ResolveSide(QuickbarTooltipSide.Auto, 0.8f) == QuickbarTooltipSide.Left,
            "A character right of center should put its tooltip on the left.");
        Check(QuickbarTooltipRules.ResolveSide(QuickbarTooltipSide.Auto, 0.5f) == QuickbarTooltipSide.Right,
            "A centered character should use a stable, deterministic side.");
        foreach (var x in new float?[] { null, float.NaN, float.PositiveInfinity })
        {
            Check(QuickbarTooltipRules.ResolveSide(QuickbarTooltipSide.Auto, x) == QuickbarTooltipSide.Right,
                "Unavailable screen positions should fall back to the right.");
        }
        Check(QuickbarTooltipRules.ResolveSide(QuickbarTooltipSide.Left, 0.1f) == QuickbarTooltipSide.Left,
            "Explicit Left must not change with character position.");
        Check(QuickbarTooltipRules.ResolveSide(QuickbarTooltipSide.Right, 0.9f) == QuickbarTooltipSide.Right,
            "Explicit Right must not change with character position.");
        Check(QuickbarTooltipRules.ResolveSide((QuickbarTooltipSide)42, 0.8f) == QuickbarTooltipSide.Left,
            "Invalid settings should normalize to Auto.");
        Check(QuickbarTooltipRules.ResolveSide(QuickbarTooltipSide.Auto, -0.1f) == QuickbarTooltipSide.Right,
            "An offscreen character on the left should still choose the right side.");
        Check(QuickbarTooltipRules.HorizontalOffset(QuickbarTooltipSide.Left, 700f, 515f, 0f, 1920f, 16f) == -684f,
            "Left placement should retain a small gap at the far-left edge.");
        Check(QuickbarTooltipRules.HorizontalOffset(QuickbarTooltipSide.Right, 700f, 515f, 0f, 1920f, 16f) == 689f,
            "Right placement should retain a small gap at the far-right edge.");
        Check(QuickbarTooltipRules.HorizontalOffset(QuickbarTooltipSide.Right, 300f, 515f, 100f, 800f, 16f) == 69f,
            "A nonzero camera viewport origin must be included.");
        Check(QuickbarTooltipRules.HorizontalOffset(QuickbarTooltipSide.Left, 200f, 790f, 0f, 800f, 16f) == -190f,
            "The margin should shrink if necessary to keep a wide tooltip on screen.");
        Check(QuickbarTooltipRules.HorizontalOffset(QuickbarTooltipSide.Right, 100f, 900f, 0f, 800f, 16f) == -100f,
            "Oversized tooltips should not receive a negative left edge.");
        Check(QuickbarTooltipRules.VerticalOffset(0f, 700f, 0f, 1080f) == 190f,
            "Normal tooltips should be vertically centered.");
        Check(QuickbarTooltipRules.VerticalOffset(20f, 300f, 100f, 900f) == 380f,
            "Vertical placement should respect the viewport origin.");
        Check(QuickbarTooltipRules.VerticalOffset(200f, 900f, 0f, 800f) == -200f,
            "Oversized tooltips should preserve access to their lower edge.");
        Console.WriteLine("Quickbar tooltip placement policy tests passed.");
    }
}
