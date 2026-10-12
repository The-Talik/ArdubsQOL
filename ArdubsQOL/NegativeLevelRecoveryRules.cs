namespace ArdubsQOL;

internal static class NegativeLevelRecoveryRules
{
    public static bool ShouldRecover(
        bool enabled,
        bool isNormalLevelUp,
        bool isRespecFlow,
        bool isEligibleCharacter,
        bool wasAlreadyCommitted,
        bool isNowCommitted,
        int previousCharacterLevel,
        int currentCharacterLevel)
    {
        return enabled
            && isNormalLevelUp
            && !isRespecFlow
            && isEligibleCharacter
            && !wasAlreadyCommitted
            && isNowCommitted
            && currentCharacterLevel > previousCharacterLevel;
    }
}
