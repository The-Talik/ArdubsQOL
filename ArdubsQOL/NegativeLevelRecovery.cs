using System;
using HarmonyLib;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.UI.MVVM._VM.CharGen;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Class.LevelUp;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Parts;

namespace ArdubsQOL;

[HarmonyPatch(typeof(LevelUpController), nameof(LevelUpController.Commit))]
internal static class NegativeLevelRecovery
{
    private readonly struct LevelUpSnapshot
    {
        public LevelUpSnapshot(LevelUpController controller)
        {
            Unit = controller.Unit;
            PreviousCharacterLevel = controller.Unit?.Progression.CharacterLevel ?? 0;
            IsNormalLevelUp = controller.State?.Mode == LevelUpState.CharBuildMode.LevelUp;
            IsRespecFlow = controller.State?.Mode == LevelUpState.CharBuildMode.Respec
                || RespecWindowVM.Instance?.IsRespec == true;
            WasAlreadyCommitted = controller.Committed;
        }

        public UnitEntityData? Unit { get; }

        public int PreviousCharacterLevel { get; }

        public bool IsNormalLevelUp { get; }

        public bool IsRespecFlow { get; }

        public bool WasAlreadyCommitted { get; }
    }

    private static void Prefix(LevelUpController __instance, out LevelUpSnapshot __state)
    {
        __state = new LevelUpSnapshot(__instance);
    }

    private static void Postfix(LevelUpController __instance, LevelUpSnapshot __state)
    {
        try
        {
            var unit = __state.Unit;
            var isEligibleCharacter = unit != null
                && unit.IsPlayerFaction
                && !unit.IsPet
                && unit.IsInCompanionRoster();

            if (unit == null
                || !NegativeLevelRecoveryRules.ShouldRecover(
                    Main.Settings.RemoveNegativeLevelOnLevelUp,
                    __state.IsNormalLevelUp,
                    __state.IsRespecFlow,
                    isEligibleCharacter,
                    __state.WasAlreadyCommitted,
                    __instance.Committed,
                    __state.PreviousCharacterLevel,
                    unit.Progression.CharacterLevel))
            {
                return;
            }

            var negativeLevels = unit.Get<UnitPartNegativeLevels>();
            if (negativeLevels == null || negativeLevels.Count < 1)
            {
                Main.LogDetail(
                    $"Level-up negative-level recovery found no negative levels on {unit.CharacterName}.");
                return;
            }

            var before = negativeLevels.Count;
            Rulebook.Trigger(
                new RuleHealEnergyDrain(
                    unit,
                    unit,
                    EnergyDrainHealType.None,
                    EnergyDrainHealType.One));
            var after = unit.Get<UnitPartNegativeLevels>()?.Count ?? 0;
            var removed = Math.Max(0, before - after);

            if (removed > 0)
            {
                Main.ModEntry.Logger.Log(
                    $"Level-up negative-level recovery removed one negative level from {unit.CharacterName}.");
            }
            else
            {
                Main.ModEntry.Logger.Warning(
                    $"Level-up negative-level recovery could not remove a negative level from {unit.CharacterName}.");
            }
        }
        catch (Exception exception)
        {
            Main.ModEntry.Logger.Error(
                $"Level-up negative-level recovery failed: {exception}");
        }
    }
}
