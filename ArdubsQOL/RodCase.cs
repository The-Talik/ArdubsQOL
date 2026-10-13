using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Items.Equipment;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Items;
using Kingmaker.RuleSystem.Rules.Abilities;
using Kingmaker.UI.UnitSettings;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.ActivatableAbilities;
using UnityEngine;

namespace ArdubsQOL;

internal sealed class RodPool
{
    internal RodPool(ItemEntityUsable item, BlueprintActivatableAbility toggle, MetamagicRodMechanics mechanics)
    {
        Toggle = toggle;
        Mechanics = mechanics;
        Name = item.Name;
        Description = item.Description;
        // Frame only the shared-menu sprite; its label is native UI text, not image pixels.
        Icon = RodIconFactory.WithGradeBorder(item.Icon ?? toggle.Icon, toggle.name)!;
    }

    internal BlueprintActivatableAbility Toggle { get; }
    internal MetamagicRodMechanics Mechanics { get; }
    internal BlueprintGuid Key => Toggle.AssetGuid;
    internal string Name { get; }
    internal string Description { get; }
    internal Sprite Icon { get; }
    internal List<ItemEntityUsable> Items { get; } = new();
    internal int Charges => RodPoolRules.TotalCharges(Items, item => item.Count, item => item.Charges);

    internal bool IsSuitable(AbilityData spell) =>
        spell.SourceItem == null && spell.Spellbook != null
        && spell.Blueprint.Type == AbilityType.Spell && spell.StickyTouch == null
        && Mechanics.IsSuitableAbility(spell);
}

internal static class RodCase
{
    // Only the native Ability fact for the Rods button is saved. These selections and
    // inventory views are rebuilt at runtime; no synthetic charges or item copies exist.
    private static readonly Dictionary<UnitEntityData, BlueprintGuid> Selections = new();
    private static Dictionary<BlueprintGuid, RodPool> _pools = new();
    private static Player? _player;
    private static int _poolFrame = -1;
    private static float _nextGrantTime;
    private static bool _errorReported;

    internal static bool IsButton(AbilityData? ability) =>
        ability?.Blueprint.AssetGuid == RodCaseBlueprints.AbilityGuid;

    internal static bool HasSpellList(UnitEntityData unit) =>
        unit.Descriptor.Spellbooks.Any(book => book.Blueprint.SpellList != null);

    internal static bool IsSelected(UnitEntityData unit, BlueprintGuid key) =>
        Main.Settings.EnableRodCase && Selections.TryGetValue(unit, out var selected)
        && selected == key && GetPools().TryGetValue(key, out var pool) && pool.Charges > 0;

    internal static bool HasSelection(UnitEntityData unit) =>
        Main.Settings.EnableRodCase && Selections.TryGetValue(unit, out var key)
        && GetPools().TryGetValue(key, out var pool) && pool.Charges > 0;

    internal static void ClearSelection(UnitEntityData unit) => Selections.Remove(unit);

    internal static void Reset()
    {
        Selections.Clear();
        InvalidateInventory();
        _nextGrantTime = 0;
    }

    private static void InvalidateInventory() => _poolFrame = -1;

    internal static IReadOnlyDictionary<BlueprintGuid, RodPool> GetPools()
    {
        var player = Game.Instance?.Player;
        if (!ReferenceEquals(_player, player))
        {
            Reset();
            _player = player;
        }
        if (_poolFrame == Time.frameCount) return _pools;

        var pools = new Dictionary<BlueprintGuid, RodPool>();
        if (Main.Settings.EnableRodCase && player?.Inventory != null)
        {
            foreach (var item in player.Inventory.Items.OfType<ItemEntityUsable>())
            {
                var toggle = item.Blueprint.ActivatableAbility;
                if (!item.IsIdentified || !item.IsSpendCharges || item.Count < 1
                    || !item.Blueprint.RestoreChargesOnRest || toggle == null
                    || !RodPoolRules.IsOrdinaryRod(toggle.name)) continue;
                var mechanics = toggle.Buff?.GetComponent<MetamagicRodMechanics>();
                if (mechanics == null || mechanics.ForceIgnoreSpellResist) continue;
                if (!pools.TryGetValue(toggle.AssetGuid, out var pool))
                {
                    pool = new RodPool(item, toggle, mechanics);
                    pools.Add(pool.Key, pool);
                }
                pool.Items.Add(item);
            }
        }
        _pools = pools;
        _poolFrame = Time.frameCount;
        return _pools;
    }

    internal static IEnumerable<RodPool> GetMenuPools() => GetPools().Values
        .OrderBy(pool => pool.Name, StringComparer.CurrentCultureIgnoreCase);

    internal static int GetCharges(BlueprintGuid key) =>
        GetPools().TryGetValue(key, out var pool) ? pool.Charges : 0;

    internal static void Toggle(UnitEntityData unit, BlueprintGuid key)
    {
        if (!Main.Settings.EnableRodCase || !HasSpellList(unit)) return;
        if (IsSelected(unit, key))
        {
            ClearSelection(unit);
            return;
        }
        InvalidateInventory();
        if (GetCharges(key) == 0) return;

        // Keep the normal one-rod-per-caster rule, including equipped unique rods.
        // Turning on an equipped rod also clears this caster's case selection.
        foreach (var ability in unit.ActivatableAbilities.RawFacts.ToArray())
        {
            if (ability.Blueprint.Group == ActivatableAbilityGroup.MetamagicRod && ability.IsOn)
                ability.TurnOffImmediately();
        }
        Selections[unit] = key;
    }

    internal static RodPool? GetApplicablePool(AbilityData? spell)
    {
        if (!Main.Settings.EnableRodCase || spell?.Caster?.Unit == null
            || !Selections.TryGetValue(spell.Caster.Unit, out var key)) return null;
        if (!GetPools().TryGetValue(key, out var pool) || pool.Charges < 1)
        {
            ClearSelection(spell.Caster.Unit);
            return null;
        }
        return pool.IsSuitable(spell) ? pool : null;
    }

    internal static Metamagic GetMetamagic(AbilityData spell) =>
        GetApplicablePool(spell)?.Mechanics.Metamagic ?? (Metamagic)0;

    internal static RodPool? CaptureCastPool(RuleCastSpell cast)
    {
        var spell = cast.Spell;
        if (!Main.Settings.EnableRodCase || spell.SourceItem != null || spell.Spellbook == null
            || spell.Blueprint.Type != AbilityType.Spell || spell.StickyTouch != null
            || !Selections.TryGetValue(cast.Initiator, out var key)) return null;
        InvalidateInventory();
        if (!GetPools().TryGetValue(key, out var pool) || pool.Charges == 0) return null;
        // The execution context has already resolved metamagic. In particular, do
        // not recheck Quicken's swift cooldown after the casting command started.
        return spell.SpellLevel <= pool.Mechanics.MaxSpellLevel
            && (cast.Context.Params.Metamagic & pool.Mechanics.Metamagic) == pool.Mechanics.Metamagic
            ? pool : null;
    }

    internal static void SpendCharge(RuleCastSpell cast, RodPool? pool)
    {
        if (pool == null || !RodPoolRules.ShouldSpendCharge(
            (cast.Context.Params.Metamagic & pool.Mechanics.Metamagic) == pool.Mechanics.Metamagic,
            cast.Success || cast.IsArcaneSpellFailed || cast.IsSpellFailed,
            cast.ForceFail, cast.IsUMDFailed, cast.IsDuplicateSpellApplied,
            cast.Spell.StickyTouch != null)) return;

        // Resolve the source only now: all casters see the same pool and debit one
        // real inventory rod, so regular saving and resting preserve its charges.
        InvalidateInventory();
        if (!GetPools().TryGetValue(pool.Key, out var current)) return;
        var item = RodPoolRules.FindChargeSource(current.Items, candidate => candidate.Charges);
        if (item == null) return;
        var source = item.Count > 1 ? item.Split(1) : item;
        if (!source.SpendCharges(cast.Initiator.Descriptor))
            throw new InvalidOperationException($"Could not spend a pooled charge of {pool.Name}.");
        if (source.Charges == 0 && source.ActivatableAbility?.IsOn == true)
            source.ActivatableAbility.TurnOffImmediately();
        InvalidateInventory();
        if (GetCharges(pool.Key) == 0)
        {
            foreach (var unit in Selections.Where(pair => pair.Value == pool.Key).Select(pair => pair.Key).ToArray())
                ClearSelection(unit);
        }
        Main.LogDetail($"Rods: {cast.Initiator.CharacterName} used {pool.Name}; {GetCharges(pool.Key)} shared charges remain.");
    }

    internal static void Update()
    {
        var player = Game.Instance?.Player;
        if (player == null || Time.unscaledTime < _nextGrantTime) return;
        _nextGrantTime = Time.unscaledTime + 0.5f;
        try
        {
            GetPools();
            var button = ResourcesLibrary.TryGetBlueprint<BlueprintAbility>(RodCaseBlueprints.AbilityGuid);
            if (button == null) return;
            foreach (var unit in player.AllCharacters.ToArray())
            {
                if (unit?.Facts == null) continue;
                var facts = unit.Abilities.RawFacts.Where(fact => fact.Blueprint == button).ToArray();
                if (Main.Settings.EnableRodCase && HasSpellList(unit))
                {
                    if (facts.Length == 0) unit.Facts.Add(new Ability(button, unit.Descriptor));
                    foreach (var duplicate in facts.Skip(1)) unit.Facts.Remove(duplicate);
                }
                else
                {
                    ClearSelection(unit);
                    foreach (var fact in facts) unit.Facts.Remove(fact);
                    RemoveButtonSlots(unit);
                }
            }
            foreach (var unit in Selections.Keys.ToArray())
            {
                if (!player.AllCharacters.Contains(unit) || !HasSelection(unit)) ClearSelection(unit);
            }
            _errorReported = false;
        }
        catch (Exception exception)
        {
            if (!_errorReported)
            {
                _errorReported = true;
                Main.ModEntry.Logger.Error($"Could not update the shared Rods menu; it will retry: {exception}");
            }
        }
    }

    private static void RemoveButtonSlots(UnitEntityData unit)
    {
        var slots = unit.UISettings.m_Slots;
        if (slots == null) return;
        for (var index = 0; index < slots.Length; index++)
        {
            if (slots[index] is MechanicActionBarSlotAbility slot && IsButton(slot.Ability))
                slots[index] = new MechanicActionBarSlotEmpty { Unit = unit };
        }
    }
}
