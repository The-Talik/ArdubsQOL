using HarmonyLib;
using Kingmaker.Blueprints;
using Kingmaker.Items.Slots;
using Kingmaker.UnitLogic.FactLogic;

namespace ArdubsQOL;

[HarmonyPatch(typeof(LockEquipmentSlot), "GetSlot")]
internal static class NightHagWeaponSlot
{
    private static readonly BlueprintGuid NightHagGuid = BlueprintGuid.Parse("460049291aa14ca7b472b6f7cfeb045b");

    private static void Postfix(LockEquipmentSlot __instance, ref ItemSlot? __result)
    {
        if (Main.Settings.UnlockNightHagWeaponSlot && __result is HandSlot
            && __instance.Fact.Blueprint.AssetGuid == NightHagGuid
            && !__instance.m_Deactivate)
        {
            // OnTurnOn stores this result for its matching OnTurnOff.
            __result = null;
        }
    }
}
