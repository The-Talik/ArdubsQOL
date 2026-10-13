using System;

namespace ArdubsQOL;

// Only the grade border is baked into the sprite. Names use the game's font.
internal static class RodIconPixels
{
    internal static uint GradeColor(RodGrade grade) => grade switch
    {
        RodGrade.Lesser => 0xFF67DFFFu,
        RodGrade.Regular => 0xFFFFD45Bu,
        RodGrade.Greater => 0xFFE59BFFu,
        _ => throw new ArgumentOutOfRangeException(nameof(grade))
    };

    internal static void DrawBorder(uint[] pixels, int width, int height, RodGrade grade)
    {
        if (width < 24 || height < 24 || pixels.Length != (long)width * height)
            throw new ArgumentException("The rod icon must have matching dimensions of at least 24 pixels.");

        var color = GradeColor(grade);
        var border = Math.Max(1, Math.Min(width, height) / 50);
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            if (x < border || x >= width - border || y < border || y >= height - border)
                pixels[y * width + x] = color;
        }
    }
}
