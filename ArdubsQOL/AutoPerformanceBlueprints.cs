using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Kingmaker.Blueprints;
using Kingmaker.Localization;
using Kingmaker.ResourceLinks;
using Kingmaker.UnitLogic.ActivatableAbilities;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Commands.Base;

namespace ArdubsQOL;

internal static class AutoPerformanceBlueprints
{
    internal static readonly BlueprintGuid LingeringPerformanceGuid = BlueprintGuid.Parse("17239b298065efc459cffe2220ecb559");

    // Explicitly reviewed native performances. Song of the Fallen and Dance of
    // the Dead clean up their raised units; Blazing Rondo can exhaust its allies.
    internal static readonly BlueprintGuid[] SourceGuids =
    {
        BlueprintGuid.Parse("12dc796147c42e04487fcad3aaa40cea"), // Archaeologist's Luck
        BlueprintGuid.Parse("08cc564a5e2c49e4b8eefef393d8041c"), // Inspire Ferocity
        BlueprintGuid.Parse("ac2ac148ea164282b93910569e4b4a33"), // Banshee's Requiem
        BlueprintGuid.Parse("0f34d20626f9459bae6ebda8b69e459b"), // Tragedy of False Hope
        BlueprintGuid.Parse("1b28d456a5b1b4744a1d87cf24309ad1"), // Flame Dancer
        BlueprintGuid.Parse("32d247b6e6b65794ab47fc372c444a96"), // Incite Rage (all)
        BlueprintGuid.Parse("b1d8fdffd132bfd428a8045b7b8b363c"), // Incite Rage (allies)
        BlueprintGuid.Parse("dbd7c54ba43e1d54592e037d63117f7b"), // Incite Rage (enemies)
        BlueprintGuid.Parse("d5ee8a2e5bf46c549988e9b09a59acd4"), // Storm Call
        BlueprintGuid.Parse("1bf09d2956d0f7a4eb4f6a2bcfb8970f"), // Inspire Tranquility
        BlueprintGuid.Parse("d99d63f84e180d44e8f92b9a832c609d"), // Bard Dirge of Doom
        BlueprintGuid.Parse("993908ad3fb81f34ba0ed168b7c61f58"), // Fascinate
        BlueprintGuid.Parse("ad8a93dfa2db7ac4e85133b5e4f14a5f"), // Frightening Tune
        BlueprintGuid.Parse("430ab3bb57f2cfc46b7b3a68afd4f74e"), // Inspire Competence
        BlueprintGuid.Parse("5250fe10c377fdb49be449dfe050ba70"), // Inspire Courage
        BlueprintGuid.Parse("be36959e44ac33641ba9e0204f3d227b"), // Inspire Greatness
        BlueprintGuid.Parse("a4ce06371f09f504fa86fcf6d0e021e4"), // Inspire Heroics
        BlueprintGuid.Parse("72e4699d1f86461429bf5c22866b5c4a"), // Sensei Inspire Competence
        BlueprintGuid.Parse("70274c5aa9124424c984217b62dabee8"), // Sensei Inspire Courage
        BlueprintGuid.Parse("29f4e6db62a482741b30fb548ce55598"), // Sensei Inspire Greatness
        BlueprintGuid.Parse("da129e268aaf38643a4fa2d9e39adc7f"), // Martyr Inspire Courage
        BlueprintGuid.Parse("1188f92d6c06cd1409bb6189039ccb08"), // Martyr Inspire Greatness
        BlueprintGuid.Parse("14f08110ef716574e89bceef1d20da15"), // Martyr Inspire Heroics
        BlueprintGuid.Parse("82f83a21ecbf9344d939c757152f5621"), // Battle Prowess
        BlueprintGuid.Parse("2aa2ee37286e2f94083f6a36cf40c17f"), // Insightful Contemplation
        BlueprintGuid.Parse("e7fef5dae0700cd428e92a9d049c6cf3"), // Song of Inspiration
        BlueprintGuid.Parse("44bfb3d43e68de84e9b5cef8defd2bfb"), // Skald Dirge of Doom
        BlueprintGuid.Parse("264e93ac44ace16488226b8f7756bf26"), // Inspired Rage
        BlueprintGuid.Parse("e8e5a5cc8b603d5448d9098cc20065f2")  // Song of Strength
    };

    internal static readonly Dictionary<BlueprintGuid, BlueprintGuid> Sources = new();
    private static readonly Dictionary<BlueprintGuid, BlueprintGuid> AbilityGuids = new();

    internal static BlueprintGuid AbilityGuid(BlueprintGuid source)
    {
        if (!AbilityGuids.TryGetValue(source, out var guid))
            AbilityGuids[source] = guid = MakeGuid(source, "Ability");
        return guid;
    }

    internal static bool IsAuto(ActivatableAbility? ability) =>
        ability != null && Sources.ContainsKey(ability.Blueprint.AssetGuid);

    internal static void Initialize()
    {
        foreach (var sourceGuid in SourceGuids)
        {
            var source = ResourcesLibrary.TryGetBlueprint<BlueprintActivatableAbility>(sourceGuid);
            if (source == null) continue;
            var abilityGuid = AbilityGuid(sourceGuid);
            Sources[abilityGuid] = sourceGuid;
            if (ResourcesLibrary.TryGetBlueprint(abilityGuid) != null) continue;

            var nameKey = $"ArdubsQOL.AutoPerformance.{sourceGuid}.Name";
            var descriptionKey = $"ArdubsQOL.AutoPerformance.{sourceGuid}.Description";
            LocalizationManager.CurrentPack.PutString(nameKey, $"{source.Name} — Auto");
            LocalizationManager.CurrentPack.PutString(descriptionKey,
                "Automatically performs for one round, then relies on Lingering Performance for two rounds before restarting. " +
                "Each restart uses the original performance's normal action and resources. " +
                "Only one automatic performance can be selected; manually starting or stopping a performance cancels automation. " +
                "During the lingering interval, the button counter shows rounds until the next restart. " +
                "Automation turns off after loading or resting.\n\n" + source.Description);
            var displayName = new LocalizedString { Key = nameKey };
            var description = new LocalizedString { Key = descriptionKey };
            var icon = AuraIconFactory.AddGoldFrame(source.Icon, $"ArdubsQOL_AutoPerformance_{sourceGuid}_Icon") ?? source.Icon;
            var buff = new BlueprintBuff
            {
                name = $"ArdubsQOL_AutoPerformance_{sourceGuid}_Buff",
                AssetGuid = MakeGuid(sourceGuid, "Buff"),
                m_DisplayName = displayName,
                m_Description = description,
                m_Icon = icon,
                m_Flags = BlueprintBuff.Flags.HiddenInUi,
                IsClassFeature = true,
                FxOnStart = new PrefabLink(),
                FxOnRemove = new PrefabLink(),
                ResourceAssetIds = Array.Empty<string>(),
                ComponentsArray = Array.Empty<BlueprintComponent>()
            };
            Add(buff);
            Add(new BlueprintActivatableAbility
            {
                name = $"ArdubsQOL_AutoPerformance_{sourceGuid}_Ability",
                AssetGuid = abilityGuid,
                m_DisplayName = displayName,
                m_Description = description,
                m_Icon = icon,
                m_Buff = BlueprintReference<BlueprintBuff>.CreateTyped<BlueprintBuffReference>(buff),
                ActivationType = AbilityActivationType.Immediately,
                m_ActivateWithUnitCommand = UnitCommand.CommandType.Free,
                Group = ActivatableAbilityGroup.None,
                DeactivateImmediately = true,
                DeactivateIfOwnerUnconscious = true,
                ActionBarAutoFillIgnored = true,
                ResourceAssetIds = Array.Empty<string>(),
                ComponentsArray = Array.Empty<BlueprintComponent>()
            });
        }
        Main.LogDetail($"Registered {Sources.Count} automatic performance blueprints.");
    }

    internal static bool Supports(BlueprintActivatableAbility source) =>
        source.Group == ActivatableAbilityGroup.BardicPerformance && !source.IsTargeted
        && source.ActivateWithUnitCommand && source.Buff != null
        && source.GetComponent<ActivatableAbilityUnitCommand>() == null
        && source.GetComponent<TurnOffImmediatelyWithUnitCommand>() == null
        && source.GetComponents<ActivatableAbilityResourceLogic>().Count() == 1
        && source.GetComponent<ActivatableAbilityResourceLogic>().SpendType
            == ActivatableAbilityResourceLogic.ResourceSpendType.NewRound;

    private static BlueprintGuid MakeGuid(BlueprintGuid source, string kind)
    {
        using var hash = MD5.Create();
        var bytes = hash.ComputeHash(Encoding.UTF8.GetBytes($"ArdubsQOL.AutoPerformance.{source}.{kind}"));
        return BlueprintGuid.Parse(new Guid(bytes).ToString("N"));
    }

    private static void Add(SimpleBlueprint blueprint)
    {
        ResourcesLibrary.BlueprintsCache.AddCachedBlueprint(blueprint.AssetGuid, blueprint);
        blueprint.OnEnable();
    }
}
