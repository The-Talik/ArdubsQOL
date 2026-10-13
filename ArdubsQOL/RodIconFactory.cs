using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArdubsQOL;

internal static class RodIconFactory
{
    private static readonly Dictionary<(int SourceId, string ToggleName), Sprite> Icons = new();
    private static readonly List<UnityEngine.Object> RuntimeAssets = new();

    internal static Sprite? WithGradeBorder(Sprite? source, string toggleName)
    {
        if (source == null || source.texture == null
            || !RodPoolRules.TryGetIconLabel(toggleName, out var label, out var grade)) return source;
        var key = (source.GetInstanceID(), toggleName);
        if (Icons.TryGetValue(key, out var cached) && cached != null) return cached;

        RenderTexture? renderTexture = null;
        Texture2D? labeledTexture = null;
        Sprite? labeledSprite = null;
        var previousRenderTexture = RenderTexture.active;
        try
        {
            var sourceRect = source.textureRect;
            var width = Math.Max(1, Mathf.RoundToInt(sourceRect.width));
            var height = Math.Max(1, Mathf.RoundToInt(sourceRect.height));
            var texture = source.texture;
            renderTexture = RenderTexture.GetTemporary(width, height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            Graphics.Blit(texture, renderTexture,
                new Vector2(sourceRect.width / texture.width, sourceRect.height / texture.height),
                new Vector2(sourceRect.x / texture.width, sourceRect.y / texture.height));
            RenderTexture.active = renderTexture;
            labeledTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = $"ArdubsQOL_{toggleName}_Texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            labeledTexture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
            var colors = labeledTexture.GetPixels32();
            var pixels = new uint[colors.Length];
            for (var index = 0; index < colors.Length; index++)
            {
                var c = colors[index];
                pixels[index] = (uint)c.a << 24 | (uint)c.r << 16 | (uint)c.g << 8 | c.b;
            }
            RodIconPixels.DrawBorder(pixels, width, height, grade);
            for (var index = 0; index < colors.Length; index++)
            {
                var p = pixels[index];
                colors[index] = new Color32((byte)(p >> 16), (byte)(p >> 8), (byte)p, (byte)(p >> 24));
            }
            labeledTexture.SetPixels32(colors);
            labeledTexture.Apply(false, false);
            labeledSprite = Sprite.Create(labeledTexture, new Rect(0f, 0f, width, height),
                new Vector2(source.pivot.x / source.rect.width, source.pivot.y / source.rect.height),
                source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            labeledSprite.name = $"ArdubsQOL_{toggleName}_Icon";
            UnityEngine.Object.DontDestroyOnLoad(labeledTexture);
            UnityEngine.Object.DontDestroyOnLoad(labeledSprite);
            RuntimeAssets.Add(labeledTexture);
            RuntimeAssets.Add(labeledSprite);
            Icons[key] = labeledSprite;
            return labeledSprite;
        }
        catch (Exception exception)
        {
            if (labeledSprite != null) UnityEngine.Object.Destroy(labeledSprite);
            if (labeledTexture != null) UnityEngine.Object.Destroy(labeledTexture);
            Icons[key] = source;
            Main.ModEntry.Logger.Warning($"Could not frame the {label} rod icon; using its original icon: {exception.Message}");
            return source;
        }
        finally
        {
            RenderTexture.active = previousRenderTexture;
            if (renderTexture != null) RenderTexture.ReleaseTemporary(renderTexture);
        }
    }
}
