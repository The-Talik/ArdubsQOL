using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic.Buffs;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Mechanics.Actions;

namespace ArdubsQOL;

internal static class EvilEyeStacking
{
    private static readonly Dictionary<BlueprintGuid, StatType> BuffStats = new()
    {
        [BlueprintGuid.Parse("3dd413240a9cf614bbd48dacb172d048")] = StatType.AC,
        [BlueprintGuid.Parse("4f918c1923ee2964aaa0f4c57bd32501")] = StatType.AC,
        [BlueprintGuid.Parse("58ce9c1ad7596c64a8a0e1fd4395c2c7")] = StatType.AdditionalAttackBonus,
        [BlueprintGuid.Parse("ef8d0845d542b644d9e99c9130a5b110")] = StatType.AdditionalAttackBonus,
        // SaveWill here identifies the three-saving-throw variant.
        [BlueprintGuid.Parse("71e20ae6290617b428d03a8047874ea7")] = StatType.SaveWill,
        [BlueprintGuid.Parse("5fc895512a6a35d4ca0827bf09eff01a")] = StatType.SaveWill
    };
    private static readonly StatType[] AffectedStats =
    {
        StatType.AC, StatType.AdditionalAttackBonus,
        StatType.SaveFortitude, StatType.SaveReflex, StatType.SaveWill
    };
    private static bool? _lastSetting;
    private static bool _refreshErrorReported;

    internal static bool IsEvilEye(BlueprintGuid guid) => BuffStats.ContainsKey(guid);

    internal static bool IsPenalty(ModifiableValue.Modifier modifier, StatType stat)
    {
        if (modifier.ModValue >= 0 || modifier.Source is not Buff buff || buff.Blueprint == null
            || !BuffStats.TryGetValue(buff.Blueprint.AssetGuid, out var affected))
        {
            return false;
        }
        return affected == stat || (affected == StatType.SaveWill
            && (stat == StatType.SaveFortitude || stat == StatType.SaveReflex));
    }

    internal static Func<ModifiableValue.Modifier, bool>? RestrictModifiers(
        IEnumerable<ModifiableValue.Modifier> modifiers, StatType stat,
        Func<ModifiableValue.Modifier, bool>? originalFilter)
    {
        ModifiableValue.Modifier? strongest = null;
        var count = 0;
        foreach (var modifier in modifiers)
        {
            if (!IsPenalty(modifier, stat) || originalFilter?.Invoke(modifier) == false) continue;
            count++;
            if (strongest == null || modifier.ModValue < strongest.ModValue) strongest = modifier;
        }
        if (count < 2) return originalFilter;

        // Do not remove, zero, retype or suspend modifiers/buffs. Native expiry,
        // dispelling and each caster's Chant/Cackle continue to operate normally.
        // Pick among modifiers admitted by the caller's filter (touch AC, etc.).
        return modifier => (ReferenceEquals(modifier, strongest) || !IsPenalty(modifier, stat))
            && (originalFilter?.Invoke(modifier) ?? true);
    }

    internal static Buff? FindExistingBuff(BuffCollection collection, BlueprintBuff blueprint, Buff incoming)
    {
        if (Main.Settings?.LimitEvilEyeStacking != true || !IsEvilEye(blueprint.AssetGuid)
            || incoming.Context?.MaybeCaster == null)
        {
            return collection.GetBuff(blueprint);
        }

        // Replace only this caster's previous application. Another caster's
        // copy must remain available if the strongest penalty expires first.
        return FindCasterBuff(collection, blueprint, incoming.Context.MaybeCaster);
    }

    internal static Buff? FindCasterBuff(BuffCollection collection, BlueprintBuff blueprint, UnitEntityData caster) =>
        collection.RawFacts.FirstOrDefault(buff => buff.Blueprint == blueprint
            && buff.Context?.MaybeCaster == caster);

    internal static Buff? FindDurationBuff(
        BuffCollection collection, BlueprintBuff blueprint, ContextActionReduceBuffDuration action)
    {
        if (Main.Settings?.LimitEvilEyeStacking == true && blueprint != null && IsEvilEye(blueprint.AssetGuid)
            && action.Increase && action.ToTarget)
        {
            var context = action.Context;
            var ability = context?.SourceAbility?.AssetGuid;
            if ((ability == Blueprints.ChantAbilityGuid || ability == Blueprints.CackleAbilityGuid)
                && context?.MaybeCaster is { } caster)
            {
                return FindCasterBuff(collection, blueprint, caster);
            }
        }
        return collection.GetBuff(blueprint);
    }

    internal static void Update()
    {
        var enabled = Main.Settings.LimitEvilEyeStacking;
        if (_lastSetting == enabled) return;
        var game = Game.Instance;
        if (game?.State?.Units == null || game.IsLoadingSave) return;
        try
        {
            foreach (var unit in game.State.Units.ToArray())
            {
                if (unit?.Stats == null) continue;
                foreach (var type in AffectedStats)
                {
                    var stat = unit.Stats.GetStat(type);
                    if (stat != null && stat.Modifiers.Any(modifier => IsPenalty(modifier, type)))
                        stat.UpdateValue();
                }
            }
            _lastSetting = enabled;
            _refreshErrorReported = false;
        }
        catch (Exception exception)
        {
            if (_refreshErrorReported) return;
            _refreshErrorReported = true;
            Main.ModEntry.Logger.Error($"Could not refresh Evil Eye penalties after changing the stacking option: {exception}");
        }
    }

    [HarmonyPatch(typeof(ModifiableValue), "ApplyModifiersFiltered")]
    private static class StatCalculationPatch
    {
        [HarmonyPrefix]
        private static void Prefix(ModifiableValue __instance, ref Func<ModifiableValue.Modifier, bool>? filter)
        {
            if (Main.Settings?.LimitEvilEyeStacking == true && AffectedStats.Contains(__instance.Type))
                filter = RestrictModifiers(__instance.Modifiers, __instance.Type, filter);
        }
    }

    [HarmonyPatch(typeof(ModifiableValue), nameof(ModifiableValue.GetDisplayModifiers))]
    private static class StatBreakdownPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ModifiableValue __instance, ref IEnumerable<ModifiableValue.Modifier> __result)
        {
            if (Main.Settings?.LimitEvilEyeStacking != true || !AffectedStats.Contains(__instance.Type)) return;
            var displayed = __result.ToList();
            var filter = RestrictModifiers(displayed, __instance.Type, null);
            __result = filter == null ? displayed : displayed.Where(filter);
        }
    }

    [HarmonyPatch(typeof(BuffCollection), "PrepareFactForAttach")]
    private static class IndependentCasterPatch
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.Select(instruction => new CodeInstruction(instruction)).ToList();
            var getBuff = AccessTools.Method(typeof(BuffCollection), nameof(BuffCollection.GetBuff));
            var calls = code.Select((instruction, index) => (instruction, index))
                .Where(pair => pair.instruction.Calls(getBuff)).ToArray();
            if (calls.Length != 1)
            {
                Main.ModEntry?.Logger.Error("Evil Eye stacking: unrecognized buff replacement check; caster separation left unchanged.");
                return code;
            }
            var call = calls[0].instruction;
            var loadIncoming = new CodeInstruction(OpCodes.Ldarg_1);
            loadIncoming.labels.AddRange(call.labels);
            loadIncoming.blocks.AddRange(call.blocks);
            call.labels.Clear();
            call.blocks.Clear();
            call.opcode = OpCodes.Call;
            call.operand = AccessTools.Method(typeof(EvilEyeStacking), nameof(FindExistingBuff));
            code.Insert(calls[0].index, loadIncoming);
            return code;
        }
    }

    [HarmonyPatch(typeof(ContextActionReduceBuffDuration), nameof(ContextActionReduceBuffDuration.RunAction))]
    private static class ManualUpkeepPatch
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.Select(instruction => new CodeInstruction(instruction)).ToList();
            var getBuff = AccessTools.Method(typeof(BuffCollection), nameof(BuffCollection.GetBuff));
            var calls = code.Select((instruction, index) => (instruction, index))
                .Where(pair => pair.instruction.Calls(getBuff)).ToArray();
            if (calls.Length != 2)
            {
                Main.ModEntry?.Logger.Error("Evil Eye stacking: unrecognized duration-action lookup; manual upkeep left unchanged.");
                return code;
            }
            foreach (var (call, index) in calls.Reverse())
            {
                var loadAction = new CodeInstruction(OpCodes.Ldarg_0);
                loadAction.labels.AddRange(call.labels);
                loadAction.blocks.AddRange(call.blocks);
                call.labels.Clear();
                call.blocks.Clear();
                call.opcode = OpCodes.Call;
                call.operand = AccessTools.Method(typeof(EvilEyeStacking), nameof(FindDurationBuff));
                code.Insert(index, loadAction);
            }
            return code;
        }
    }
}
