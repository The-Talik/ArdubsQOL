using System.Linq;
using HarmonyLib;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.TurnBasedMode.Controllers;
using Kingmaker.UI.UnitSettings;
using Kingmaker.UnitLogic.ActivatableAbilities;
using TurnBased.Controllers;

namespace ArdubsQOL;

[HarmonyPatch]
internal static class ActivatablePatches
{
    [HarmonyPatch(typeof(ActivatableAbility), "CanTurnOn")]
    [HarmonyPostfix]
    private static void CanTurnOn(ActivatableAbility __instance, ref bool __result)
    {
        if (Blueprints.IsAura(__instance))
        {
            __result = __result
                && Main.Settings.EnableChantAura
                && ActionEconomy.CanActivate(__instance.Owner.Unit);
        }
    }

    [HarmonyPatch(typeof(MechanicActionBarSlot), nameof(MechanicActionBarSlot.IsPossibleActive))]
    [HarmonyPostfix]
    private static void ShowActivationAvailability(
        MechanicActionBarSlot __instance,
        ref bool __result)
    {
        if (__instance is MechanicActionBarSlotActivableAbility slot
            && slot.ActivatableAbility != null
            && Blueprints.IsAura(slot.ActivatableAbility)
            && !slot.ActivatableAbility.IsOn)
        {
            __result = __result
                && Main.Settings.EnableChantAura
                && ActionEconomy.CanActivate(slot.ActivatableAbility.Owner.Unit);
        }
    }

    [HarmonyPatch(typeof(ActivatableAbility), "OnDidTurnOn")]
    [HarmonyPostfix]
    private static void OnDidTurnOn(ActivatableAbility __instance)
    {
        if (!Blueprints.IsAura(__instance))
        {
            return;
        }

        if (!Main.Settings.EnableChantAura)
        {
            __instance.IsOn = false;
            return;
        }

        if (!__instance.IsOn || !__instance.IsStarted)
        {
            Main.ModEntry.Logger.Log(
                $"{__instance.Blueprint.name} did not start: IsOn={__instance.IsOn}, " +
                $"IsStarted={__instance.IsStarted}; {ActionEconomy.DescribeAvailability(__instance.Owner.Unit)}.");
            return;
        }

        if (!ActionEconomy.ReserveOnActivation(__instance))
        {
            Main.ModEntry.Logger.Log(
                $"{__instance.Blueprint.name} was switched off because no move action was available; " +
                $"{ActionEconomy.DescribeAvailability(__instance.Owner.Unit)}.");
            __instance.IsOn = false;
            return;
        }

        Main.ModEntry.Logger.Log(
            $"{__instance.Blueprint.name} active: IsOn={__instance.IsOn}, " +
            $"IsStarted={__instance.IsStarted}, HasBuff={__instance.AppliedBuff != null}.");
        SilentRangeIndicator.Show(__instance);
        AuraActivationPresentation.Play(__instance);
        FreezeManager.UpdateNow();
    }

    [HarmonyPatch(typeof(ActivatableAbility), "OnDidTurnOff")]
    [HarmonyPostfix]
    private static void OnDidTurnOff(ActivatableAbility __instance)
    {
        if (!Blueprints.IsAura(__instance))
        {
            return;
        }

        ActionEconomy.Release(__instance);
        SilentRangeIndicator.Hide(__instance);
        Main.ModEntry.Logger.Log(
            $"{__instance.Blueprint.name} deactivated: IsOn={__instance.IsOn}, IsStarted={__instance.IsStarted}.");
        FreezeManager.UpdateNow();
    }

    [HarmonyPatch(typeof(ActivatableAbility), nameof(ActivatableAbility.OnNewRound))]
    [HarmonyPostfix]
    private static void OnNewRound(ActivatableAbility __instance)
    {
        if (!Blueprints.IsAura(__instance) || !__instance.IsOn || !__instance.IsStarted)
        {
            return;
        }

        if (!Main.Settings.EnableChantAura)
        {
            __instance.IsOn = false;
            return;
        }

        if (!ActionEconomy.ReserveOnNewRound(__instance))
        {
            __instance.IsOn = false;
        }
    }

    [HarmonyPatch(typeof(ActivatableAbility), nameof(ActivatableAbility.HandleUnitLeaveCombat))]
    [HarmonyPostfix]
    private static void TurnOffWhenCombatEnds(ActivatableAbility __instance, UnitEntityData unit)
    {
        if (!Blueprints.IsAura(__instance) || unit != __instance.Owner.Unit)
        {
            return;
        }

        if (__instance.IsOn)
        {
            __instance.IsOn = false;
        }
        else if (__instance.IsStarted)
        {
            __instance.Stop(forceRemovedBuff: true);
        }
    }

    [HarmonyPatch(typeof(TurnController), "CalculatePredictionForActivatableAbilities")]
    [HarmonyPostfix]
    private static void ShowMoveActionReservation(TurnController __instance)
    {
        var unit = __instance.SelectedUnit;
        if (unit == null || !unit.UsedOneMoveAction())
        {
            return;
        }

        var aura = unit.ActivatableAbilities.RawFacts.FirstOrDefault(
            ability => Blueprints.IsAura(ability) && ability.IsOn && ability.IsStarted);
        if (aura == null)
        {
            return;
        }

        var move = __instance.GetActionsStates(unit).Move;
        move.SetPrediction(
            CombatAction.UsageType.UseAbility,
            CombatAction.ActivityType.Ability,
            CombatAction.ActivityState.Used,
            aura);
    }
}
