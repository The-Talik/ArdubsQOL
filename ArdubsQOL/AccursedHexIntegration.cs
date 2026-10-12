using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.Visual;
using Owlcat.Runtime.Visual.RenderPipeline.RendererFeatures.Highlighting;
using UnityEngine;

namespace ArdubsQOL;

// Optional, reflection-only integration. Ardub's QOL remains loadable when
// Tabletop Tweaks is absent and does not add any TTT types to saved games.
internal static class AccursedHexIntegration
{
    private const string PartTypeName = "TabletopTweaks.Core.NewUnitParts.UnitPartAccursedHexTTT";
    private const string RetryRestrictionTypeName =
        "TabletopTweaks.Core.NewComponents.AbilitySpecific.AbilityTargetHasNoFactUnlessAccursedHex";
    private static readonly Dictionary<UnitEntityData, Outline> Outlines = new();
    private static Type? partType;
    private static MethodInfo? getPart;
    private static FieldInfo? trackedHexes;
    private static FieldInfo? entryUnit;
    private static FieldInfo? entryGuid;
    private static FieldInfo? entryPassedSave;
    private static FieldInfo? entityRefId;
    private static bool resolved;
    private static bool loggedResolution;
    private static bool warned;

    internal sealed class Retry
    {
        internal readonly UnitEntityData Caster;
        internal readonly UnitEntityData Target;
        internal readonly BlueprintGuid AbilityGuid;
        internal Retry(UnitEntityData caster, UnitEntityData target, BlueprintGuid abilityGuid)
        { Caster = caster; Target = target; AbilityGuid = abilityGuid; }
    }

    private sealed class Outline
    {
        internal UnitMultiHighlight Highlighter = null!;
        internal MultiHighlighter.HighlightSource Source = null!;
    }

    internal static IReadOnlyList<string> LabelsFor(UnitEntityData target)
    {
        if (!Main.Settings.ShowAccursedHexRetries) return Array.Empty<string>();
        return RetriesFor(target)
            .Select(retry => AccursedHexLabelRules.Format(AbilityName(retry.AbilityGuid), retry.Caster.CharacterName))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    internal static void UpdateHighlights()
    {
        try
        {
            var active = new HashSet<UnitEntityData>();
            var ability = Main.Settings.ShowAccursedHexRetries ? Game.Instance?.SelectedAbilityHandler?.SelectedAbility : null;
            var caster = ability?.Caster?.Unit;
            if (caster != null)
            {
                var guid = ability!.Blueprint.AssetGuid;
                foreach (var retry in Entries(caster).Where(retry => retry.AbilityGuid == guid))
                {
                    var target = retry.Target;
                    if (target.View == null || !target.IsVisibleForPlayer || target.Descriptor.State.IsDead) continue;
                    active.Add(target);
                    AddOutline(target);
                }
            }

            foreach (var pair in Outlines.ToArray())
                if (!active.Contains(pair.Key) || pair.Key.View == null ||
                    !ReferenceEquals(pair.Value.Highlighter, pair.Key.View.m_Highlighter))
                    RemoveOutline(pair.Key, pair.Value);
        }
        catch (Exception exception)
        {
            ClearOutlines();
            WarnOnce("Could not update Accursed Hex target outlines: " + exception);
        }
    }

    private static IEnumerable<Retry> RetriesFor(UnitEntityData target)
    {
        var player = Game.Instance?.Player;
        if (player == null) return Enumerable.Empty<Retry>();
        return player.AllCharacters.SelectMany(Entries)
            .Where(retry => retry.Target.UniqueId == target.UniqueId)
            .ToArray();
    }

    private static IEnumerable<Retry> Entries(UnitEntityData caster)
    {
        if (!Resolve()) yield break;
        object? part;
        try { part = getPart!.Invoke(caster, null); }
        catch (Exception exception) { WarnOnce("Could not read Tabletop Tweaks Accursed Hex state: " + exception); yield break; }
        if (part == null || !(trackedHexes!.GetValue(part) is IEnumerable entries)) yield break;
        foreach (var entry in entries)
        {
            if (entry == null || entryPassedSave!.GetValue(entry) is not bool passed) continue;
            var reference = entryUnit!.GetValue(entry);
            var targetId = reference == null ? null : entityRefId!.GetValue(reference)?.ToString();
            var target = Game.Instance?.State.Units.FirstOrDefault(unit => unit.UniqueId == targetId);
            if (target == null || entryGuid!.GetValue(entry) is not BlueprintGuid guid) continue;
            if (!AccursedHexLabelRules.ShouldDisplayRetry(passed, HasPerTargetRestriction(guid))) continue;
            yield return new Retry(caster, target, guid);
        }
    }

    private static bool HasPerTargetRestriction(BlueprintGuid guid)
    {
        var blueprint = ResourcesLibrary.TryGetBlueprint<BlueprintAbility>(guid);
        return blueprint?.ComponentsArray.Any(component =>
            component?.GetType().FullName == RetryRestrictionTypeName) == true;
    }

    private static bool Resolve()
    {
        if (resolved) return true;
        partType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(PartTypeName, false))
            .FirstOrDefault(type => type != null);
        if (partType == null) return false;
        var genericGet = typeof(UnitEntityData).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(method => method.Name == "Get" && method.IsGenericMethodDefinition &&
                method.GetGenericArguments().Length == 1 && method.GetParameters().Length == 0);
        var entryType = partType.GetNestedType("HexData", BindingFlags.Public | BindingFlags.NonPublic);
        trackedHexes = partType.GetField("TrackedHexes", BindingFlags.Instance | BindingFlags.NonPublic);
        entryUnit = entryType?.GetField("Unit", BindingFlags.Instance | BindingFlags.Public);
        entryGuid = entryType?.GetField("Guid", BindingFlags.Instance | BindingFlags.Public);
        entryPassedSave = entryType?.GetField("PassedSave", BindingFlags.Instance | BindingFlags.Public);
        entityRefId = entryUnit?.FieldType.GetField("Id", BindingFlags.Instance | BindingFlags.Public);
        if (genericGet == null || trackedHexes == null || entryUnit == null || entryGuid == null ||
            entryPassedSave == null || entityRefId == null)
        {
            WarnOnce("Tabletop Tweaks Accursed Hex internals were not recognized; its QOL cues are disabled.");
            return false;
        }
        getPart = genericGet.MakeGenericMethod(partType);
        resolved = true;
        if (!loggedResolution)
        {
            loggedResolution = true;
            Main.ModEntry.Logger.Log("Accursed Hex UI: connected to Tabletop Tweaks Core.");
        }
        return true;
    }

    private static string AbilityName(BlueprintGuid guid)
    {
        var blueprint = ResourcesLibrary.TryGetBlueprint<BlueprintAbility>(guid);
        var name = blueprint?.Name?.ToString();
        return string.IsNullOrWhiteSpace(name) ? "Hex" : name!;
    }

    private static void AddOutline(UnitEntityData target)
    {
        var highlighter = target.View?.m_Highlighter;
        if (highlighter == null) return;
        if (Outlines.TryGetValue(target, out var existing))
        {
            if (ReferenceEquals(existing.Highlighter, highlighter)) return;
            RemoveOutline(target, existing);
        }
        var source = new MultiHighlighter.HighlightSource
        {
            Lifetime = float.PositiveInfinity,
            Priority = 100,
            Mode = MultiHighlighter.ModeType.SingleColor,
            Color = new Color(1f, 0.68f, 0.08f, 1f),
            OnsetTime = 0.12f,
            RemoveTime = 0.12f,
            UnscaledTime = true
        };
        highlighter.Play(source);
        Outlines[target] = new Outline { Highlighter = highlighter, Source = source };
    }

    private static void RemoveOutline(UnitEntityData target, Outline outline)
    {
        try { if (outline.Highlighter != null) outline.Highlighter.Stop(outline.Source); }
        catch { /* A view can disappear while an area unloads. */ }
        Outlines.Remove(target);
    }

    private static void ClearOutlines()
    {
        foreach (var pair in Outlines.ToArray()) RemoveOutline(pair.Key, pair.Value);
    }

    private static void WarnOnce(string message)
    {
        if (warned) return;
        warned = true;
        if (Main.ModEntry != null) Main.ModEntry.Logger.Warning(message);
    }
}
