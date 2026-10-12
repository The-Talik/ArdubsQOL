using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UI.UnitSettings;
using Kingmaker.UnitLogic.ActivatableAbilities;

namespace ArdubsQOL;

internal static class TransientAuraManager
{
    private static bool _updating;
    private static bool _errorReported;

    internal static void Update()
    {
        if (_updating)
        {
            return;
        }

        var player = Game.Instance?.Player;
        if (player == null)
        {
            return;
        }

        try
        {
            _updating = true;
            if (!Main.Settings.EnableChantAura)
            {
                var units = new HashSet<UnitEntityData>();
                AddUnits(units, player.AllCharacters);
                AddUnits(units, player.AllCrossSceneUnits);
                foreach (var unit in units)
                {
                    RemoveAuras(unit);
                }

                _errorReported = false;
                return;
            }

            foreach (var unit in player.AllCharacters.ToArray())
            {
                if (unit?.Facts == null)
                {
                    continue;
                }

                EnsureAura(unit, Blueprints.ChantAbilityGuid, Blueprints.ChantAuraAbilityGuid);
                EnsureAura(unit, Blueprints.CackleAbilityGuid, Blueprints.CackleAuraAbilityGuid);
            }

            _errorReported = false;
        }
        catch (Exception exception)
        {
            if (!_errorReported)
            {
                _errorReported = true;
                Main.ModEntry.Logger.Error($"Could not grant Chant/Cackle Aura abilities; it will retry: {exception}");
            }
        }
        finally
        {
            _updating = false;
        }
    }

    private static void AddUnits(
        HashSet<UnitEntityData> destination,
        IEnumerable<UnitEntityData> source)
    {
        foreach (var unit in source)
        {
            if (unit != null)
            {
                destination.Add(unit);
            }
        }
    }

    private static void RemoveAuras(UnitEntityData unit)
    {
        var auras = unit.ActivatableAbilities.RawFacts
            .Where(Blueprints.IsAura)
            .ToArray();
        foreach (var aura in auras)
        {
            if (aura.IsOn)
            {
                aura.IsOn = false;
            }

            unit.Facts.Remove(aura);
        }

        var slots = unit.UISettings.m_Slots;
        if (slots != null)
        {
            for (var index = 0; index < slots.Length; index++)
            {
                if (slots[index] is MechanicActionBarSlotActivableAbility slot
                    && slot.ActivatableAbility != null
                    && Blueprints.IsAura(slot.ActivatableAbility))
                {
                    slots[index] = new MechanicActionBarSlotEmpty { Unit = unit };
                }
            }
        }

        if (auras.Length > 0)
        {
            Main.LogDetail($"Removed {auras.Length} saved Chant/Cackle Aura abilities from {unit.CharacterName}.");
        }
    }

    private static void EnsureAura(
        UnitEntityData unit,
        BlueprintGuid sourceAbilityGuid,
        BlueprintGuid auraAbilityGuid)
    {
        if (!unit.Abilities.RawFacts.Any(ability => ability.Blueprint.AssetGuid == sourceAbilityGuid)
            || unit.ActivatableAbilities.RawFacts.Any(ability => ability.Blueprint.AssetGuid == auraAbilityGuid))
        {
            return;
        }

        var blueprint = ResourcesLibrary.TryGetBlueprint<BlueprintActivatableAbility>(auraAbilityGuid);
        if (blueprint == null)
        {
            return;
        }

        var fact = blueprint.CreateFact(null, unit.Descriptor, null) as ActivatableAbility;
        if (fact != null && unit.Facts.Add(fact) != null)
        {
            Main.LogDetail($"Granted {blueprint.name} to {unit.CharacterName}.");
        }
    }
}
