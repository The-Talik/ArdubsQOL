using System.Collections.Generic;
using System.Linq;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UI.Selection;
using Kingmaker.Visual;
using Kingmaker.Visual.Decals;
using UnityEngine;

namespace ArdubsQOL;

internal static class AccursedHexGroundCircle
{
    private static readonly Dictionary<UnitEntityData, Circle> Circles = new();
    private static readonly Color Gold = new(1f, 0.68f, 0.08f, 1f);

    private sealed class Circle
    {
        internal UIDecal Mark = null!;
        internal GUIDecal Template = null!;
        internal GUIDecal Decal = null!;

        internal bool IsValid(UnitEntityData target) =>
            target.View != null && Mark != null && Mark.Unit == target && Mark.isActiveAndEnabled &&
            Template != null && Template.transform.IsChildOf(target.View.transform) && Decal != null;
    }

    internal static void Update(ISet<UnitEntityData> active)
    {
        foreach (var pair in Circles.ToArray())
            if (!active.Contains(pair.Key) || !pair.Value.IsValid(pair.Key))
                Remove(pair.Key, pair.Value);

        foreach (var target in active)
        {
            if (!Circles.TryGetValue(target, out var circle))
            {
                circle = Create(target);
                if (circle == null) continue;
                Circles.Add(target, circle);
            }
            CopyTransform(circle.Template.transform, circle.Decal.transform);
        }
    }

    private static Circle? Create(UnitEntityData target)
    {
        // Reuse only the native circle's artwork, not its hover state or event handlers.
        var mark = target.View?.GetComponentsInChildren<UIDecal>(true)
            .FirstOrDefault(candidate => candidate.Unit == target && candidate.isActiveAndEnabled);
        var template = mark?.Normal?.GetComponentInChildren<GUIDecal>(true);
        if (mark == null || template == null || template.SharedMaterial == null) return null;

        var gameObject = new GameObject("Accursed Hex retry circle")
        {
            hideFlags = HideFlags.DontSave,
            layer = template.gameObject.layer
        };
        gameObject.SetActive(false);
        try
        {
            gameObject.transform.SetParent(template.transform.parent, false);
            CopyTransform(template.transform, gameObject.transform);
            var decal = gameObject.AddComponent<GUIDecal>();
            decal.m_Material = template.SharedMaterial;
            decal.m_ValidateHeight = template.m_ValidateHeight;
            decal.m_RaycastMask = template.m_RaycastMask;
            decal.Layer = mark.GetComponentsInChildren<GUIDecal>(true)
                .Where(candidate => candidate != decal).Max(candidate => candidate.Layer) + 1;
            decal.MaterialProperties.SetBaseColor(Gold);
            gameObject.SetActive(true);
            return new Circle { Mark = mark, Template = template, Decal = decal };
        }
        catch
        {
            gameObject.SetActive(false);
            Object.Destroy(gameObject);
            throw;
        }
    }

    private static void CopyTransform(Transform source, Transform destination)
    {
        destination.localPosition = source.localPosition;
        destination.localRotation = source.localRotation;
        destination.localScale = source.localScale;
    }

    private static void Remove(UnitEntityData target, Circle circle)
    {
        if (circle.Decal != null)
        {
            circle.Decal.gameObject.SetActive(false);
            Object.Destroy(circle.Decal.gameObject);
        }
        Circles.Remove(target);
    }

    internal static void Clear()
    {
        foreach (var pair in Circles.ToArray()) Remove(pair.Key, pair.Value);
    }
}
