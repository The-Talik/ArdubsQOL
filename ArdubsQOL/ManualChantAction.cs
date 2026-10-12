using System.Linq;
using HarmonyLib;
using Kingmaker;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Commands.Base;
using TurnBased.Controllers;

namespace ArdubsQOL;

internal static class ManualChantAction
{
    private static bool ShouldUseStandardAction(AbilityData ability)
    {
        var caster = ability.Caster?.Unit;
        if (caster == null)
        {
            return false;
        }

        var abilityGuid = ability.Blueprint.AssetGuid;
        var matchingAuraActive = caster.ActivatableAbilities.RawFacts.Any(
            aura => aura.IsOn
                && aura.IsStarted
                && ((abilityGuid == Blueprints.ChantAbilityGuid && Blueprints.IsChantAura(aura))
                    || (abilityGuid == Blueprints.CackleAbilityGuid && Blueprints.IsCackleAura(aura))));

        return ActionRules.ShouldUseStandardActionForManualChant(
            Main.Settings.EnableChantAura,
            CombatController.IsInTurnBasedCombat(),
            matchingAuraActive);
    }

    [HarmonyPatch(typeof(AbilityData), nameof(AbilityData.ActionType), MethodType.Getter)]
    private static class AbilityDataActionTypePatch
    {
        [HarmonyPostfix]
        private static void Postfix(AbilityData __instance, ref UnitCommand.CommandType __result)
        {
            if (ShouldUseStandardAction(__instance))
            {
                __result = UnitCommand.CommandType.Standard;
            }
        }
    }

    [HarmonyPatch(typeof(AbilityData), nameof(AbilityData.RuntimeActionType), MethodType.Getter)]
    private static class AbilityDataRuntimeActionTypePatch
    {
        [HarmonyPostfix]
        private static void Postfix(AbilityData __instance, ref UnitCommand.CommandType __result)
        {
            if (ShouldUseStandardAction(__instance))
            {
                __result = UnitCommand.CommandType.Standard;
            }
        }
    }
}
