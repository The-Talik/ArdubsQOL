using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Armies;
using Kingmaker.Armies.TacticalCombat;
using Kingmaker.Blueprints.Root.Strings;
using Kingmaker.UI.MVVM._PCView.Crusade.ArmyInfo;
using Kingmaker.UI.MVVM._VM.Crusade.ArmyInfo;
using Kingmaker.UI.MVVM._VM.Tooltip.Bricks;
using Kingmaker.UI.MVVM._VM.Tooltip.Templates;
using Owlcat.Runtime.UI.Tooltips;

namespace ArdubsQOL;

internal static class InfirmaryUnitBreakdown
{
    private static bool _errorReported;

    private sealed class ArmyTooltip : TooltipTemplateSimple
    {
        private readonly Func<ArmyLeader?> _leader;

        internal ArmyTooltip(string header, string description, Func<ArmyLeader?> leader)
            : base(header, description) { _leader = leader; }

        public override IEnumerable<ITooltipBrick> GetBody(TooltipTemplateType type) =>
            base.GetBody(type).Concat(BuildArmy(_leader()));
    }

    internal static TooltipTemplateSimple CreateArmyTooltip(string header, string description, ArmyLeaderStatsPCView view) =>
        new ArmyTooltip(header, description, () => (view.GetViewModel() as ArmyLeaderStatsVM)?.m_Leader);

    internal static List<ITooltipBrick> BuildArmy(ArmyLeader? leader)
    {
        var bricks = new List<ITooltipBrick>();
        if (Main.Settings?.ShowInfirmaryUnitBreakdown != true
            || leader?.Army?.Squads == null || leader.Faction != ArmyFaction.Crusaders)
            return bricks;
        try
        {
            var squads = leader.Army.Squads.Where(squad => squad?.Unit != null && squad.Count > 0)
                .Select(squad => new SquadResult(squad)).ToArray();
            if (squads.Length == 0) return bricks;
            bricks.Add(new TooltipBrickSeparator());
            bricks.Add(new TooltipBrickTitle($"{UIStrings.Instance.CrusadeTexts.InfirmaryTooltipHeader}: {leader.Stats.InfirmarySize.ModifiedValue:N0}", TooltipTitleType.H2));
            bricks.Add(new TooltipBrickText("Full-stack recovery cost (units × points per unit):"));
            foreach (var squad in squads)
                bricks.Add(new TooltipBrickText(InfirmaryBreakdownRules.FormatCost(
                    squad.Unit.CharacterName, squad.Count, squad.UnitExperience)));
            _errorReported = false;
        }
        catch (Exception exception)
        {
            bricks.Clear();
            ReportError(exception);
        }
        return bricks;
    }

    internal static List<ITooltipBrick> BuildBattle()
    {
        var bricks = new List<ITooltipBrick>();
        if (Main.Settings?.ShowInfirmaryUnitBreakdown != true || !TacticalCombatHelper.IsActive)
            return bricks;
        try
        {
            var combat = Game.Instance?.TacticalCombat;
            var leader = TacticalCombatHelper.GetArmyDataByFaction(ArmyFaction.Crusaders)?.LeaderData;
            if (combat?.Data == null) return bricks;
            // Match the native infirmary bar: include wiped-out squads but not
            // temporary summons, and ask the game itself which troops it covers.
            var squads = combat.Data.EnumerateUnitsExcludingSummoned()
                .Where(squad => squad.Faction == ArmyFaction.Crusaders)
                .Select(squad => squad.GetResults()).ToArray();
            if (squads.Length == 0) return bricks;
            var recoveries = combat.Infirmary.GetUnitsToResurrectAfterBattle(squads, leader, out _);
            bricks = BuildBattleRows(squads, recoveries);
            _errorReported = false;
        }
        catch (Exception exception)
        {
            bricks.Clear();
            ReportError(exception);
        }
        return bricks;
    }

    internal static List<ITooltipBrick> BuildBattleRows(
        IReadOnlyCollection<SquadResult> squads, IEnumerable<TacticalCombatInfirmary.ResurrectData>? recoveries)
    {
        var recovered = recoveries?.ToDictionary(recovery => recovery.Squad, recovery => recovery.CountToResurrect)
            ?? new Dictionary<SquadResult, int>();
        var bricks = new List<ITooltipBrick>
        {
            new TooltipBrickSeparator(),
            new TooltipBrickText("Current casualties × points per unit:")
        };
        long total = 0;
        foreach (var squad in squads)
        {
            var missing = InfirmaryBreakdownRules.MissingCount(squad.OriginalCount, squad.Count);
            var covered = recovered.TryGetValue(squad, out var count) ? Math.Max(0, Math.Min(missing, count)) : 0;
            total += InfirmaryBreakdownRules.Points(missing, squad.UnitExperience);
            bricks.Add(new TooltipBrickText(InfirmaryBreakdownRules.FormatCost(squad.Unit.CharacterName, missing, squad.UnitExperience)
                + (missing > 0 ? $" ({covered:N0}/{missing:N0} recoverable)" : "")));
        }
        bricks.Add(new TooltipBrickText($"Total casualty value: {total:N0} points"));
        return bricks;
    }

    private static void ReportError(Exception exception)
    {
        if (_errorReported) return;
        _errorReported = true;
        Main.ModEntry.Logger.Error($"Could not add the infirmary unit breakdown: {exception}");
    }

    [HarmonyPatch(typeof(ArmyLeaderStatsPCView), "BindViewImplementation")]
    private static class ArmyTooltipPatch
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.Select(instruction => new CodeInstruction(instruction)).ToList();
            var holder = AccessTools.Field(typeof(ArmyLeaderStatsPCView), "m_InfirmaryBonusHolder");
            var constructor = AccessTools.Constructor(typeof(TooltipTemplateSimple), new[] { typeof(string), typeof(string) });
            var holders = code.Select((instruction, index) => (instruction, index))
                .Where(pair => pair.instruction.LoadsField(holder)).ToArray();
            if (holders.Length != 1)
            {
                Main.ModEntry?.Logger.Error("Infirmary breakdown: unrecognized army tooltip holder; left unchanged.");
                return code;
            }
            var start = holders[0].index;
            var position = code.FindIndex(start, instruction => instruction.opcode == OpCodes.Newobj
                && Equals(instruction.operand, constructor));
            if (position < 0 || position - start > 16)
            {
                Main.ModEntry?.Logger.Error("Infirmary breakdown: unrecognized army tooltip constructor; left unchanged.");
                return code;
            }
            var call = code[position];
            var loadView = new CodeInstruction(OpCodes.Ldarg_0);
            loadView.labels.AddRange(call.labels);
            loadView.blocks.AddRange(call.blocks);
            call.labels.Clear();
            call.blocks.Clear();
            call.opcode = OpCodes.Call;
            call.operand = AccessTools.Method(typeof(InfirmaryUnitBreakdown), nameof(CreateArmyTooltip));
            code.Insert(position, loadView);
            // Replace just the infirmary template, keeping the original handler,
            // hover configuration and view-owned disposal (no duplicate handlers).
            return code;
        }
    }

    [HarmonyPatch(typeof(TooltipTemplateCombatHospitalProgress), nameof(TooltipTemplateCombatHospitalProgress.GetBody))]
    private static class BattleTooltipPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref IEnumerable<ITooltipBrick> __result)
        {
            if (Main.Settings?.ShowInfirmaryUnitBreakdown == true)
                __result = __result.Concat(BuildBattle());
        }
    }
}
