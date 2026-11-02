using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints.Area;
using Kingmaker.Crusade;
using Kingmaker.EntitySystem.Persistence;
using Kingmaker.GameModes;
using Kingmaker.Kingdom;
using Kingmaker.Kingdom.Settlements;
using Kingmaker.UI.Settlement;

namespace ArdubsQOL;

internal static class OutpostNavigation
{
    private sealed class SwitchRequest
    {
        internal readonly Game Game;
        internal readonly KingdomState Kingdom;
        internal readonly BlueprintArea SourceArea;
        internal readonly SettlementState Source;
        internal readonly SettlementState Target;
        internal readonly BlueprintAreaEnterPoint Entrance;
        internal bool Assigned;

        internal SwitchRequest(Game game, KingdomState kingdom, SettlementState source,
            SettlementState target, BlueprintAreaEnterPoint entrance)
        {
            Game = game;
            Kingdom = kingdom;
            SourceArea = game.CurrentlyLoadedArea;
            Source = source;
            Target = target;
            Entrance = entrance;
        }
    }

    private static SwitchRequest? _pending;

    internal static bool Busy => _pending != null
        || LoadingProcess.Instance.IsLoadingInProcess
        || Game.Instance.SaveManager.CurrentState != SaveManager.State.None
        || Game.Instance.SaveManager.CommitInProgress;

    internal static List<SettlementState> GetSettlements() =>
        KingdomState.Instance?.SettlementsManager.Settlements
            .Where(settlement => settlement?.SettlementBuildArea?.Area is SettlementBlueprintArea)
            .OrderBy(settlement => settlement.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList() ?? new List<SettlementState>();

    internal static bool TrySwitch(SettlementState target)
    {
        var game = Game.Instance;
        var kingdom = KingdomState.Instance;
        var source = kingdom?.CurrentSettlement;
        var entrance = target?.SettlementBuildArea;
        if (!OutpostNavigationRules.CanSwitch(
            Main.Settings.EnableOutpostSwitcher,
            game.CurrentlyLoadedArea is SettlementBlueprintArea
                && game.CurrentMode == GameModeType.KingdomSettlement,
            LoadingProcess.Instance.IsLoadingInProcess || game.LoadingSave != null,
            game.SaveManager.CurrentState != SaveManager.State.None || game.SaveManager.CommitInProgress,
            _pending != null,
            source != null && kingdom!.SettlementsManager.Settlements.Contains(source),
            target != null && kingdom != null && kingdom.SettlementsManager.Settlements.Contains(target),
            ReferenceEquals(source, target), entrance?.Area is SettlementBlueprintArea)) return false;

        var request = new SwitchRequest(game, kingdom!, source!, target!, entrance!);
        _pending = request;
        try
        {
            UIDraggedBuilding.Clear();
            Main.LogDetail($"Outpost switch: {source!.Name} -> {target!.Name}; preserving the original map return route.");
            // The public three-argument LoadArea teleports when both settlements
            // share a scene. Call the native scheduling overload so it disposes
            // the old area state and rebuilds all UI/entities, even in that case.
            // Do not change ReturnPoint, ExitSettlementToGlobalMap, or game time.
            game.LoadArea(entrance!.Area, entrance, AutoSaveMode.None, false, null, () => Complete(request));
            if (LoadingProcess.Instance.ScheduledProcessesHaveTag(LoadingProcessTag.Load, true)) return true;
            _pending = null;
            Main.ModEntry.Logger.Error("Outpost switch: the game did not schedule the requested settlement load.");
        }
        catch (Exception exception)
        {
            _pending = null;
            Main.ModEntry.Logger.Error($"Outpost switch failed: {exception}");
        }
        return false;
    }

    internal static void AfterAreaUnloading(Game game)
    {
        var request = _pending;
        if (request == null || !ReferenceEquals(request.Game, game)
            || !ReferenceEquals(request.Kingdom, KingdomState.Instance)
            || !ReferenceEquals(game.CurrentlyLoadedArea, request.SourceArea)
            || game.LoadingSave != null) return;

        // SettlementGameModeStarter cleared CurrentSettlement during the native
        // unload event. Assign only after every old-scene subscriber has run,
        // but before the new toolbar, grids and building models are initialized.
        request.Kingdom.CurrentSettlement = request.Target;
        request.Assigned = true;
    }

    internal static void BeforeSettlementBuildings()
    {
        var request = _pending;
        if (request?.Assigned == true && ReferenceEquals(request.Game, Game.Instance)
            && ReferenceEquals(request.Kingdom, KingdomState.Instance)
            && ReferenceEquals(request.Game.CurrentlyLoadedArea, request.Entrance.Area))
            request.Kingdom.CurrentSettlement = request.Target;
    }

    private static void Complete(SwitchRequest request)
    {
        if (!ReferenceEquals(_pending, request)) return;
        _pending = null;
        if (!request.Assigned || !ReferenceEquals(request.Game.CurrentlyLoadedArea, request.Entrance.Area)
            || !ReferenceEquals(request.Kingdom.CurrentSettlement, request.Target))
            Main.ModEntry.Logger.Error("Outpost switch: the requested settlement did not finish initializing correctly.");
        else Main.LogDetail($"Outpost switch completed: {request.Target.Name}.");
    }

    internal static void Update()
    {
        var request = _pending;
        if (request == null) return;
        if (!ReferenceEquals(Game.Instance, request.Game)
            || request.Game.LoadingSave != null
            || !ReferenceEquals(KingdomState.Instance, request.Kingdom))
        {
            _pending = null;
            return;
        }
        if (!LoadingProcess.Instance.IsLoadingInProcess)
        {
            _pending = null;
            Main.ModEntry.Logger.Error("Outpost switch: loading ended before the settlement-switch callback completed.");
        }
    }
}

[HarmonyPatch(typeof(Game), nameof(Game.HandleAreaBeginUnloading))]
internal static class OutpostSwitchUnloadPatch
{
    private static void Postfix(Game __instance, bool forDispose)
    {
        if (!forDispose) OutpostNavigation.AfterAreaUnloading(__instance);
    }
}

[HarmonyPatch(typeof(SettlementGameModeStarter), nameof(SettlementGameModeStarter.OnAreaDidLoad))]
internal static class OutpostSwitchBuildingsPatch
{
    private static void Prefix() => OutpostNavigation.BeforeSettlementBuildings();
}
