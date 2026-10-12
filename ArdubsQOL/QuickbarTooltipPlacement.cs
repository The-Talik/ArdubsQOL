using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Kingmaker;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UI.Common;
using Kingmaker.UI.MVVM._PCView.ActionBar;
using Kingmaker.UI.MVVM._PCView.Tooltip;
using Kingmaker.UI.MVVM._VM.ActionBar;
using Kingmaker.UI.MVVM._VM.Tooltip;
using Kingmaker.UI.MVVM._VM.Tooltip.Utils;
using Owlcat.Runtime.UI.Tooltips;
using UnityEngine;

namespace ArdubsQOL;

internal static class QuickbarTooltipPlacement
{
    private sealed class Origin
    {
        internal readonly UnitEntityData Unit;

        internal Origin(UnitEntityData unit)
        {
            Unit = unit;
        }
    }

    // Track the originating hover, not the template or the shared tooltip view.
    // Templates can be reused; the same view also displays inventory and unit tooltips.
    private static readonly ConditionalWeakTable<TooltipData, Origin> DataOrigins = new();
    private static readonly ConditionalWeakTable<TooltipVM, Origin> ViewModelOrigins = new();

    internal static void CaptureOrigin(TooltipHandler handler, TooltipData data)
    {
        DataOrigins.Remove(data);
        var component = handler.m_Component;
        if (component == null)
        {
            return;
        }

        var slot = component.GetComponentInParent<ActionBarSlotPCView>();
        if (slot == null || slot.GetComponentInParent<ActionBarPCView>() == null)
        {
            return;
        }

        var unit = (slot.GetViewModel() as ActionBarSlotVM)?.MechanicActionBarSlot?.Unit;
        if (unit != null)
        {
            DataOrigins.Add(data, new Origin(unit));
        }
    }

    internal static void CaptureViewModel(TooltipVM viewModel, TooltipData data)
    {
        if (data != null && DataOrigins.TryGetValue(data, out var origin))
        {
            ViewModelOrigins.Add(viewModel, origin);
        }
    }

    internal static void Apply(RectTransform window)
    {
        if (!Main.Settings.EnableSideQuickbarTooltips || window == null)
        {
            return;
        }

        var view = window.GetComponent<TooltipPCView>();
        var viewModel = view != null ? view.GetViewModel() as TooltipVM : null;
        if (viewModel == null || viewModel.IsComparative
            || !ViewModelOrigins.TryGetValue(viewModel, out var origin))
        {
            return;
        }

        var uiCamera = Game.Instance?.UI?.UICamera;
        var parent = window.parent as RectTransform;
        if (uiCamera == null || parent == null || uiCamera.pixelRect.width <= 0f
            || uiCamera.pixelRect.height <= 0f)
        {
            return;
        }

        float? characterX = null;
        var rig = Game.Instance?.UI?.GetCameraRig();
        var worldCamera = rig != null ? rig.Camera : null;
        if (worldCamera != null)
        {
            var projected = worldCamera.WorldToViewportPoint(origin.Unit.Position);
            if (projected.z > 0f)
            {
                // Convert to the UI camera's viewport in case its pixel rectangle differs.
                var screenX = worldCamera.pixelRect.x + projected.x * worldCamera.pixelRect.width;
                characterX = (screenX - uiCamera.pixelRect.x) / uiCamera.pixelRect.width;
            }
        }

        var side = QuickbarTooltipRules.ResolveSide(Main.Settings.QuickbarTooltipPlacement, characterX);
        Canvas.ForceUpdateCanvases();
        var corners = new Vector3[4];
        window.GetWorldCorners(corners);
        var bottomLeft = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]);
        var topRight = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[2]);
        var viewport = uiCamera.pixelRect;
        // A small resolution-scaled gap keeps the window's frame clear of the edge.
        var margin = 16f * viewport.height / 1080f;
        var offset = new Vector2(
            QuickbarTooltipRules.HorizontalOffset(side, bottomLeft.x, topRight.x - bottomLeft.x,
                viewport.x, viewport.width, margin),
            QuickbarTooltipRules.VerticalOffset(bottomLeft.y, topRight.y - bottomLeft.y,
                viewport.y, viewport.height));
        var pivotOnScreen = RectTransformUtility.WorldToScreenPoint(uiCamera, window.position);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parent, pivotOnScreen + offset, uiCamera, out var localPoint))
        {
            window.localPosition = new Vector3(localPoint.x, localPoint.y, window.localPosition.z);
        }
        // Only translate this hover. Vanilla placement resets the pooled window next time;
        // do not alter its anchors, scale, dimensions, or the click-opened info window.
    }

    internal static void ReportError(Exception exception)
    {
        Main.ModEntry.Logger.Error($"Quickbar tooltip placement failed; retaining normal placement: {exception}");
    }
}

[HarmonyPatch(typeof(TooltipHandler), "TooltipData", MethodType.Getter)]
internal static class QuickbarTooltipOriginPatch
{
    private static void Postfix(TooltipHandler __instance, TooltipData __result)
    {
        try
        {
            if (__result != null)
            {
                QuickbarTooltipPlacement.CaptureOrigin(__instance, __result);
            }
        }
        catch (Exception exception)
        {
            QuickbarTooltipPlacement.ReportError(exception);
        }
    }
}

[HarmonyPatch(typeof(TooltipVM), MethodType.Constructor, new[] { typeof(TooltipData), typeof(bool) })]
internal static class QuickbarTooltipViewModelPatch
{
    private static void Postfix(TooltipVM __instance, TooltipData data)
    {
        try
        {
            QuickbarTooltipPlacement.CaptureViewModel(__instance, data);
        }
        catch (Exception exception)
        {
            QuickbarTooltipPlacement.ReportError(exception);
        }
    }
}

[HarmonyPatch(typeof(UIUtility), nameof(UIUtility.SetPopupWindowPosition),
    new[] { typeof(RectTransform), typeof(RectTransform), typeof(List<Vector2>) })]
internal static class QuickbarTooltipPositionPatch
{
    private static void Postfix(RectTransform windowTransform)
    {
        try
        {
            QuickbarTooltipPlacement.Apply(windowTransform);
        }
        catch (Exception exception)
        {
            QuickbarTooltipPlacement.ReportError(exception);
        }
    }
}
