using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Kingmaker;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UI.ActionBar;
using Kingmaker.UI.MVVM._VM.ActionBar;
using Kingmaker.UI.MVVM._VM.Tooltip.Templates;
using Kingmaker.UI.UnitSettings;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Commands.Base;
using Owlcat.Runtime.UI.Tooltips;
using UnityEngine;

namespace ArdubsQOL;

// The native conversion popup accepts a list of mechanic slots. The marker's
// native Count is nonzero even for an empty inventory, so the Rods button can
// show an explanatory entry instead of executing an empty ability.
internal sealed class RodCaseConversion : SlotConversion
{
    internal RodCaseConversion(AbilityData button) : base(new List<AbilityData> { button }) { }
}

internal sealed class RodPoolSlot : MechanicActionBarSlot
{
    private readonly RodPool? _pool;
    public override bool IsNotAvailable => !Main.Settings.EnableRodCase;

    internal RodPoolSlot(UnitEntityData unit, RodPool? pool)
    {
        Unit = unit;
        _pool = pool;
    }

    public override string KeyName => _pool?.Toggle.name ?? "ArdubsQOL_EmptyRodCase";
    public override int GetResource() => _pool == null ? 0 : RodCase.GetCharges(_pool.Key);
    public override bool IsActive() => _pool != null && RodCase.IsSelected(Unit, _pool.Key);
    public override bool IsCasting() => false;
    public override bool IsRuntimeOnly() => true;
    public override bool IsBad() => Unit == null;
    public override Sprite GetIcon() => (_pool?.Icon
        ?? Kingmaker.Blueprints.ResourcesLibrary.TryGetBlueprint<Kingmaker.UnitLogic.Abilities.Blueprints.BlueprintAbility>(RodCaseBlueprints.AbilityGuid)?.Icon)!;
    public override Sprite GetDecorationSprite() => null!;
    public override Color GetDecorationColor() => Color.clear;
    public override string GetTitle() => _pool?.Name ?? "No ordinary metamagic rods";
    public override object GetContentData() => _pool?.Toggle!;
    public override string GetDescription() => _pool == null
        ? "Identified ordinary metamagic rods in the shared party inventory appear here. Unique rods are not included."
        : _pool.Description + $"\n\nShared charges remaining: {GetResource()}. "
            + "Click to select this rod for this character; click again to turn it off.";
    public override TooltipBaseTemplate GetTooltipTemplate() =>
        new TooltipTemplateSimple(GetTitle(), GetDescription());
    public override bool CanUseIfTurnBasedInternal() =>
        CanUseByActionType(false, UnitCommand.CommandType.Free);

    public override void OnClick()
    {
        base.OnClick();
        if (_pool != null && IsPossibleActive()) RodCase.Toggle(Unit, _pool.Key);
    }

    public override void UpdateSlotInternal(ActionBarSlot slot)
    {
        if (slot.ActiveMark == null) return;
        slot.ActiveMark.color = slot.RunningColor;
        slot.ActiveMark.gameObject.SetActive(IsActive());
    }
}

[HarmonyPatch(typeof(MechanicActionBarSlotAbility), nameof(MechanicActionBarSlotAbility.GetConvertedAbilityData))]
internal static class RodCaseConversionsPatch
{
    private static bool Prefix(MechanicActionBarSlotAbility __instance, ref SlotConversion __result)
    {
        if (!RodCase.IsButton(__instance.Ability)) return true;
        __result = new RodCaseConversion(__instance.Ability);
        return false;
    }
}

[HarmonyPatch(typeof(SlotConversion), nameof(SlotConversion.GetMechanicSlots))]
internal static class RodCaseConversionSlotsPatch
{
    private static bool Prefix(SlotConversion __instance, UnitEntityData unit,
        ref IEnumerable<MechanicActionBarSlot> __result)
    {
        if (__instance is not RodCaseConversion) return true;
        var entries = RodCase.GetMenuPools().Select(pool => (MechanicActionBarSlot)new RodPoolSlot(unit, pool)).ToList();
        if (entries.Count == 0) entries.Add(new RodPoolSlot(unit, null));
        __result = entries;
        return false;
    }
}

[HarmonyPatch(typeof(ActionBarSlotVM), nameof(ActionBarSlotVM.OnMainClick))]
internal static class RodCaseOpenMenuPatch
{
    private static bool Prefix(ActionBarSlotVM __instance)
    {
        if (__instance.MechanicActionBarSlot is not MechanicActionBarSlotAbility slot
            || !RodCase.IsButton(slot.Ability)) return true;
        if (Main.Settings.EnableRodCase && slot.Unit.Descriptor.State.IsConscious)
            __instance.OnShowConvertRequest();
        return false;
    }
}

[HarmonyPatch(typeof(MechanicActionBarSlot), nameof(MechanicActionBarSlot.IsActive))]
internal static class RodCaseActiveButtonPatch
{
    private static void Postfix(MechanicActionBarSlot __instance, ref bool __result)
    {
        if (__instance is MechanicActionBarSlotAbility slot && RodCase.IsButton(slot.Ability))
            __result = RodCase.HasSelection(slot.Unit);
    }
}

[HarmonyPatch(typeof(MechanicActionBarSlotAbility), nameof(MechanicActionBarSlotAbility.OnClick))]
internal static class RodCasePreventEmptyCastPatch
{
    private static bool Prefix(MechanicActionBarSlotAbility __instance) => !RodCase.IsButton(__instance.Ability);
}
