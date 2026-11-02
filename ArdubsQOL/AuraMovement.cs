using System;
using System.Linq;
using HarmonyLib;
using Kingmaker;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.View;
using TurnBased.Controllers;

namespace ArdubsQOL;

internal static class AuraMovement
{
    internal const int SpeedLimitFeet = 5;
    // The game's speed stat is distance covered by a three-second move action.
    internal const float SpeedLimitMetersPerSecond = SpeedLimitFeet * 0.3048f / 3f;

    internal static bool UsesTurnBasedActions(UnitEntityData unit) =>
        Game.Instance?.Player != null && unit.IsInCombat && CombatController.IsInTurnBasedCombat();

    internal static bool HasActiveAura(UnitEntityData? unit) =>
        Main.Settings?.EnableChantAura == true
        && unit?.ActivatableAbilities != null
        && unit.ActivatableAbilities.RawFacts.Any(
            aura => Blueprints.IsAura(aura) && aura.IsOn && aura.IsStarted);

    internal static bool IsLimited(UnitEntityData? unit)
    {
        if (unit == null || Main.Settings?.EnableChantAura != true)
        {
            return false;
        }

        // A mount must not let its chanting rider bypass the movement cost.
        if (HasActiveAura(unit) && !UsesTurnBasedActions(unit))
        {
            return true;
        }

        var rider = unit.Get<UnitPartSaddled>()?.Rider;
        return HasActiveAura(rider) && !UsesTurnBasedActions(rider!);
    }

    [HarmonyPatch(typeof(ModifiableValue), nameof(ModifiableValue.ModifiedValue), MethodType.Getter)]
    private static class DisplayedSpeedPatch
    {
        private static void Postfix(ModifiableValue __instance, ref int __result)
        {
            if (__instance is ModifiableValueSpeed && IsLimited(__instance.Owner?.Unit))
            {
                __result = Math.Min(__result, SpeedLimitFeet);
            }
        }
    }

    [HarmonyPatch(typeof(UnitEntityData), nameof(UnitEntityData.CurrentSpeedMps), MethodType.Getter)]
    private static class CurrentSpeedPatch
    {
        private static void Postfix(UnitEntityData __instance, ref float __result)
        {
            if (IsLimited(__instance))
            {
                __result = Math.Min(__result, SpeedLimitMetersPerSecond);
            }
        }
    }

    [HarmonyPatch(typeof(UnitMovementAgentBase), nameof(UnitMovementAgentBase.MaxSpeed), MethodType.Getter)]
    private static class MovementOverridePatch
    {
        private static void Postfix(UnitMovementAgentBase __instance, ref float __result)
        {
            if (IsLimited(__instance.Unit?.EntityData))
            {
                __result = Math.Min(__result, SpeedLimitMetersPerSecond);
            }
        }
    }
}
