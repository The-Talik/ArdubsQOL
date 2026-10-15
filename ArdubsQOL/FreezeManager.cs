using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Designers;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic.ActivatableAbilities;
using Kingmaker.UnitLogic.Buffs;
using TurnBased.Controllers;

namespace ArdubsQOL;

internal static class FreezeManager
{
    private sealed class FrozenTimer
    {
        internal TimeSpan Remaining;
        internal int SeenGeneration;
    }

    private const float AuraRangeMeters = 9.144f;

    private static readonly HashSet<BlueprintGuid> ChantEligibleBuffs = new()
    {
        BlueprintGuid.Parse("4f918c1923ee2964aaa0f4c57bd32501"), // ShamanHexEvilEyeACBuff
        BlueprintGuid.Parse("ef8d0845d542b644d9e99c9130a5b110"), // ShamanHexEvilEyeAttackBuff
        BlueprintGuid.Parse("5fc895512a6a35d4ca0827bf09eff01a"), // ShamanHexEvilEyeSavesBuff
        BlueprintGuid.Parse("32ba2d292233b7a4cbcc3884496a68f4"), // ShamanHexFortuneBuff
        BlueprintGuid.Parse("973720081b5f2174cb2dd229cef52ce0"), // ShamanHexFuryBuff
        BlueprintGuid.Parse("a04238ff3ffc6ac4e9ae866e6eb2d793"), // ShamanHexMisfortuneBuff
        BlueprintGuid.Parse("9e7a13c71f6be1749af81afc57640ccf")  // ShamanHexProtectiveLuckBuff
    };

    private static readonly HashSet<BlueprintGuid> CackleEligibleBuffs = new()
    {
        BlueprintGuid.Parse("3dd413240a9cf614bbd48dacb172d048"), // WitchHexEvilEyeACBuff
        BlueprintGuid.Parse("58ce9c1ad7596c64a8a0e1fd4395c2c7"), // WitchHexEvilEyeAttackBuff
        BlueprintGuid.Parse("71e20ae6290617b428d03a8047874ea7"), // WitchHexEvilEyeSavesBuff
        BlueprintGuid.Parse("9121b87785f37f94e861b03a103d03e2"), // WitchHexFortuneBuff
        BlueprintGuid.Parse("101d5ae5477029e4bac167b3557cb25f"), // WitchHexMisfortuneBuff
        BlueprintGuid.Parse("1ba3d3a0942d080448ce6aa865bbbd65")  // WitchHexProtectiveLuckBuff
    };

    private static readonly Dictionary<Buff, FrozenTimer> Frozen = new();
    private static int _generation;
    private static bool _errorReported;
    private static bool _updating;

    internal static void Update()
    {
        if (_updating)
        {
            return;
        }

        try
        {
            _updating = true;
            UpdateCore();
            _errorReported = false;
        }
        catch (Exception exception)
        {
            if (!_errorReported)
            {
                _errorReported = true;
                Main.ModEntry.Logger.Error($"Chant Aura timer update failed; it will retry automatically: {exception}");
            }
        }
        finally
        {
            _updating = false;
        }
    }

    internal static void UpdateNow()
    {
        Update();
    }

    internal static bool HasSuspendedBuffs => Frozen.Count > 0;

    internal static bool WouldSuspendTarget(ActivatableAbility aura, UnitEntityData target)
    {
        var caster = aura.Owner.Unit;
        var eligible = Blueprints.IsChantAura(aura) ? ChantEligibleBuffs : CackleEligibleBuffs;
        var now = Game.Instance.TimeController.GameTime;
        // Read-only preview: it does not require the aura to be on, extend a
        // timer, or revive an expired hex. Reuse the actual sustaining range.
        return target.Buffs.RawFacts.Any(buff => AuraHoverPreviewRules.CanHighlightBuff(
                buff.Manager != null,
                buff.IsPermanent,
                eligible.Contains(buff.Blueprint.AssetGuid),
                buff.Context?.MaybeCaster == caster,
                buff.EndTime,
                now,
                Frozen.TryGetValue(buff, out var timer) ? timer.Remaining : TimeSpan.Zero))
            && IsWithinRange(target, caster);
    }

    internal static AuraSuspensionKind GetSuspensionKind(Buff buff)
    {
        if (buff == null || !Frozen.ContainsKey(buff))
        {
            return AuraSuspensionKind.None;
        }

        var guid = buff.Blueprint.AssetGuid;
        if (ChantEligibleBuffs.Contains(guid))
        {
            return AuraSuspensionKind.Chant;
        }

        return CackleEligibleBuffs.Contains(guid)
            ? AuraSuspensionKind.Cackle
            : AuraSuspensionKind.None;
    }

    internal static void TryFreezeImmediately(Buff buff)
    {
        if (_updating || !Main.Settings.EnableChantAura || !IsEligible(buff))
        {
            return;
        }

        try
        {
            var caster = buff.Context?.MaybeCaster;
            if (caster != null
                && HasActiveAuraForBuff(caster, buff.Blueprint.AssetGuid)
                && IsWithinRange(buff.Owner.Unit, caster))
            {
                var now = Game.Instance.TimeController.GameTime;
                Freeze(buff, now, ++_generation, new HashSet<BuffCollection>());
            }
        }
        catch (Exception exception)
        {
            if (!_errorReported)
            {
                _errorReported = true;
                Main.ModEntry.Logger.Error($"Chant Aura failed while attaching a buff; it will retry automatically: {exception}");
            }
        }
    }

    private static void UpdateCore()
    {
        var game = Game.Instance;
        if (game?.Player == null)
        {
            return;
        }

        var now = game.Player.GameTime;
        if (!Main.Settings.EnableChantAura)
        {
            ResumeAll(now);
            return;
        }

        if (game.State?.Units == null
            || !CombatController.IsInTurnBasedCombat()
            || !game.Player.IsInCombat)
        {
            ResumeAll(now);
            return;
        }

        var casters = game.Player.AllCharacters
            .Where(HasAnyActiveAura)
            .ToHashSet();

        if (casters.Count == 0)
        {
            ResumeAll(now);
            return;
        }

        var generation = ++_generation;
        var changedCollections = new HashSet<BuffCollection>();

        foreach (var unit in game.State.Units.ToArray())
        {
            if (unit?.Buffs == null)
            {
                continue;
            }

            foreach (var buff in unit.Buffs.RawFacts.ToArray())
            {
                if (!IsEligible(buff))
                {
                    continue;
                }

                var caster = buff.Context?.MaybeCaster;
                if (caster == null
                    || !casters.Contains(caster)
                    || !HasActiveAuraForBuff(caster, buff.Blueprint.AssetGuid)
                    || !IsWithinRange(unit, caster))
                {
                    continue;
                }

                Freeze(buff, now, generation, changedCollections);
            }
        }

        foreach (var entry in Frozen.ToArray())
        {
            if (entry.Value.SeenGeneration == generation)
            {
                continue;
            }

            Resume(entry.Key, entry.Value, now, changedCollections);
            Frozen.Remove(entry.Key);
        }

        foreach (var collection in changedCollections)
        {
            collection.UpdateNextEvent();
        }
    }

    private static void Freeze(
        Buff buff,
        TimeSpan now,
        int generation,
        HashSet<BuffCollection> changedCollections)
    {
        if (buff.Manager == null || buff.IsPermanent)
        {
            Frozen.Remove(buff);
            return;
        }

        if (!Frozen.TryGetValue(buff, out var timer))
        {
            if (buff.EndTime <= now)
            {
                return;
            }

            timer = new FrozenTimer
            {
                Remaining = buff.EndTime - now
            };
            Frozen.Add(buff, timer);
            Main.LogDetail(
                $"Freezing {buff.Blueprint.name} on {buff.Owner.Unit.CharacterName} at {timer.Remaining.TotalSeconds:0.###} seconds.");
        }

        timer.SeenGeneration = generation;
        var desiredEndTime = now + timer.Remaining;

        // Normal Chant and Cackle extend an existing buff's EndTime. Preserve that
        // external increase instead of overwriting it with the previously frozen
        // duration on the next aura update.
        var updatedRemaining = ActionRules.PreserveExternalDurationExtension(
            timer.Remaining,
            now,
            buff.EndTime);
        if (updatedRemaining > timer.Remaining)
        {
            timer.Remaining = updatedRemaining;
            desiredEndTime = now + timer.Remaining;
            Main.LogDetail(
                $"Accepted an extension to {buff.Blueprint.name} on {buff.Owner.Unit.CharacterName}; " +
                $"the frozen duration is now {timer.Remaining.TotalSeconds:0.###} seconds.");
        }

        if (buff.EndTime != desiredEndTime)
        {
            buff.EndTime = desiredEndTime;
            changedCollections.Add(buff.Owner.Unit.Buffs);
        }
    }

    private static void Resume(
        Buff buff,
        FrozenTimer timer,
        TimeSpan now,
        HashSet<BuffCollection> changedCollections)
    {
        if (buff.Manager == null || buff.IsPermanent)
        {
            return;
        }

        buff.EndTime = now + timer.Remaining;
        changedCollections.Add(buff.Owner.Unit.Buffs);
        Main.LogDetail(
            $"Resuming {buff.Blueprint.name} on {buff.Owner.Unit.CharacterName} at {timer.Remaining.TotalSeconds:0.###} seconds.");
    }

    private static void ResumeAll(TimeSpan now)
    {
        if (Frozen.Count == 0)
        {
            return;
        }

        var changedCollections = new HashSet<BuffCollection>();
        foreach (var entry in Frozen)
        {
            Resume(entry.Key, entry.Value, now, changedCollections);
        }

        Frozen.Clear();
        foreach (var collection in changedCollections)
        {
            collection.UpdateNextEvent();
        }
    }

    private static bool IsEligible(Buff buff)
    {
        return buff != null
            && (ChantEligibleBuffs.Contains(buff.Blueprint.AssetGuid)
                || CackleEligibleBuffs.Contains(buff.Blueprint.AssetGuid))
            && !buff.IsPermanent;
    }

    private static bool HasAnyActiveAura(UnitEntityData caster)
    {
        return caster != null
            && caster.IsInCombat
            && caster.ActivatableAbilities.RawFacts.Any(
                ability => Blueprints.IsAura(ability) && ability.IsOn && ability.IsStarted);
    }

    private static bool HasActiveAuraForBuff(UnitEntityData caster, BlueprintGuid buffGuid)
    {
        return caster != null
            && caster.IsInCombat
            && caster.ActivatableAbilities.RawFacts.Any(
                ability => ability.IsOn
                    && ability.IsStarted
                    && ((Blueprints.IsChantAura(ability) && ChantEligibleBuffs.Contains(buffGuid))
                        || (Blueprints.IsCackleAura(ability) && CackleEligibleBuffs.Contains(buffGuid))));
    }

    private static bool IsWithinRange(UnitEntityData target, UnitEntityData caster)
    {
        // Base Chant/Cackle targeting includes the target's corporeal radius and
        // line of sight. Use the same helper so the aura matches highlighted units.
        return GameHelper.IsUnitInRange(target, caster.EyePosition, AuraRangeMeters, true);
    }
}

[HarmonyPatch(typeof(BuffCollection), "OnFactDidAttach")]
internal static class BuffAttachedPatch
{
    [HarmonyPostfix]
    private static void Postfix(Buff fact)
    {
        FreezeManager.TryFreezeImmediately(fact);
    }
}
