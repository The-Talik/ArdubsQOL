using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints.Area;
using Kingmaker.Crusade;
using Kingmaker.EntitySystem.Persistence;

namespace ArdubsQOL;

[HarmonyPatch(typeof(Game), "LoadArea", new[]
{
    typeof(BlueprintArea), typeof(BlueprintAreaEnterPoint), typeof(AutoSaveMode),
    typeof(bool), typeof(SaveInfo), typeof(Action)
})]
internal static class OutpostLoading
{
    private sealed class LoadScope
    {
        internal readonly Game Game;
        internal LoadScope(Game game) { Game = game; }
    }

    [ThreadStatic] private static LoadScope? _currentLoad;
    private static readonly MethodInfo NativeCleanup = AccessTools.Method(typeof(Game), nameof(Game.UnloadUnusedAssetsCoroutine));
    private static readonly Type? NativeIteratorType = NativeCleanup.GetCustomAttribute<IteratorStateMachineAttribute>()?.StateMachineType;

    [HarmonyPrefix]
    private static void BeginLoad(
        Game __instance, BlueprintArea area, AutoSaveMode autoSaveMode, bool forceUnload,
        SaveInfo? saveInfo, out LoadScope? __state)
    {
        __state = _currentLoad;
        var eligible = OutpostLoadingRules.CanDeferAssetCleanup(
            Main.Settings.FasterOutpostLoading,
            Classify(__instance.CurrentlyLoadedArea), Classify(area),
            saveInfo != null || autoSaveMode == AutoSaveMode.LoadFromSave,
            forceUnload, autoSaveMode == AutoSaveMode.WhenAreaIsUnloaded);
        _currentLoad = eligible ? new LoadScope(__instance) : null;
    }

    [HarmonyFinalizer]
    private static void EndLoad(LoadScope? __state)
    {
        _currentLoad = __state;
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ReplaceOutpostCleanups(IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.Select(instruction => new CodeInstruction(instruction)).ToList();
        var calls = code.Where(instruction => instruction.Calls(NativeCleanup)).ToArray();
        // Two normal sweeps, plus the optional debug/build-mode sweep. Leave
        // unknown loader layouts completely unchanged rather than guessing.
        if (calls.Length != 3)
        {
            Main.ModEntry?.Logger.Warning("Outpost loading: unrecognized cleanup layout; the optimization is disabled.");
            return code;
        }
        var replacement = AccessTools.Method(typeof(OutpostLoading), nameof(CreateCleanup));
        for (var index = 0; index < 2; index++)
        {
            calls[index].opcode = OpCodes.Call;
            calls[index].operand = replacement;
        }
        return code;
    }

    private static OutpostAreaKind Classify(BlueprintArea? area) =>
        area is SettlementBlueprintArea ? OutpostAreaKind.Construction
        : area?.IsGlobalMap == true ? OutpostAreaKind.GlobalMap : OutpostAreaKind.Other;

    private static IEnumerator<object> CreateCleanup(Game game)
    {
        var original = game.UnloadUnusedAssetsCoroutine();
        if (_currentLoad == null || !ReferenceEquals(_currentLoad.Game, game)
            || NativeIteratorType == null || original.GetType() != NativeIteratorType)
        {
            return original;
        }
        var moveNext = AccessTools.Method(NativeIteratorType, "MoveNext");
        if (moveNext == null || Harmony.GetPatchInfo(moveNext)?.Owners.Any() == true)
        {
            // Another mod may have added work that cannot safely be deferred.
            return original;
        }
        return DeferAssetCleanup(original, () =>
        {
            CustomPortraitsManager.Instance.Cleanup();
            AkCallbackManager.CleanupEventCallbacks();
            Main.LogDetail("Outpost loading: deferred one resource-cache, forced-GC and unused-asset sweep; portrait and audio callback cleanup retained. Ordinary area/save/menu loads still perform native cleanup.");
        });
    }

    internal static IEnumerator<object> DeferAssetCleanup(IEnumerator<object> original, Action lightweightCleanup)
    {
        using (original)
        {
            // Do not advance the native iterator: its waits, cache eviction, GC
            // and unused-asset scan are deferred together. Both normal passes
            // still run on the next non-outpost load, with their request-counter
            // ordering intact. Scene/entity unloading is never intercepted.
            lightweightCleanup();
            yield break;
        }
    }
}
