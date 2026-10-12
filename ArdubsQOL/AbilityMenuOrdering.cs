using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Kingmaker.Blueprints;
using Kingmaker.UI.UnitSettings;

namespace ArdubsQOL;

[HarmonyPatch]
internal static class AbilityMenuOrdering
{
    private const string ActionBarViewModel = "Kingmaker.UI.MVVM._VM.ActionBar.ActionBarVM";
    private const string ActionBarSelectorViewModel = "Kingmaker.UI.MVVM._VM.ActionBar.ActionBarSelectorVM";

    private static IEnumerable<MethodBase> TargetMethods()
    {
        var actionBar = AccessTools.TypeByName(ActionBarViewModel);
        var selector = AccessTools.TypeByName(ActionBarSelectorViewModel);
        var actionBarCollector = actionBar == null ? null : AccessTools.Method(actionBar, "CollectAbilities");
        var selectorCollector = selector == null ? null : AccessTools.Method(selector, "CollectAbilities");

        if (actionBarCollector != null)
        {
            yield return actionBarCollector;
        }

        if (selectorCollector != null)
        {
            yield return selectorCollector;
        }
    }

    [HarmonyPostfix]
    private static void PutAurasBesideTheirAbilities(object __instance)
    {
        var field = AccessTools.Field(__instance.GetType(), "GroupAbilities")
            ?? AccessTools.Field(__instance.GetType(), "m_Items");
        if (field?.GetValue(__instance) is not IList items)
        {
            return;
        }

        MoveAuraAfterSource(items, Blueprints.ChantAbilityGuid, Blueprints.ChantAuraAbilityGuid);
        MoveAuraAfterSource(items, Blueprints.CackleAbilityGuid, Blueprints.CackleAuraAbilityGuid);
    }

    private static void MoveAuraAfterSource(
        IList items,
        BlueprintGuid sourceAbilityGuid,
        BlueprintGuid auraAbilityGuid)
    {
        var sourceIndex = -1;
        var auraIndex = -1;

        for (var index = 0; index < items.Count; index++)
        {
            var slot = GetSlot(items[index]);
            if (slot is MechanicActionBarSlotAbility ability
                && ability.Ability?.Blueprint.AssetGuid == sourceAbilityGuid)
            {
                sourceIndex = index;
            }
            else if (slot is MechanicActionBarSlotActivableAbility activatable
                && activatable.ActivatableAbility?.Blueprint.AssetGuid == auraAbilityGuid)
            {
                auraIndex = index;
            }
        }

        if (sourceIndex < 0 || auraIndex < 0 || auraIndex == sourceIndex + 1)
        {
            return;
        }

        var aura = items[auraIndex];
        items.RemoveAt(auraIndex);
        if (auraIndex < sourceIndex)
        {
            sourceIndex--;
        }

        items.Insert(sourceIndex + 1, aura);
    }

    private static MechanicActionBarSlot? GetSlot(object item)
    {
        return AccessTools.Property(item.GetType(), "MechanicActionBarSlot")?.GetValue(item)
            as MechanicActionBarSlot;
    }
}
