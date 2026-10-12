using HarmonyLib;
using Kingmaker;
using Kingmaker.Controllers.Rest;
using Kingmaker.RuleSystem.Rules.Abilities;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.ActivatableAbilities;

namespace ArdubsQOL;

[HarmonyPatch(typeof(AbilityData), nameof(AbilityData.HasMetamagic))]
internal static class RodCaseMetamagicAvailabilityPatch
{
    private static void Postfix(AbilityData __instance, Metamagic metamagic, ref bool __result)
    {
        if (!__result) __result = (RodCase.GetMetamagic(__instance) & metamagic) == metamagic;
    }
}

[HarmonyPatch(typeof(RuleCalculateAbilityParams), nameof(RuleCalculateAbilityParams.OnTrigger))]
internal static class RodCaseSpellParamsPatch
{
    private static void Prefix(RuleCalculateAbilityParams __instance)
    {
        if (__instance.AbilityData == null) return;
        var mask = RodCase.GetMetamagic(__instance.AbilityData);
        if (mask != 0) __instance.AddMetamagic(mask);
    }
}

[HarmonyPatch(typeof(RuleCastSpell), nameof(RuleCastSpell.OnTrigger))]
internal static class RodCaseSpendChargePatch
{
    private static void Prefix(RuleCastSpell __instance, out RodPool? __state) =>
        __state = RodCase.CaptureCastPool(__instance);

    private static void Postfix(RuleCastSpell __instance, RodPool? __state) =>
        RodCase.SpendCharge(__instance, __state);
}

[HarmonyPatch(typeof(ActivatableAbility), nameof(ActivatableAbility.IsOn), MethodType.Setter)]
internal static class RodCaseEquippedRodPatch
{
    private static void Prefix(ActivatableAbility __instance, bool value)
    {
        if (value && __instance.Blueprint.Group == ActivatableAbilityGroup.MetamagicRod)
            RodCase.ClearSelection(__instance.Owner.Unit);
    }
}

[HarmonyPatch(typeof(Player), "OnPostLoad")]
internal static class RodCaseLoadPatch
{
    private static void Prefix() => RodCase.Reset();
}

[HarmonyPatch(typeof(RestController), "ApplyRestInterval")]
internal static class RodCaseRestPatch
{
    private static void Prefix() => RodCase.Reset();
}
