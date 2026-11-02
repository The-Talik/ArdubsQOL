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
        return !turnBased || !inCombat || (ownTurn && !usedOneMoveAction);
    }

    internal static bool ReservationAlreadyCoversTurn(
        bool reserved,
        TimeSpan reservedTurnStart,
        TimeSpan currentTurnStart)
    {
        return reserved && reservedTurnStart == currentTurnStart;
    }

    internal static bool CanDelayWithAuraReservation(
        bool reservedThisTurn,
        float timeMoved,
        float standardActionCooldown,
        float moveActionCooldown,
        float swiftActionCooldown)
    {
        return reservedThisTurn
            && timeMoved <= 0f
            && standardActionCooldown <= 0f
            && swiftActionCooldown <= 0f
            && moveActionCooldown >= 0f
            && moveActionCooldown <= MoveActionCooldown;
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
        bool matchingAuraActive)
    {
        return featureEnabled && matchingAuraActive;
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
