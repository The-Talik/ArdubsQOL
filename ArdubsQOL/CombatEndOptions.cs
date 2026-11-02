using System;
using System.Linq;
using Kingmaker;
using Kingmaker.PubSubSystem;

namespace ArdubsQOL;

internal sealed class CombatEndOptions : IPartyCombatHandler
{
    private static readonly CombatEndOptions Handler = new();
    private bool _wasInCombat;

    internal static void Initialize()
    {
        Handler._wasInCombat = Game.Instance?.Player?.IsInCombat == true;
        EventBus.Subscribe(Handler);
    }

    internal static bool ShouldStop(bool enabled, bool wasInCombat, bool inCombat, bool loadingSave) =>
        enabled && wasInCombat && !inCombat && !loadingSave;

    public void HandlePartyCombatStateChanged(bool inCombat)
    {
        var game = Game.Instance;
        var stopChantAuras = ShouldStop(
            Main.Settings.StopChantAurasAfterCombat,
            _wasInCombat,
            inCombat,
            game?.IsLoadingSave == true);
        var stopShifterAspects = ShouldStop(
            Main.Settings.StopShifterAspectsAfterCombat,
            _wasInCombat,
            inCombat,
            game?.IsLoadingSave == true);
        _wasInCombat = inCombat;

        // Loading an out-of-combat save or receiving another non-combat event
        // must not disable an ability that was deliberately enabled while exploring.
        if ((!stopChantAuras && !stopShifterAspects) || game?.Player == null)
        {
            return;
        }

        if (stopChantAuras) StopChantAuras();
        if (stopShifterAspects) StopShifterAspects();
    }

    private static void StopChantAuras()
    {
        try
        {
            var stopped = 0;
            foreach (var unit in Game.Instance.Player.AllCharacters.ToArray())
            {
                if (unit?.ActivatableAbilities == null)
                {
                    continue;
                }

                foreach (var aura in unit.ActivatableAbilities.RawFacts
                             .Where(aura => Blueprints.IsAura(aura) && (aura.IsOn || aura.IsStarted)).ToArray())
                {
                    if (aura.IsOn)
                    {
                        aura.IsOn = false;
                    }
                    else
                    {
                        aura.Stop(forceRemovedBuff: true);
                        ActionEconomy.Release(aura);
                        SilentRangeIndicator.Hide(aura);
                    }

                    stopped++;
                }
            }

            FreezeManager.UpdateNow();
            Main.LogDetail($"Combat-end aura option stopped {stopped} Chant/Cackle auras.");
        }
        catch (Exception exception)
        {
            Main.ModEntry.Logger.Error($"Could not turn off Chant/Cackle auras after combat: {exception}");
        }
    }

    private static void StopShifterAspects()
    {
        var stopped = 0;
        foreach (var unit in Game.Instance.Player.AllCharacters.ToArray())
        {
            if (unit?.ActivatableAbilities == null) continue;
            try
            {
                stopped += ShifterAspectCombatEnd.Stop(unit);
            }
            catch (Exception exception)
            {
                Main.ModEntry.Logger.Error($"Could not turn off Shifter Aspects after combat for {unit.CharacterName}: {exception}");
            }
        }
        Main.LogDetail($"Combat-end Shifter Aspect option stopped {stopped} aspect toggles.");
    }
}
