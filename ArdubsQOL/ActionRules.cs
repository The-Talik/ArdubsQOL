using System;

namespace ArdubsQOL;

internal static class ActionRules
{
    internal const float MoveActionCooldown = 3f;

    internal static bool CanActivate(
        bool turnBased,
        bool inCombat,
        bool ownTurn,
        bool usedOneMoveAction)
    {
        return turnBased && inCombat && ownTurn && !usedOneMoveAction;
    }

    internal static bool ReservationAlreadyCoversTurn(
        bool reserved,
        TimeSpan reservedTurnStart,
        TimeSpan currentTurnStart)
    {
        return reserved && reservedTurnStart == currentTurnStart;
    }

    internal static float ReleaseMoveCooldown(float currentCooldown, bool reservedByAura)
    {
        if (!reservedByAura)
        {
            return currentCooldown;
        }

        return Math.Max(0f, currentCooldown - MoveActionCooldown);
    }

    internal static bool ShouldUseStandardActionForManualChant(
        bool featureEnabled,
        bool turnBased,
        bool matchingAuraActive)
    {
        return featureEnabled && turnBased && matchingAuraActive;
    }

    internal static TimeSpan PreserveExternalDurationExtension(
        TimeSpan frozenRemaining,
        TimeSpan now,
        TimeSpan currentEndTime)
    {
        var currentRemaining = currentEndTime - now;
        return currentRemaining > frozenRemaining
            ? currentRemaining
            : frozenRemaining;
    }
}
