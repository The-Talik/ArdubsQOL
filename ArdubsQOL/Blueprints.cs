using System;
using HarmonyLib;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.Localization;
using Kingmaker.ResourceLinks;
using Kingmaker.UnitLogic.ActivatableAbilities;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Commands.Base;

namespace ArdubsQOL;

internal static class Blueprints
{
    internal static readonly BlueprintGuid ChantAuraAbilityGuid = BlueprintGuid.Parse("03a11e8c3439471aa6ed93ec8918e9c4");
    internal static readonly BlueprintGuid ChantAuraBuffGuid = BlueprintGuid.Parse("20361e8585d54988b87c208d94c476da");
    internal static readonly BlueprintGuid CackleAuraAbilityGuid = BlueprintGuid.Parse("cbaea3ea74e6445eba4d61652bcf1128");
    internal static readonly BlueprintGuid CackleAuraBuffGuid = BlueprintGuid.Parse("e6b70f18ea6b4a8f863ad0d331986354");

    internal static readonly BlueprintGuid ChantAbilityGuid = BlueprintGuid.Parse("6cd07c80aabf2b248a11921090de9c17");
    internal static readonly BlueprintGuid CackleAbilityGuid = BlueprintGuid.Parse("4bd01292a9bc4304f861a6a07f03b855");

    private const string ChantNameKey = "ArdubsQOL.Chant.Name";
    private const string ChantDescriptionKey = "ArdubsQOL.Chant.Description";
    private const string CackleNameKey = "ArdubsQOL.Cackle.Name";
    private const string CackleDescriptionKey = "ArdubsQOL.Cackle.Description";
    internal static void Initialize()
    {
        CreateAura(
            ChantAbilityGuid,
            ChantAuraAbilityGuid,
            ChantAuraBuffGuid,
            "ArdubsQOL_ChantAbility",
            "ArdubsQOL_ChantBuff",
            ChantNameKey,
            ChantDescriptionKey,
            "Chant Aura");

        CreateAura(
            CackleAbilityGuid,
            CackleAuraAbilityGuid,
            CackleAuraBuffGuid,
            "ArdubsQOL_CackleAbility",
            "ArdubsQOL_CackleBuff",
            CackleNameKey,
            CackleDescriptionKey,
            "Cackle Aura");
    }

    internal static bool IsAura(ActivatableAbility ability)
    {
        var guid = ability.Blueprint.AssetGuid;
        return guid == ChantAuraAbilityGuid || guid == CackleAuraAbilityGuid;
    }

    internal static bool IsChantAura(ActivatableAbility ability)
    {
        return ability.Blueprint.AssetGuid == ChantAuraAbilityGuid;
    }

    internal static bool IsCackleAura(ActivatableAbility ability)
    {
        return ability.Blueprint.AssetGuid == CackleAuraAbilityGuid;
    }

    internal static bool IsAuraBuff(BlueprintGuid guid)
    {
        return guid == ChantAuraBuffGuid || guid == CackleAuraBuffGuid;
    }

    private static void CreateAura(
        BlueprintGuid sourceAbilityGuid,
        BlueprintGuid auraAbilityGuid,
        BlueprintGuid auraBuffGuid,
        string abilityInternalName,
        string buffInternalName,
        string nameKey,
        string descriptionKey,
        string displayNameText)
    {
        if (ResourcesLibrary.TryGetBlueprint(auraAbilityGuid) != null)
        {
            return;
        }

        var sourceAbility = ResourcesLibrary.TryGetBlueprint<Kingmaker.UnitLogic.Abilities.Blueprints.BlueprintAbility>(sourceAbilityGuid)
            ?? throw new InvalidOperationException($"Could not find the base {displayNameText} source ability.");
        PutString(nameKey, displayNameText);
        PutString(
            descriptionKey,
            $"{displayNameText} suspends the countdown of select hexes within 30 feet. " +
            "Maintaining the aura uses the character's move action.");

        var displayName = new LocalizedString { Key = nameKey };
        var description = new LocalizedString { Key = descriptionKey };
        var auraIcon = AuraIconFactory.AddGoldFrame(sourceAbility.Icon, $"{abilityInternalName}Icon")
            ?? sourceAbility.Icon;

        var buff = new BlueprintBuff
        {
            name = buffInternalName,
            AssetGuid = auraBuffGuid,
            m_DisplayName = displayName,
            m_Description = description,
            m_Icon = auraIcon,
            FxOnStart = new PrefabLink(),
            FxOnRemove = new PrefabLink(),
            IsClassFeature = true,
            ComponentsArray = Array.Empty<BlueprintComponent>()
        };

        AddBlueprint(buff);

        var ability = new BlueprintActivatableAbility
        {
            name = abilityInternalName,
            AssetGuid = auraAbilityGuid,
            m_DisplayName = displayName,
            m_Description = description,
            m_Icon = auraIcon,
            ResourceAssetIds = Array.Empty<string>(),
            m_Buff = BlueprintReference<BlueprintBuff>.CreateTyped<BlueprintBuffReference>(buff),
            m_ActivateWithUnitCommand = UnitCommand.CommandType.Free,
            ActivationType = AbilityActivationType.Immediately,
            DeactivateImmediately = true,
            IsOnByDefault = false,
            OnlyInCombat = true,
            DeactivateIfCombatEnded = true,
            DeactivateIfOwnerDisabled = true,
            DeactivateIfOwnerUnconscious = true,
            DoNotTurnOffOnRest = false,
            Group = ActivatableAbilityGroup.None,
            WeightInGroup = 1,
            ComponentsArray = Array.Empty<BlueprintComponent>()
        };

        AddBlueprint(ability);

        Main.ModEntry.Logger.Log($"Registered {displayNameText} blueprint.");
    }

    private static void AddBlueprint(SimpleBlueprint blueprint)
    {
        ResourcesLibrary.BlueprintsCache.AddCachedBlueprint(blueprint.AssetGuid, blueprint);
        blueprint.OnEnable();
    }

    private static void PutString(string key, string value)
    {
        LocalizationManager.CurrentPack.PutString(key, value);
    }
}

[HarmonyPatch(typeof(BlueprintsCache), nameof(BlueprintsCache.Init))]
internal static class BlueprintsCachePatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix()
    {
        try
        {
            Blueprints.Initialize();
        }
        catch (Exception exception)
        {
            Main.ModEntry.Logger.Error($"Failed to create Chant Aura blueprints: {exception}");
        }
        try
        {
            RodCaseBlueprints.Initialize();
        }
        catch (Exception exception)
        {
            Main.ModEntry.Logger.Error($"Failed to create the shared Rods menu blueprint: {exception}");
        }
    }
}
