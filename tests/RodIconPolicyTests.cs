using ArdubsQOL;

internal static class RodIconPolicyTests
{
    internal static void Run()
    {
        var types = new[]
        {
            "Bolster", "Empower", "Extend", "Maximize", "Persistent",
            "Quicken", "Reach", "Selective", "Intensified", "Piercing"
        };
        var grades = new Dictionary<string, RodGrade>
        {
            ["Lesser"] = RodGrade.Lesser, ["Normal"] = RodGrade.Regular, ["Greater"] = RodGrade.Greater
        };
        Check(grades.Values.Select(RodIconPixels.GradeColor).Distinct().Count() == 3,
            "Each rod grade must have a distinct color.");
        foreach (var type in types)
        foreach (var grade in grades)
        {
            Check(RodPoolRules.TryGetIconLabel($"MetamagicRod{grade.Key}{type}ToggleAbility",
                out var label, out var resolvedGrade), "Every ordinary rod should have an icon label.");
            Check(label == type && resolvedGrade == grade.Value,
                "Labels must retain the whole metamagic word, not an abbreviation or grade prefix.");
            foreach (var size in new[] { 24, 32, 64, 128, 256 })
                CheckBorder(size, size, resolvedGrade);
        }
        Check(!RodPoolRules.TryGetIconLabel("MetamagicRodMasterpieceToggleAbility", out _, out _),
            "Unique rods must not be mislabeled as ordinary metamagic rods.");
        Check(!RodPoolRules.TryGetIconLabel(null, out _, out _), "Missing rod names must be handled safely.");
        CheckBorder(96, 64, RodGrade.Greater);
        Check(RodIconLabelRules.FontSize(64f) == 16f,
            "A standard icon should use a normal 16-point font, roughly 60% of the old capital height.");
        Check(RodIconLabelRules.FontSize(128f) == 32f, "The label should scale with the UI icon.");
        foreach (var invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            Check(RodIconLabelRules.FontSize(invalid) == 16f, "Unresolved UI layout should have a safe default.");
        Console.WriteLine("All 30 full-word rod labels, grade borders, and font sizing tests passed.");
    }

    private static void CheckBorder(int width, int height, RodGrade grade)
    {
        const uint original = 0x80A07040u;
        var pixels = Enumerable.Repeat(original, width * height).ToArray();
        RodIconPixels.DrawBorder(pixels, width, height, grade);
        var color = RodIconPixels.GradeColor(grade);
        var border = Math.Max(1, Math.Min(width, height) / 50);
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var isBorder = x < border || x >= width - border || y < border || y >= height - border;
            Check(pixels[y * width + x] == (isBorder ? color : original),
                "Only the border pixels may change: no black strip, bitmap letters, or altered artwork.");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
