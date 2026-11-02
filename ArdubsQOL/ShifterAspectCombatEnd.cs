using System.Linq;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic.ActivatableAbilities;

namespace ArdubsQOL;

internal static class ShifterAspectCombatEnd
{
    // Chimeric sub-abilities use ShifterChimeraAspect and remember the selected
    // bonuses. Only the main ShifterAspect toggle spends the shared resource.
    internal static bool IsMainAspectGroup(ActivatableAbilityGroup group) =>
        group == ActivatableAbilityGroup.ShifterAspect;

    internal static int Stop(UnitEntityData unit)
    {
        var aspects = unit.ActivatableAbilities.RawFacts
            .Where(ability => ShifterAspectRules.ShouldStopAfterCombat(
                IsMainAspectGroup(ability.Blueprint.Group), ability.IsOn, ability.IsStarted)).ToArray();

        foreach (var aspect in aspects)
        {
            ShifterAspectTime.PreservePaidInterval(aspect, ref aspect.m_AppliedBuff);

            aspect.IsOn = false;
            if (aspect.IsStarted) aspect.Stop();
        }
        return aspects.Length;
    }
}
