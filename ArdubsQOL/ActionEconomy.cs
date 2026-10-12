using System;
using System.Runtime.CompilerServices;
using Kingmaker;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic.ActivatableAbilities;
using Kingmaker.UnitLogic.Commands.Base;
using TurnBased.Controllers;

namespace ArdubsQOL;

internal static class ActionEconomy
{
    private sealed class Reservation
    {
        internal bool Reserved;
        internal TimeSpan TurnStartTime;
    }

    private static readonly ConditionalWeakTable<ActivatableAbility, Reservation> Reservations = new();

    internal static bool CanActivate(UnitEntityData unit)
    {
        return ActionRules.CanActivate(
            CombatController.IsInTurnBasedCombat(),
            unit.IsInCombat,
            unit.IsCurrentUnit(),
            unit.UsedOneMoveAction());
    }

    internal static string DescribeAvailability(UnitEntityData unit)
    {
        return $"turnBased={CombatController.IsInTurnBasedCombat()}, " +
            $"inCombat={unit.IsInCombat}, ownTurn={unit.IsCurrentUnit()}, " +
            $"moveUsed={unit.UsedOneMoveAction()}, moveCooldown={unit.CombatState.Cooldown.MoveAction:0.###}";
    }

    internal static bool ReserveOnActivation(ActivatableAbility ability)
    {
        if (!CanActivate(ability.Owner.Unit))
        {
            return false;
        }

        return Reserve(ability);
    }

    internal static bool ReserveOnNewRound(ActivatableAbility ability)
    {
        var reservation = Reservations.GetOrCreateValue(ability);
        var turnStartTime = Game.Instance?.TurnBasedCombatController?.TurnStartTime ?? TimeSpan.Zero;

        // TryStart initializes TimeToNextRound to zero, so the game calls OnNewRound once
        // immediately after activation. That callback still belongs to the same turn and
        // must not charge a second move action or switch the aura off.
        if (ActionRules.ReservationAlreadyCoversTurn(
                reservation.Reserved,
                reservation.TurnStartTime,
                turnStartTime))
        {
            Main.LogDetail(
                $"Kept the existing move reservation for {ability.Blueprint.name} " +
                "during its same-turn initial round callback.");
            return true;
        }

        reservation.Reserved = false;

        if (!CanActivate(ability.Owner.Unit))
        {
            return false;
        }

        return Reserve(ability);
    }

    internal static void Release(ActivatableAbility ability)
    {
        if (!Reservations.TryGetValue(ability, out var reservation) || !reservation.Reserved)
        {
            Reservations.Remove(ability);
            return;
        }

        var unit = ability.Owner.Unit;
        var turnStartTime = Game.Instance?.TurnBasedCombatController?.TurnStartTime ?? TimeSpan.Zero;
        if (CombatController.IsInTurnBasedCombat()
            && unit.IsInCombat
            && unit.IsCurrentUnit()
            && reservation.TurnStartTime == turnStartTime)
        {
            var cooldown = unit.CombatState.Cooldown;
            cooldown.MoveAction = ActionRules.ReleaseMoveCooldown(cooldown.MoveAction, reservedByAura: true);
        }

        reservation.Reserved = false;
        Reservations.Remove(ability);
        RefreshPrediction(unit);
    }

    private static bool Reserve(ActivatableAbility ability)
    {
        var unit = ability.Owner.Unit;
        unit.SpendAction(UnitCommand.CommandType.Move, isFullRound: false, timeSinceCommandStart: 0f);

        var reservation = Reservations.GetOrCreateValue(ability);
        reservation.Reserved = true;
        reservation.TurnStartTime = Game.Instance.TurnBasedCombatController.TurnStartTime;
        Main.ModEntry.Logger.Log(
            $"Reserved move action for {ability.Blueprint.name}; {DescribeAvailability(unit)}.");
        RefreshPrediction(unit);
        return true;
    }

    private static void RefreshPrediction(UnitEntityData unit)
    {
        var turn = Game.Instance?.TurnBasedCombatController?.CurrentTurn;
        if (turn?.SelectedUnit == unit)
        {
            turn.CalculatePredictionForAbility(null, forceNewPrediction: false);
        }
    }
}
