using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArdubsQOL;

internal static class AuraIconFactory
{
    private static readonly List<UnityEngine.Object> RuntimeAssets = new();
    private static readonly Color32 Gold = new(255, 216, 64, 255);

    internal static Sprite? AddGoldFrame(Sprite? source, string name)
    {
        if (source == null || source.texture == null)
        {
            return source;
        }

        RenderTexture? renderTexture = null;
        var previousRenderTexture = RenderTexture.active;

        try
        {
            var sourceRect = source.textureRect;
            var width = Math.Max(1, Mathf.RoundToInt(sourceRect.width));
            var height = Math.Max(1, Mathf.RoundToInt(sourceRect.height));

            renderTexture = RenderTexture.GetTemporary(
                width,
                height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default);

            var texture = source.texture;
            var scale = new Vector2(
                sourceRect.width / texture.width,
                sourceRect.height / texture.height);
            var offset = new Vector2(
                sourceRect.x / texture.width,
                sourceRect.y / texture.height);

            Graphics.Blit(texture, renderTexture, scale, offset);
            RenderTexture.active = renderTexture;

            var framedTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = $"{name}Texture",
                filterMode = texture.filterMode,
                wrapMode = TextureWrapMode.Clamp
            };
            framedTexture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);

            var pixels = framedTexture.GetPixels32();
            DrawGoldFrame(pixels, width, height);
            framedTexture.SetPixels32(pixels);
            framedTexture.Apply(false, false);

            var pivot = new Vector2(
                source.pivot.x / source.rect.width,
                source.pivot.y / source.rect.height);
            var framedSprite = Sprite.Create(
                framedTexture,
                new Rect(0f, 0f, width, height),
                pivot,
                source.pixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
            framedSprite.name = name;

            UnityEngine.Object.DontDestroyOnLoad(framedTexture);
            UnityEngine.Object.DontDestroyOnLoad(framedSprite);
            RuntimeAssets.Add(framedTexture);
            RuntimeAssets.Add(framedSprite);
            return framedSprite;
        }
        catch (Exception exception)
        {
            Main.ModEntry.Logger.Warning(
                $"Could not create the gold-framed aura icon; using the original icon instead: {exception.Message}");
            return source;
        }
        finally
        {
            RenderTexture.active = previousRenderTexture;
            if (renderTexture != null)
            {
                RenderTexture.ReleaseTemporary(renderTexture);
            }
        }
    }

    private static void DrawGoldFrame(Color32[] pixels, int width, int height)
    {
        var shortSide = Math.Min(width, height);
        var solidWidth = Math.Max(2, Mathf.RoundToInt(shortSide * 0.045f));
        var glowWidth = solidWidth + Math.Max(3, Mathf.RoundToInt(shortSide * 0.075f));

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var distanceFromEdge = Math.Min(Math.Min(x, width - 1 - x), Math.Min(y, height - 1 - y));
                if (distanceFromEdge >= glowWidth)
                {
                    continue;
                }

                byte opacity;
                if (distanceFromEdge < solidWidth)
                {
                    opacity = 255;
                }
                else
                {
                    var glowProgress = 1f -
                        (distanceFromEdge - solidWidth) / (float)(glowWidth - solidWidth);
                    opacity = (byte)Mathf.RoundToInt(200f * glowProgress);
                }

                var index = y * width + x;
                pixels[index] = Blend(pixels[index], Gold, opacity);
            }
        }
    }

    private static Color32 Blend(Color32 background, Color32 foreground, byte opacity)
    {
        var foregroundWeight = opacity / 255f;
        var backgroundWeight = 1f - foregroundWeight;
        return new Color32(
            (byte)Mathf.RoundToInt(background.r * backgroundWeight + foreground.r * foregroundWeight),
            (byte)Mathf.RoundToInt(background.g * backgroundWeight + foreground.g * foregroundWeight),
            (byte)Mathf.RoundToInt(background.b * backgroundWeight + foreground.b * foregroundWeight),
            Math.Max(background.a, opacity));
    }
}
