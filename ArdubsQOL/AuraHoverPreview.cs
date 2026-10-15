using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Controllers.Clicks.Handlers;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.PubSubSystem;
using Kingmaker.UI.MVVM._VM.ActionBar;
using Kingmaker.UI.UnitSettings;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.ActivatableAbilities;
using UnityEngine;

namespace ArdubsQOL;

// Uses native hover decals, not a cast, area-effect entity, or saved ability fact.
internal static class AuraHoverPreview
{
    private sealed class Targets : ITargetProvider
    {
        private readonly ActivatableAbility _aura;

        internal Targets(ActivatableAbility aura) => _aura = aura;
        public bool CanTargetAlly => true;
        public bool CanTargetEnemies => true;

        public bool WouldCurrentlyTarget(
            Vector3 targetPos,
            UnitEntityData unit,
            IShowAoEAffectedUIHandler.AoeType aoeType)
        {
            // This is always a caster-centered circle, never a cone/line or a
            // pointer-centered preview. Friendly hex recipients count too.
            return aoeType == IShowAoEAffectedUIHandler.AoeType.Any
                && unit != null && unit.View != null && unit.IsVisibleForPlayer
                && !unit.Descriptor.State.IsDead
                && FreezeManager.WouldSuspendTarget(_aura, unit);
        }
    }

    private static MechanicActionBarSlotActivableAbility? _slot;
    private static AbilityData? _circleAbility;
    private static Targets? _targets;
    private static bool _errorReported;

    internal static void OnHover(MechanicActionBarSlotActivableAbility slot, bool state)
    {
        var aura = slot.ActivatableAbility;
        if (!state || aura == null || !Blueprints.IsAura(aura)) return;

        try
        {
            if (!CanPreview(slot)) return;
            if (!ReferenceEquals(_slot, slot))
            {
                Clear();
                var guid = Blueprints.IsChantAura(aura) ? Blueprints.ChantAbilityGuid : Blueprints.CackleAbilityGuid;
                var blueprint = ResourcesLibrary.TryGetBlueprint<BlueprintAbility>(guid)
                    ?? throw new InvalidOperationException("Could not find the base Chant/Cackle hover ability.");
                _circleAbility = new AbilityData(blueprint, aura.Owner);
                _targets = new Targets(aura);
                _slot = slot;
            }

            EventBus.RaiseEvent((IAbilityTargetHoverUIHandler h) => h.HandleAbilityTargetHover(_circleAbility!, true));
            RefreshTargets();
            _errorReported = false;
        }
        catch (Exception exception)
        {
            Clear();
            ReportError(exception);
        }
    }

    internal static void BeforeHover(MechanicActionBarSlot? slot, bool state)
    {
        if (AuraHoverPreviewRules.ShouldClearForHover(_slot, slot, state)) Clear();
    }

    internal static void Release(MechanicActionBarSlot? slot)
    {
        if (_slot != null && ReferenceEquals(_slot, slot)) Clear();
    }

    internal static void Update()
    {
        if (_slot == null) return;
        try
        {
            if (!_slot.HoverState || !CanPreview(_slot))
            {
                Clear();
                return;
            }
            RefreshTargets();
        }
        catch (Exception exception)
        {
            Clear();
            ReportError(exception);
        }
    }

    private static bool CanPreview(MechanicActionBarSlotActivableAbility slot)
    {
        var game = Game.Instance;
        var caster = slot.ActivatableAbility.Owner.Unit;
        var turn = game?.TurnBasedCombatController?.CurrentTurn;
        return Main.Settings.EnableChantAura && slot.ActivatableAbility.Active
            && game?.Player != null && caster.View != null
            && game.State?.Units.Contains(caster) == true
            && game.SelectedAbilityHandler?.SelectedAbility == null
            && (turn?.Rider?.Commands.Empty ?? true)
            && (turn?.Mount?.Commands.Empty ?? true);
    }

    private static void RefreshTargets()
    {
        EventBus.RaiseEvent((IShowAoEAffectedUIHandler h) => h.HandleAoEMove(_slot!.Unit.Position, _targets!));
    }

    internal static void Clear()
    {
        if (_slot == null) return;
        var circle = _circleAbility;
        _slot = null;
        _circleAbility = null;
        _targets = null;
        try
        {
            // Clear ownership first so a late mouse-exit from an old slot cannot
            // erase a different ability's preview.
            EventBus.RaiseEvent((IAbilityTargetHoverUIHandler h) => h.HandleAbilityTargetHover(circle!, false));
            EventBus.RaiseEvent((IShowAoEAffectedUIHandler h) => h.HandleAoECancel());
        }
        catch (Exception exception)
        {
            ReportError(exception);
        }
    }

    private static void ReportError(Exception exception)
    {
        if (_errorReported) return;
        _errorReported = true;
        Main.ModEntry.Logger.Error($"Chant/Cackle aura hover preview failed: {exception}");
    }
}

[HarmonyPatch(typeof(MechanicActionBarSlotActivableAbility), nameof(MechanicActionBarSlotActivableAbility.OnHover))]
internal static class AuraHoverPreviewShowPatch
{
    [HarmonyPostfix]
    private static void Postfix(MechanicActionBarSlotActivableAbility __instance, bool state)
        => AuraHoverPreview.OnHover(__instance, state);
}

[HarmonyPatch(typeof(MechanicActionBarSlot), nameof(MechanicActionBarSlot.OnHover))]
internal static class AuraHoverPreviewSlotExitPatch
{
    [HarmonyPrefix]
    private static void Prefix(MechanicActionBarSlot __instance, bool state)
        => AuraHoverPreview.BeforeHover(__instance, state);
}

[HarmonyPatch(typeof(ActionBarSlotVM), nameof(ActionBarSlotVM.OnHover))]
internal static class AuraHoverPreviewVmExitPatch
{
    [HarmonyPrefix]
    private static void Prefix(ActionBarSlotVM __instance, bool state)
    {
        // Native turn-based action-in-progress guards also skip mouse exits.
        // Release our preview even when the original method returns early.
        AuraHoverPreview.BeforeHover(__instance.MechanicActionBarSlot, state);
    }
}

[HarmonyPatch]
internal static class AuraHoverPreviewSlotReleasePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(ActionBarSlotVM), "DisposeImplementation");
        yield return AccessTools.Method(typeof(ActionBarSlotVM), nameof(ActionBarSlotVM.SetMechanicSlot));
    }

    [HarmonyPrefix]
    private static void Prefix(ActionBarSlotVM __instance)
        => AuraHoverPreview.Release(__instance.MechanicActionBarSlot);
}

[HarmonyPatch(typeof(ClickWithSelectedAbilityHandler), nameof(ClickWithSelectedAbilityHandler.SetAbility))]
internal static class AuraHoverPreviewSelectionPatch
{
    [HarmonyPrefix]
    private static void Prefix() => AuraHoverPreview.Clear();
}
