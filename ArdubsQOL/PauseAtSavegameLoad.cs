using HarmonyLib;
using Kingmaker;
using Kingmaker.EntitySystem.Persistence;

namespace ArdubsQOL;

[HarmonyPatch(typeof(Game), nameof(Game.LoadGame))]
internal static class PauseAtSavegameLoadPatch
{
    [HarmonyPostfix]
    private static void Postfix(SaveInfo saveInfo)
    {
        if (!Main.Settings.PauseAtSavegameLoad
            || saveInfo == null
            || !saveInfo.IsActuallySaved
            || !LoadingProcess.Instance.ScheduledProcessesHaveTag(
                LoadingProcessTag.Load,
                checkCurrentLoadingProcess: true))
        {
            return;
        }

        LoadingProcess.Instance.StartLoadingProcess(
            PauseAfterLoad,
            LoadingProcessTag.Load);
    }

    private static void PauseAfterLoad()
    {
        Game.Instance.PauseAfterLoad();
        Main.ModEntry.Logger.Log("Savegame load completed; pause queued.");
    }
}
