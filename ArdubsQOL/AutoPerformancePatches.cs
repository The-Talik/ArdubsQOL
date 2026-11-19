using HarmonyLib;
using Kingmaker;
using Kingmaker.Controllers.Rest;
using Kingmaker.UI.UnitSettings;
using Kingmaker.UnitLogic.ActivatableAbilities;
using Kingmaker.UnitLogic.Commands;
using Kingmaker.UnitLogic.Commands.Base;

namespace ArdubsQOL;

[HarmonyPatch]
internal static class AutoPerformancePatches
{
    [HarmonyPatch(typeof(ActivatableAbility), "CanTurnOn")]
    [HarmonyPostfix]
    private static void CanTurnOn(ActivatableAbility __instance, ref bool __result)
    {
        if (AutoPerformanceBlueprints.IsAuto(__instance))
            __result = __result && AutoPerformance.Source(__instance) is { } source && AutoPerformance.CanStart(source);
    }

    [HarmonyPatch(typeof(ActivatableAbility), nameof(ActivatableAbility.IsAvailable), MethodType.Getter)]
    [HarmonyPostfix]
    private static void Available(ActivatableAbility __instance, ref bool __result)
    {
        if (AutoPerformanceBlueprints.IsAuto(__instance)) __result = __result && AutoPerformance.Available(__instance);
    }

    [HarmonyPatch(typeof(ActivatableAbility), "OnDidTurnOn")]
    [HarmonyPrefix]
    private static void BeforeOn(ActivatableAbility __instance) => AutoPerformance.ManualChange(__instance);

    [HarmonyPatch(typeof(ActivatableAbility), "OnDidTurnOn")]
    [HarmonyPostfix]
    private static void AfterOn(ActivatableAbility __instance)
    {
        if (AutoPerformanceBlueprints.IsAuto(__instance) && __instance.IsOn && __instance.IsStarted)
            AutoPerformance.TurnedOn(__instance);
    }

    [HarmonyPatch(typeof(ActivatableAbility), "OnDidTurnOff")]
    [HarmonyPrefix]
    private static void BeforeOff(ActivatableAbility __instance) => AutoPerformance.ManualChange(__instance);

    [HarmonyPatch(typeof(ActivatableAbility), "OnDidTurnOff")]
    [HarmonyPostfix]
    private static void AfterOff(ActivatableAbility __instance)
    {
        if (AutoPerformanceBlueprints.IsAuto(__instance)) AutoPerformance.TurnedOff(__instance);
    }

    [HarmonyPatch(typeof(ActivatableAbility), nameof(ActivatableAbility.OnNewRound))]
    [HarmonyPrefix]
    private static bool BeforeRound(ActivatableAbility __instance, out bool __state) =>
        AutoPerformance.BeforeRound(__instance, out __state);

    [HarmonyPatch(typeof(ActivatableAbility), nameof(ActivatableAbility.OnNewRound))]
    [HarmonyPostfix]
    private static void AfterRound(ActivatableAbility __instance, bool __state) =>
        AutoPerformance.AfterRound(__instance, __state);

    [HarmonyPatch(typeof(UnitActivateAbility), nameof(UnitActivateAbility.GetCommandType))]
    [HarmonyPostfix]
    private static void ActionType(ActivatableAbility ability, ref UnitCommand.CommandType __result)
    {
        if (AutoPerformanceBlueprints.IsAuto(ability) && AutoPerformance.Source(ability) is { } source)
            __result = source.IsOn && source.IsStarted ? UnitCommand.CommandType.Free : UnitActivateAbility.GetCommandType(source);
    }

    [HarmonyPatch(typeof(UnitCommands), "Run", typeof(UnitCommand), typeof(bool), typeof(bool))]
    [HarmonyPrefix]
    private static void PreserveCommandQueue(UnitCommand cmd, ref bool doNotClearQueue)
    {
        if (cmd is UnitActivateAbility activate && AutoPerformance.ChangingSource == activate.Ability)
            doNotClearQueue = true;
    }

    [HarmonyPatch(typeof(MechanicActionBarSlot), nameof(MechanicActionBarSlot.IsPossibleActive))]
    [HarmonyPostfix]
    private static void Possible(MechanicActionBarSlot __instance, ref bool __result)
    {
        if (__instance is MechanicActionBarSlotActivableAbility slot && AutoPerformanceBlueprints.IsAuto(slot.ActivatableAbility))
            __result = slot.ActivatableAbility.IsOn
                || (AutoPerformance.Source(slot.ActivatableAbility) is { } source && AutoPerformance.CanStart(source));
    }

    [HarmonyPatch(typeof(MechanicActionBarSlotActivableAbility), nameof(MechanicActionBarSlotActivableAbility.GetResource))]
    [HarmonyPostfix]
    private static void Resource(MechanicActionBarSlotActivableAbility __instance, ref int __result)
    {
        if (AutoPerformanceBlueprints.IsAuto(__instance.ActivatableAbility))
            __result = AutoPerformance.Source(__instance.ActivatableAbility)?.ResourceCount ?? -1;
    }

    [HarmonyPatch(typeof(MechanicActionBarSlotActivableAbility), nameof(MechanicActionBarSlotActivableAbility.IsDisabled))]
    [HarmonyPostfix]
    private static void Disabled(MechanicActionBarSlotActivableAbility __instance, ref bool __result)
    {
        if (AutoPerformanceBlueprints.IsAuto(__instance.ActivatableAbility) && __instance.ActivatableAbility.IsOn) __result = false;
    }

    [HarmonyPatch(typeof(MechanicActionBarSlot), nameof(MechanicActionBarSlot.GetCountText))]
    [HarmonyPostfix]
    private static void Count(MechanicActionBarSlot __instance, ref string __result)
    {
        if (__instance is MechanicActionBarSlotActivableAbility slot && AutoPerformanceBlueprints.IsAuto(slot.ActivatableAbility)
            && AutoPerformance.Countdown(slot.ActivatableAbility) is { } countdown) __result = countdown;
    }

    [HarmonyPatch(typeof(Player), "OnPostLoad")]
    [HarmonyPrefix]
    private static void BeforeLoad() => AutoPerformance.BeforeLoad();

    [HarmonyPatch(typeof(Player), "OnPostLoad")]
    [HarmonyPostfix]
    private static void AfterLoad() => AutoPerformance.AfterLoad();

    [HarmonyPatch(typeof(RestController), "ApplyRestInterval")]
    [HarmonyPrefix]
    private static void Rest() => AutoPerformance.StopAll();
}
