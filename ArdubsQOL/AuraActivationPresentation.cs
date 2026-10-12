using System;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Sound;
using Kingmaker.UnitLogic.ActivatableAbilities;

namespace ArdubsQOL;

internal static class AuraActivationPresentation
{
    private const string ChantSoundEvent = "ShamanHexChantAbility";
    private const string CackleSoundEvent = "VFX_Witch_Hex_Cackle";

    private static bool _errorReported;

    internal static void Play(ActivatableAbility aura)
    {
        var soundEvent = Blueprints.IsChantAura(aura)
            ? ChantSoundEvent
            : Blueprints.IsCackleAura(aura)
                ? CackleSoundEvent
                : null;
        if (soundEvent == null)
        {
            return;
        }

        PlaySound(aura.Owner.Unit, soundEvent);
    }

    private static void PlaySound(UnitEntityData caster, string soundEvent)
    {
        try
        {
            var view = caster.View;
            if (view == null)
            {
                Main.LogDetail($"Could not play {soundEvent} because {caster.CharacterName} has no active view.");
                return;
            }

            SoundEventsManager.PostEvent(soundEvent, view.gameObject);
            _errorReported = false;
            Main.LogDetail($"Played the native {soundEvent} sound for {caster.CharacterName}.");
        }
        catch (Exception exception)
        {
            if (_errorReported)
            {
                return;
            }

            _errorReported = true;
            Main.ModEntry.Logger.Error($"Could not play the native {soundEvent} sound: {exception}");
        }
    }
}
