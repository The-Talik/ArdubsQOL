using System;
using HarmonyLib;
using Kingmaker.EntitySystem.Entities;
using TurnBased.Controllers;

namespace ArdubsQOL;

[HarmonyPatch]
internal static class AuraTurnDelay
{
    [ThreadStatic] private static TurnController? _checkingDelay;

    [HarmonyPatch(typeof(TurnController), nameof(TurnController.CanDelay))]
    [HarmonyPrefix]
    private static void BeginDelayCheck(TurnController __instance, out TurnController? __state)
    {
        __state = _checkingDelay;
        _checkingDelay = __instance;
    }

    [HarmonyPatch(typeof(TurnController), nameof(TurnController.CanDelay))]
    [HarmonyFinalizer]
    private static void EndDelayCheck(TurnController? __state)
    {
        _checkingDelay = __state;
    }

    [HarmonyPatch(typeof(TurnController), nameof(TurnController.IsActed))]
    [HarmonyPostfix]
    private static void IgnoreAuraReservationForDelay(TurnController __instance, UnitEntityData unit, ref bool __result)
    {
        // Leave native action bookkeeping intact everywhere except CanDelay's read-only check.
        if (__result && ReferenceEquals(_checkingDelay, __instance)
            && ActionEconomy.CanDelayWithReservation(__instance, unit))
        {
            __result = false;
        }
    }

    [HarmonyPatch(typeof(TurnController), nameof(TurnController.DelayInitiaive))]
    [HarmonyPostfix]
    private static void ResetDelayedReservations(TurnController __instance)
    {
        // Native delay resets the rider's move cooldown. Re-reserve upkeep after
        // the delayed turn is prepared, rather than reusing its previous payment.
        ActionEconomy.ForgetDelayedReservations(__instance.Rider);
        ActionEconomy.ForgetDelayedReservations(__instance.Mount);
    }
}
