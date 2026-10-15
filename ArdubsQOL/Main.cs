using System;
using System.Reflection;
using HarmonyLib;
using UnityModManagerNet;

namespace ArdubsQOL;

public static class Main
{
    internal static UnityModManager.ModEntry ModEntry = null!;
    internal static Settings Settings = null!;

    public static bool Load(UnityModManager.ModEntry modEntry)
    {
        ModEntry = modEntry;

        try
        {
            Settings = UnityModManager.ModSettings.Load<Settings>(modEntry) ?? new Settings();
            modEntry.OnGUI = _ => Settings.Draw();
            modEntry.OnSaveGUI = entry => Settings.Save(entry);
            modEntry.OnUpdate = (_, _) =>
            {
                TransientAuraManager.Update();
                FreezeManager.Update();
                SilentRangeIndicator.Update();
                AuraHoverPreview.Update();
                AccursedHexIntegration.UpdateHighlights();
                RodCase.Update();
            };

            new Harmony(modEntry.Info.Id).PatchAll(Assembly.GetExecutingAssembly());
            modEntry.Logger.Log("Ardub's QOL loaded.");
            return true;
        }
        catch (Exception exception)
        {
            modEntry.Logger.Error(exception.ToString());
            return false;
        }
    }

    internal static void LogDetail(string message)
    {
        if (Settings.DetailedLogging)
        {
            ModEntry.Logger.Log(message);
        }
    }
}
