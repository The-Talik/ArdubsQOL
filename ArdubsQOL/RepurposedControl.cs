using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Armies.TacticalCombat;
using Kingmaker.Blueprints;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UI.ActionBar;
using Kingmaker.UI.MVVM._VM.ActionBar;
using Kingmaker.UnitLogic.FactLogic;

namespace ArdubsQOL;

internal static class RepurposedControl
{
    // The raised creature's buff, not the spell's damage-over-time mark on a living enemy.
    internal static readonly BlueprintGuid RaisedBuffGuid = BlueprintGuid.Parse("5e18ce2e21330e34690c372fbd9d6d60");

    internal static bool CanControl(UnitEntityData? unit)
    {
        if (Main.Settings?.ControlRepurposedMonsters != true || unit == null || !unit.IsInGame
            || !unit.IsPlayerFaction || unit.IsDetached)
        {
            return false;
        }

        var game = Game.Instance;
        if (game?.Player == null || TacticalCombatHelper.IsActive
            || game.LoadedAreaState?.Settings.CapitalPartyMode.Value == true
            || !unit.Descriptor.Faction.IsDirectlyControllable
            || unit.Descriptor.State.IsFinallyDead || unit.Descriptor.State.IsPanicked
            || unit.CutsceneControlledUnit?.GetCurrentlyActive() != null)
        {
            return false;
        }

        var raisedByParty = false;
        var repurposeBlocks = 0;
        foreach (var buff in unit.Buffs)
        {
            if (buff.Blueprint.AssetGuid != RaisedBuffGuid || !buff.IsTurnedOn
                || buff.IsDisposed || buff.IsSuppressed)
            {
                continue;
            }

            var caster = buff.Context?.MaybeCaster;
            if (caster == null || !caster.IsPlayerFaction || !game.Player.PartyAndPets.Contains(caster))
            {
                continue;
            }

            raisedByParty = true;
            foreach (var component in buff.Components)
            {
                if (component.IsTurnedOn && !component.IsDisposed
                    && component.SourceBlueprintComponent is ChangeFaction factionChange
                    && !factionChange.m_AllowDirectControl)
                {
                    repurposeBlocks++;
                }
            }
        }

        // Read the counter without changing it. Releasing the flag would save an
        // altered counter and break ChangeFaction's matching release on buff removal.
        // Any extra block (confusion, resurrection animation, cutscene, etc.) still wins.
        return AllowsControl(raisedByParty, unit.PreventDirectControl.Count, repurposeBlocks);
    }

    internal static bool AllowsControl(bool raisedByParty, int totalBlocks, int repurposeBlocks) =>
        raisedByParty && totalBlocks >= 0 && repurposeBlocks >= 0 && totalBlocks <= repurposeBlocks;

    [HarmonyPatch(typeof(UnitEntityData), nameof(UnitEntityData.IsDirectlyControllable), MethodType.Getter)]
    private static class DirectControlPatch
    {
        [HarmonyPostfix]
        private static void Postfix(UnitEntityData __instance, ref bool __result)
        {
            if (!__result && CanControl(__instance)) __result = true;
        }
    }

    [HarmonyPatch]
    private static class ActionBarPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(ActionBarVM), "CheckPredictionPanel");
            yield return AccessTools.Method(typeof(ActionBarManager), "CheckTurnPanelView");
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            var code = instructions.Select(instruction => new CodeInstruction(instruction)).ToList();
            var contains = AccessTools.Method(typeof(List<UnitEntityData>), nameof(List<UnitEntityData>.Contains));
            var calls = code.Where(instruction => instruction.Calls(contains)).ToArray();
            if (calls.Length != 1)
            {
                Main.ModEntry?.Logger.Error($"Repurposed control: unrecognized action-bar check in {__originalMethod}; left unchanged.");
                return code;
            }

            calls[0].opcode = OpCodes.Call;
            calls[0].operand = AccessTools.Method(typeof(RepurposedControl), nameof(IsActionBarUnit));
            return code;
        }
    }

    internal static bool IsActionBarUnit(List<UnitEntityData> group, UnitEntityData unit) =>
        group.Contains(unit) || CanControl(unit);
}
