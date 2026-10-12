using System;
using Kingmaker.Blueprints;
using Kingmaker.ElementsSystem;
using Kingmaker.Localization;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Commands.Base;

namespace ArdubsQOL;

internal static class RodCaseBlueprints
{
    internal static readonly BlueprintGuid AbilityGuid = BlueprintGuid.Parse("53bf6eaf3d80490d998ec6b7b09fd2f8");
    private static readonly BlueprintGuid IconSourceGuid = BlueprintGuid.Parse("1cf04842d5dbd0f49946b1af1022cd1a");

    internal static void Initialize()
    {
        if (ResourcesLibrary.TryGetBlueprint(AbilityGuid) != null) return;
        const string nameKey = "ArdubsQOL.Rods.Name";
        const string descriptionKey = "ArdubsQOL.Rods.Description";
        LocalizationManager.CurrentPack.PutString(nameKey, "Rods");
        LocalizationManager.CurrentPack.PutString(descriptionKey,
            "Choose an ordinary metamagic rod from the shared party inventory without equipping it. " +
            "Charges from identical rods are pooled; lesser, regular, and greater rods remain separate. " +
            "Each character can select one rod type, and multiple characters can share the same pool. " +
            "An eligible spell spends one shared charge. Click the selected rod again to turn it off. " +
            "Selections turn off after resting or loading a save.");
        var sourceIcon = ResourcesLibrary.TryGetBlueprint<Kingmaker.Blueprints.Items.Equipment.BlueprintItemEquipmentUsable>(IconSourceGuid)?.Icon;
        var button = new BlueprintAbility
        {
            name = "ArdubsQOL_RodsAbility",
            AssetGuid = AbilityGuid,
            m_DisplayName = new LocalizedString { Key = nameKey },
            m_Description = new LocalizedString { Key = descriptionKey },
            m_Icon = AuraIconFactory.AddGoldFrame(sourceIcon, "ArdubsQOL_RodsIcon") ?? sourceIcon,
            Type = AbilityType.Supernatural,
            Range = AbilityRange.Personal,
            ActionType = UnitCommand.CommandType.Free,
            m_AutoUseIsForbidden = true,
            ActionBarAutoFillIgnored = true,
            CanTargetSelf = true,
            NotOffensive = true,
            ResourceAssetIds = Array.Empty<string>(),
            ComponentsArray = new BlueprintComponent[]
            {
                new AbilityEffectRunAction { Actions = new ActionList { Actions = Array.Empty<GameAction>() } }
            }
        };
        ResourcesLibrary.BlueprintsCache.AddCachedBlueprint(AbilityGuid, button);
        button.OnEnable();
        Main.ModEntry.Logger.Log("Registered the shared Rods menu blueprint.");
    }
}
