using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.GameModes;
using Kingmaker.UI.UnitSettings;
using Kingmaker.UnitLogic.ActivatableAbilities;
using Kingmaker.UnitLogic.Commands;
using TurnBased.Controllers;
using UnityEngine;

namespace ArdubsQOL;

internal static class AutoPerformance
{
    internal sealed class Selection
    {
        internal readonly ActivatableAbility Toggle;
        internal readonly ActivatableAbility Source;
        internal readonly AutoPerformanceCycle Cycle = new();
        internal Selection(ActivatableAbility toggle, ActivatableAbility source)
        {
            Toggle = toggle;
            Source = source;
        }
    }

    private static readonly Dictionary<UnitEntityData, Selection> Selections = new();
    private static Player? _player;
    private static float _nextGrantTime;
    private static bool _errorReported;
    internal static ActivatableAbility? ChangingSource { get; private set; }

    internal static ActivatableAbility? Source(ActivatableAbility toggle) =>
        AutoPerformanceBlueprints.Sources.TryGetValue(toggle.Blueprint.AssetGuid, out var guid)
            ? toggle.Owner.ActivatableAbilities.RawFacts.FirstOrDefault(fact => fact.Blueprint.AssetGuid == guid && fact.Active)
            : null;

    private static bool Eligible(ActivatableAbility source) =>
        Main.Settings.EnableAutoPerformances && source.Active
        && source.Owner.State.Features.LingeringPerformance
        && AutoPerformanceBlueprints.Supports(source.Blueprint);

    internal static bool Available(ActivatableAbility toggle) => toggle.IsOn
        || (Source(toggle) is { } source && Eligible(source) && source.IsAvailable);

    internal static bool CanStart(ActivatableAbility source)
    {
        var unit = source.Owner.Unit;
        var game = Game.Instance;
        if (!Eligible(source) || !source.IsAvailable || !unit.State.CanAct || !unit.State.IsConscious
            || game == null || game.IsLoadingSave
            || (game.CurrentMode != GameModeType.Default && game.CurrentMode != GameModeType.Pause)) return false;
        if (!source.IsStarted && !CombatController.IsInTurnBasedCombat())
        {
            if (unit.CombatState.HasCooldownForCommand(UnitActivateAbility.GetCommandType(source))) return false;
            // Wait for a spell already chosen by the player rather than replacing
            // its command when a real-time restart becomes due.
            if (unit.Commands.Raw.Any(command => command is UnitUseAbility && !command.IsFinished)) return false;
        }
        return new MechanicActionBarSlotActivableAbility { Unit = unit, ActivatableAbility = source }.IsPossibleActive();
    }

    internal static void TurnedOn(ActivatableAbility toggle)
    {
        if (Source(toggle) is not { } source || !CanStart(source))
        {
            toggle.IsOn = false;
            return;
        }
        var unit = toggle.Owner.Unit;
        if (Selections.TryGetValue(unit, out var previous)) previous.Toggle.IsOn = false;
        foreach (var other in unit.ActivatableAbilities.RawFacts.ToArray())
            if (other != toggle && AutoPerformanceBlueprints.IsAuto(other) && other.IsOn) other.IsOn = false;
        var selection = new Selection(toggle, source);
        Selections[unit] = selection;
        // Adopting an already-running song avoids spending a second activation
        // action or resetting its native resource tick.
        if (source.IsStarted && source.IsOn)
        {
            if (source.TimeToNextRound > 0)
                selection.Cycle.Charge(Now - TimeSpan.FromSeconds(6 - Math.Min(6, source.TimeToNextRound)), TurnTime);
        }
        else
        {
            Start(selection);
        }
    }

    internal static void TurnedOff(ActivatableAbility toggle)
    {
        if (!Selections.TryGetValue(toggle.Owner.Unit, out var selection) || selection.Toggle != toggle) return;
        Selections.Remove(toggle.Owner.Unit);
        // Starting applies the native buff before the first resource tick. An
        // immediate cancellation must not turn that window into free lingering.
        if (selection.Source.IsStarted && selection.Source.IsOn && !selection.Cycle.Paid)
            WithSource(selection.Source, selection.Source.OnNewRound);
        StopSource(selection.Source);
    }

    internal static void ManualChange(ActivatableAbility source)
    {
        if (ChangingSource == source || source.Blueprint.Group != ActivatableAbilityGroup.BardicPerformance
            || !Selections.TryGetValue(source.Owner.Unit, out var selection)) return;
        // The user's native operation must retain its normal stop/start timing.
        Selections.Remove(source.Owner.Unit);
        selection.Toggle.IsOn = false;
    }

    internal static bool BeforeRound(ActivatableAbility source, out bool charge)
    {
        charge = false;
        if (!TrySelection(source, out var selection) || !source.IsStarted) return true;
        var cycle = selection.Cycle;
        if (!cycle.Paid)
        {
            charge = true;
            return true;
        }
        if (!cycle.ShouldStop(CombatController.IsInTurnBasedCombat() && source.Owner.Unit.IsInCombat, Now, TurnTime))
            return false;
        cycle.Linger(Now);
        StopSource(source);
        Main.LogDetail($"Auto performance: {source.Owner.CharacterName} stopped {source.Name}; restarting after two lingering rounds.");
        return false;
    }

    internal static void AfterRound(ActivatableAbility source, bool charge)
    {
        if (charge && source.IsStarted && TrySelection(source, out var selection))
            selection.Cycle.Charge(Now, TurnTime);
    }

    internal static string? Countdown(ActivatableAbility toggle) =>
        Selections.TryGetValue(toggle.Owner.Unit, out var selection) && selection.Toggle == toggle && selection.Cycle.Lingering
            ? $"{selection.Cycle.RoundsUntilRestart(Now)}r" : null;

    internal static void Update()
    {
        var game = Game.Instance;
        var player = game?.Player;
        if (player == null || game!.IsLoadingSave) return;
        if (!ReferenceEquals(_player, player))
        {
            Selections.Clear();
            _player = player;
            _nextGrantTime = 0;
        }
        try
        {
            foreach (var selection in Selections.Values.ToArray())
            {
                var unit = selection.Toggle.Owner.Unit;
                if (!selection.Toggle.IsOn || !Eligible(selection.Source) || unit.IsDetached
                    || !player.AllCharacters.Contains(unit)
                    || game.CurrentMode == GameModeType.GlobalMap || game.CurrentMode == GameModeType.Rest)
                {
                    selection.Toggle.IsOn = false;
                    TurnedOff(selection.Toggle);
                    continue;
                }
                if (selection.Cycle.Lingering)
                {
                    if (!selection.Cycle.IsDue(Now)) continue;
                    if (!selection.Source.IsAvailableByResources)
                    {
                        selection.Toggle.IsOn = false;
                        continue;
                    }
                    if (CanStart(selection.Source)) Start(selection);
                }
                else if (!selection.Source.IsStarted && !HasPendingStart(selection.Source))
                {
                    selection.Toggle.IsOn = false;
                }
            }

            if (Time.unscaledTime >= _nextGrantTime)
            {
                _nextGrantTime = Time.unscaledTime + 0.5f;
                var units = player.AllCharacters.Concat(player.AllCrossSceneUnits).Distinct().ToArray();
                foreach (var unit in units)
                {
                    if (unit?.Facts == null) continue;
                    Reconcile(unit);
                }
            }
            _errorReported = false;
        }
        catch (Exception exception)
        {
            if (!_errorReported)
            {
                _errorReported = true;
                Main.ModEntry.Logger.Error($"Could not update automatic Lingering Performances: {exception}");
            }
            StopAll();
        }
    }

    private static void Reconcile(UnitEntityData unit)
    {
        // Runtime mechanics flags are rebuilt by fact TurnOn callbacks, after
        // deserialization. Do not delete saved abilities during that interval.
        if (Main.Settings.EnableAutoPerformances && (!unit.IsPostLoadExecuted || !unit.Facts.IsTurnedOn)) return;
        foreach (var sourceGuid in AutoPerformanceBlueprints.SourceGuids)
        {
            var guid = AutoPerformanceBlueprints.AbilityGuid(sourceGuid);
            var toggles = unit.ActivatableAbilities.RawFacts.Where(fact => fact.Blueprint.AssetGuid == guid).ToArray();
            var source = unit.ActivatableAbilities.RawFacts.FirstOrDefault(fact => fact.Blueprint.AssetGuid == sourceGuid && !fact.IsDisposed);
            if (source != null && OwnsRequirements(source))
            {
                if (toggles.Length == 0 && Eligible(source)
                    && ResourcesLibrary.TryGetBlueprint<BlueprintActivatableAbility>(guid) is { } blueprint)
                {
                    unit.Facts.Add(new ActivatableAbility(blueprint));
                    unit.UISettings?.SetDirty();
                }
                foreach (var duplicate in toggles.Skip(1)) Remove(unit, duplicate);
                foreach (var toggle in toggles)
                    if ((toggle.IsOn || toggle.IsStarted) && !Selections.ContainsKey(unit)) toggle.TurnOffImmediately();
            }
            else
            {
                foreach (var toggle in toggles) Remove(unit, toggle);
            }
        }
    }

    private static bool OwnsRequirements(ActivatableAbility source) =>
        Main.Settings.EnableAutoPerformances && !source.IsDisposed && !source.TemporarilyDisabled
        && (source.Owner.State.Features.LingeringPerformance
            || source.Owner.Unit.Facts.List.Any(fact => !fact.IsDisposed
                && fact.Blueprint?.AssetGuid == AutoPerformanceBlueprints.LingeringPerformanceGuid));

    private static void Remove(UnitEntityData unit, ActivatableAbility toggle)
    {
        toggle.TurnOffImmediately();
        var slots = unit.UISettings?.m_Slots;
        if (slots != null)
            for (var index = 0; index < slots.Length; index++)
                if (slots[index] is MechanicActionBarSlotActivableAbility slot && slot.ActivatableAbility == toggle)
                    slots[index] = new MechanicActionBarSlotEmpty { Unit = unit };
        // Native detach keeps quickbar-backed activatables as disabled saved
        // facts. Clear that retention flag after removing their slots.
        toggle.HasActionBarSlot.ReleaseAll();
        unit.Facts.Remove(toggle);
        unit.UISettings?.SetDirty();
    }

    internal static void StopAll()
    {
        foreach (var selection in Selections.Values.ToArray())
        {
            selection.Toggle.IsOn = false;
            TurnedOff(selection.Toggle);
        }
    }

    internal static void BeforeLoad()
    {
        Selections.Clear();
        _player = null;
        _nextGrantTime = 0;
    }

    internal static void AfterLoad()
    {
        var player = Game.Instance?.Player;
        if (player == null) return;
        foreach (var unit in player.AllCharacters.Concat(player.AllCrossSceneUnits).Distinct().ToArray())
            foreach (var toggle in unit.ActivatableAbilities.RawFacts.Where(AutoPerformanceBlueprints.IsAuto).ToArray())
            {
                if (toggle.IsOn && Source(toggle) is { } source) StopSource(source);
                toggle.TurnOffImmediately();
            }
    }

    private static bool TrySelection(ActivatableAbility source, out Selection selection)
    {
        selection = null!;
        return Selections.Count != 0 && source.Blueprint.Group == ActivatableAbilityGroup.BardicPerformance
            && source.Owner?.Unit is { } unit && Selections.TryGetValue(unit, out selection)
            && selection.Source == source && selection.Toggle.IsOn;
    }

    private static bool HasPendingStart(ActivatableAbility source) => source.IsOn
        && source.Owner.Unit.Commands.Raw.Concat(source.Owner.Unit.Commands.Queue)
            .Any(command => command is UnitActivateAbility activate && activate.Ability == source && !command.IsFinished);

    private static void Start(Selection selection)
    {
        selection.Cycle.Begin();
        WithSource(selection.Source, () => selection.Source.IsOn = true);
        Main.LogDetail($"Auto performance: {selection.Source.Owner.CharacterName} requested {selection.Source.Name} using its native activation action.");
    }

    private static void StopSource(ActivatableAbility source)
    {
        WithSource(source, () =>
        {
            foreach (var command in source.Owner.Unit.Commands.Raw.Concat(source.Owner.Unit.Commands.Queue).ToArray())
            {
                if (command is not UnitActivateAbility activate || activate.Ability != source || command.IsActed || command.IsFinished) continue;
                command.Interrupt();
                source.Owner.Unit.Commands.Queue.Remove(command);
            }
            source.TurnOffImmediately();
        });
    }

    private static void WithSource(ActivatableAbility source, Action action)
    {
        var previous = ChangingSource;
        ChangingSource = source;
        try { action(); }
        finally { ChangingSource = previous; }
    }

    private static TimeSpan Now => Game.Instance.TimeController.GameTime;
    private static TimeSpan TurnTime => Game.Instance.TurnBasedCombatController?.TurnStartTime ?? TimeSpan.Zero;
}
