using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.Blueprints.Root;
using Kingmaker.Inspect;
using Kingmaker.UI.Common;
using Kingmaker.UI.MVVM._VM.Tooltip.Bricks;
using Kingmaker.UI.MVVM._VM.Tooltip.Templates;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Buffs;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Owlcat.Runtime.UI.Tooltips;

namespace ArdubsQOL;

internal static class HoverInspectorCasterNames
{
    private static readonly HashSet<BlueprintGuid> EvilEyeBuffs = new()
    {
        BlueprintGuid.Parse("4f918c1923ee2964aaa0f4c57bd32501"), // ShamanHexEvilEyeACBuff
        BlueprintGuid.Parse("ef8d0845d542b644d9e99c9130a5b110"), // ShamanHexEvilEyeAttackBuff
        BlueprintGuid.Parse("5fc895512a6a35d4ca0827bf09eff01a"), // ShamanHexEvilEyeSavesBuff
        BlueprintGuid.Parse("3dd413240a9cf614bbd48dacb172d048"), // WitchHexEvilEyeACBuff
        BlueprintGuid.Parse("58ce9c1ad7596c64a8a0e1fd4395c2c7"), // WitchHexEvilEyeAttackBuff
        BlueprintGuid.Parse("71e20ae6290617b428d03a8047874ea7")  // WitchHexEvilEyeSavesBuff
    };

    private static void AddTooltipBuffs(
        List<ITooltipBrick> bricks,
        UnitInspectInfoByPart.ActiveBuffsPartData? activeBuffs,
        UnitDescriptor? unit)
    {
        if (unit == null)
        {
            return;
        }

        var flatFooted = UIUtilityUnit.IsFlatFootedInTBM(unit)
            ? BlueprintRoot.Instance.UIRoot.FlatFootedFakeFeature
            : null;
        var buffs = activeBuffs?.ActiveBuffs ?? new List<Buff>();
        var retryLabels = AccursedHexIntegration.LabelsFor(unit.Unit);
        if (buffs.Count == 0 && flatFooted == null && retryLabels.Count == 0)
        {
            return;
        }

        bricks.Add(new TooltipBrickSeparator());
        bricks.Add(new TooltipBrickTitle(
            Kingmaker.Blueprints.Root.Strings.UIStrings.Instance.CharacterSheet.BuffsAndConditions,
            TooltipTitleType.H3,
            saberFormat: true));

        var displayedBuffs = AddAndClearBuffs(buffs);
        var sameNameCounts = displayedBuffs.Keys
            .GroupBy(buff => buff.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        var text = new StringBuilder();
        if (flatFooted != null)
        {
            text.Append(flatFooted.Name).Append(' ');
        }

        foreach (var item in displayedBuffs)
        {
            var displayName = GetDisplayName(item.Key, buffs, sameNameCounts);
            if (item.Value > 1)
            {
                text.Append(displayName)
                    .Append(" X")
                    .Append(item.Value)
                    .Append(' ');
            }
            else
            {
                UIUtilityTexts.AddWordWithSeparating(text, displayName, ".");
            }
        }

        foreach (var retryLabel in retryLabels)
        {
            UIUtilityTexts.AddWordWithSeparating(text, retryLabel, ".");
        }

        bricks.Add(new TooltipBrickText(text.ToString()));
    }

    private static Dictionary<Buff, int> AddAndClearBuffs(IEnumerable<Buff> activeBuffs)
    {
        var result = new Dictionary<Buff, int>();
        foreach (var buff in activeBuffs.Where(buff => !buff.HiddenInInspector))
        {
            Buff? existing = null;
            if (buff.Blueprint.Stacking == StackingType.Stack && buff.IsPermanent)
            {
                existing = result.Keys.FirstOrDefault(candidate => candidate.Blueprint == buff.Blueprint);
            }

            if (existing != null)
            {
                result[existing]++;
            }
            else
            {
                result[buff] = 1;
            }
        }

        return result;
    }

    private static string GetDisplayName(
        Buff buff,
        IReadOnlyCollection<Buff> allBuffs,
        IReadOnlyDictionary<string, int> sameNameCounts)
    {
        var name = GetDisplayNameWithDuration(buff, buff.Name);
        var nameCount = sameNameCounts.TryGetValue(name, out var count) ? count : 1;
        if (Main.Settings.ShowStackableDebuffCasters)
        {
            var casters = GetHostileCasterNames(buff, allBuffs);
            if (CasterLabelRules.ShouldAnnotate(
                    EvilEyeBuffs.Contains(buff.Blueprint.AssetGuid),
                    nameCount,
                    casters.Count > 0))
            {
                name = $"{name} ({string.Join(", ", casters)})";
            }
        }

        return SuspensionLabelRules.Apply(name, FreezeManager.GetSuspensionKind(buff));
    }

    private static string GetDisplayNameWithDuration(Buff buff, string displayName)
    {
        var compactTime = buff.TimeLeft <= TimeSpan.Zero
            ? "0s"
            : BlueprintRoot.Instance.Calendar.GetCompactPeriodString(buff.TimeLeft);
        return HoverDurationRules.Append(displayName, buff.IsPermanent, buff.TimeLeft, compactTime);
    }

    private static List<string> GetHostileCasterNames(Buff displayedBuff, IReadOnlyCollection<Buff> allBuffs)
    {
        var owner = displayedBuff.Owner?.Unit;
        if (owner == null)
        {
            return new List<string>();
        }

        IEnumerable<Buff> representedBuffs = displayedBuff.Blueprint.Stacking == StackingType.Stack
            && displayedBuff.IsPermanent
            ? allBuffs.Where(candidate => candidate.Blueprint == displayedBuff.Blueprint)
            : new[] { displayedBuff };

        return representedBuffs
            .Select(candidate => candidate.Context?.MaybeCaster)
            .Where(caster => caster != null && caster.IsEnemy(owner))
            .Select(caster => caster!.CharacterName)
            .Where(casterName => !string.IsNullOrWhiteSpace(casterName))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    [HarmonyPatch(typeof(TooltipTemplateUnitInspect), "AddActiveBuffs")]
    private static class AddActiveBuffsPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(
            List<ITooltipBrick> bricks,
            UnitInspectInfoByPart.ActiveBuffsPartData activeBuffs,
            UnitDescriptor unit,
            TooltipTemplateType type)
        {
            if (type != TooltipTemplateType.Tooltip)
            {
                return true;
            }

            try
            {
                AddTooltipBuffs(bricks, activeBuffs, unit);
                return false;
            }
            catch (Exception exception)
            {
                Main.ModEntry.Logger.Error($"Could not add QOL details to the hover inspector: {exception}");
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(TooltipBrickBuffVM), "GetDuration")]
    private static class RefreshBuffRowSuspensionLabelPatch
    {
        [HarmonyPostfix]
        private static void Postfix(TooltipBrickBuffVM __instance)
        {
            __instance.Name = SuspensionLabelRules.Apply(
                __instance.Name,
                FreezeManager.GetSuspensionKind(__instance.m_Buff));
        }
    }
}
