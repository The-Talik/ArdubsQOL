using System;
using System.Linq;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.UnitLogic.ActivatableAbilities;
using Kingmaker.UnitLogic.Buffs;

namespace ArdubsQOL;

[HarmonyPatch]
internal static class ShifterAspectTime
{
    private static bool _errorReported;

    [HarmonyPatch(typeof(ActivatableAbility), "OnDidTurnOff")]
    [HarmonyPrefix]
    private static void PreservePaidInterval(
        ActivatableAbility __instance,
        ref Buff? ___m_AppliedBuff)
    {
        try
        {
            var buff = ___m_AppliedBuff;
            if (buff == null || !__instance.IsStarted)
            {
                return;
            }

            var resourceLogic = __instance.Blueprint.GetComponent<ActivatableAbilityResourceLogic>();
            var now = Game.Instance.TimeController.GameTime;
            var paidIntervalEnd = ShifterAspectRules.GetPaidIntervalEnd(
                Main.Settings.PreserveShifterAspectTime,
                __instance.Blueprint.Group == ActivatableAbilityGroup.ShifterAspect,
                resourceLogic?.SpendType == ActivatableAbilityResourceLogic.ResourceSpendType.OncePerMinute,
                buff.HasNextResourceSpendingTime,
                now,
                buff.NextResourceSpendingTime);
            if (!paidIntervalEnd.HasValue)
            {
                return;
            }

            buff.EndTime = paidIntervalEnd.Value;
            buff.NextResourceSpendingTime = TimeSpan.MaxValue;
            buff.Owner.Unit.Buffs.UpdateNextEvent();

            // The activatable can now stop without removing the prepaid base-game buff.
            ___m_AppliedBuff = null;
            _errorReported = false;
        }
        catch (Exception exception)
        {
            ReportError("Could not preserve the remaining Shifter Aspect interval", exception);
        }
    }

    [HarmonyPatch(typeof(ActivatableAbility), "OnDidTurnOn")]
    [HarmonyPostfix]
    private static void RemoveLingeringAspectBeforeSwitch(ActivatableAbility __instance)
    {
        if (!Main.Settings.PreserveShifterAspectTime
            || !IsResourceBackedShifterAspect(__instance.Blueprint))
        {
            return;
        }

        try
        {
            var owner = __instance.Owner.Unit;
            foreach (var buff in owner.Buffs.RawFacts.ToArray())
            {
                if (buff.IsPermanent
                    || ActivatableAbility.GetBuffOwningAbility(owner, buff) != null
                    || buff.Context?.Root?.AssociatedBlueprint is not BlueprintActivatableAbility source
                    || !IsResourceBackedShifterAspect(source))
                {
                    continue;
                }

                buff.Remove();
            }

            _errorReported = false;
        }
        catch (Exception exception)
        {
            ReportError("Could not clear a lingering Shifter Aspect before switching aspects", exception);
        }
    }

    private static bool IsResourceBackedShifterAspect(BlueprintActivatableAbility blueprint)
    {
        return blueprint.Group == ActivatableAbilityGroup.ShifterAspect
            && blueprint.GetComponent<ActivatableAbilityResourceLogic>()?.SpendType
                == ActivatableAbilityResourceLogic.ResourceSpendType.OncePerMinute;
    }

    private static void ReportError(string message, Exception exception)
    {
        if (_errorReported)
        {
            return;
        }

        _errorReported = true;
        Main.ModEntry.Logger.Error($"{message}: {exception}");
    }
}
