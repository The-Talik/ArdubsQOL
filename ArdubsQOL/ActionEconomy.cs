using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Kingmaker;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.GameModes;
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
    private static bool _errorReported;

    internal static void Update()
    {
        var game = Game.Instance;
        var player = game?.Player;
        if (game == null || player == null || game.IsLoadingSave
            || game.CurrentMode == GameModeType.Cutscene || !Main.Settings.EnableChantAura)
        {
            return;
        }

        try
        {
            foreach (var unit in player.AllCharacters.ToArray())
            {
                if (unit?.ActivatableAbilities == null)
                {
                    continue;
                }

                foreach (var aura in unit.ActivatableAbilities.RawFacts
                             .Where(aura => Blueprints.IsAura(aura) && aura.IsOn && aura.IsStarted).ToArray())
                {
                    // World-map travel has no unit movement speed, so it cannot
                    // pay the real-time upkeep cost. Rest uses native cleanup.
                    if (game.CurrentlyLoadedArea?.IsGlobalMap == true)
                    {
                        aura.IsOn = false;
                        continue;
                    }

                    if (!AuraMovement.UsesTurnBasedActions(unit))
                    {
                        Reservations.Remove(aura);
                    }
                    // Native OnNewRound remains responsible for normal TB turns.
                    // Only initialize missing reservations after turn preparation;
                    // Prepare clears cooldowns, including when a delayed turn resumes.
                    else if (!Reservations.TryGetValue(aura, out _)
                        && unit.IsCurrentUnit()
                        && Game.Instance.TurnBasedCombatController.CurrentTurn is { } turn
                        && (turn.Status == TurnController.TurnStatus.Preparing || turn.IsActing)
                        && !ReserveOnNewRound(aura))
                    {
                        aura.IsOn = false;
                        continue;
                    }

                    SilentRangeIndicator.Show(aura);
                }
            }

            _errorReported = false;
        }
        catch (Exception exception)
        {
            if (!_errorReported)
            {
                _errorReported = true;
                Main.ModEntry.Logger.Error($"Could not synchronize Chant/Cackle Aura action costs; it will retry: {exception}");
            }
        }
    }

    internal static bool CanActivate(UnitEntityData unit)
    {
        return ActionRules.CanActivate(
            CombatController.IsInTurnBasedCombat(),
            unit.IsInCombat,
            unit.IsCurrentUnit(),
            unit.UsedOneMoveAction());
    }

    internal static bool CanDelayWithReservation(TurnController turn, UnitEntityData? unit)
    {
        var controller = Game.Instance?.TurnBasedCombatController;
        if (Main.Settings?.EnableChantAura != true || unit?.ActivatableAbilities == null
            || !ReferenceEquals(controller?.CurrentTurn, turn) || !AuraMovement.UsesTurnBasedActions(unit))
        {
            return false;
        }

        var reserved = unit.ActivatableAbilities.RawFacts.Any(aura =>
            Blueprints.IsAura(aura) && aura.IsOn && aura.IsStarted
            && Reservations.TryGetValue(aura, out var reservation)
            && ActionRules.ReservationAlreadyCoversTurn(
                reservation.Reserved, reservation.TurnStartTime, controller!.TurnStartTime));
        var cooldown = turn.GetCooldown(unit);
        return ActionRules.CanDelayWithAuraReservation(
            reserved, turn.GetTimeMoved(unit), cooldown.StandardAction, cooldown.MoveAction, cooldown.SwiftAction);
    }

    internal static void ForgetDelayedReservations(UnitEntityData? unit)
    {
        if (unit?.ActivatableAbilities == null) return;
        foreach (var aura in unit.ActivatableAbilities.RawFacts.Where(Blueprints.IsAura))
            Reservations.Remove(aura);
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

        if (!AuraMovement.UsesTurnBasedActions(ability.Owner.Unit))
        {
            Reservations.Remove(ability);
            return true;
        }

        return Reserve(ability);
    }

    internal static bool ReserveOnNewRound(ActivatableAbility ability)
    {
        var unit = ability.Owner.Unit;
        if (!AuraMovement.UsesTurnBasedActions(unit))
        {
            Reservations.Remove(ability);
            return true;
        }

        if (!unit.IsCurrentUnit())
        {
            return true;
        }

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
