using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Kingmaker.UI.MVVM._PCView.Tooltip;
using Kingmaker.UI.MVVM._VM.Tooltip;
using Kingmaker.UI.MVVM._VM.Tooltip.Templates;
using UnityEngine;
using UnityEngine.UI;

namespace ArdubsQOL;

internal static class HoverInspectorScale
{
    private sealed class ViewState
    {
        internal bool Initialized;
        internal Vector3 BaseLocalScale;
        internal float BasePreferredHeight = -1f;
        internal float BasePreferredWidth = -1f;
    }

    private static readonly ConditionalWeakTable<TooltipBaseView, ViewState> ViewStates = new();

    internal static float Normalize(float scale)
    {
        if (scale <= 0f)
        {
            return 1f;
        }

        return Mathf.Clamp(Mathf.Round(scale * 20f) / 20f, 0.5f, 1f);
    }

    internal static void Apply(TooltipBaseView view)
    {
        var state = GetState(view);
        var scale = IsUnitInspector(view)
            ? Normalize(Main.Settings.HoverInspectorFontScale)
            : 1f;

        view.transform.localScale = new Vector3(
            state.BaseLocalScale.x * scale,
            state.BaseLocalScale.y * scale,
            state.BaseLocalScale.z);

        ApplyStoredDimensions(view, state, scale);
    }

    internal static void Reset(TooltipBaseView view)
    {
        var state = GetState(view);
        view.transform.localScale = state.BaseLocalScale;
        ApplyStoredDimensions(view, state, 1f);
    }

    internal static void RefreshVisibleInspectors()
    {
        foreach (var view in UnityEngine.Object.FindObjectsOfType<TooltipBaseView>())
        {
            if (view.isActiveAndEnabled)
            {
                Apply(view);
            }
        }
    }

    private static bool IsUnitInspector(TooltipBaseView view)
    {
        return (view.GetViewModel() as TooltipVM)?.MainTemplate is TooltipTemplateUnitInspect;
    }

    private static ViewState GetState(TooltipBaseView view)
    {
        var state = ViewStates.GetValue(view, _ => new ViewState());
        if (!state.Initialized)
        {
            state.Initialized = true;
            state.BaseLocalScale = view.transform.localScale;
        }

        return state;
    }

    private static float CurrentScale(TooltipBaseView view)
    {
        return IsUnitInspector(view)
            ? Normalize(Main.Settings.HoverInspectorFontScale)
            : 1f;
    }

    private static void CaptureAndScaleHeight(TooltipBaseView view)
    {
        var layout = view.GetComponent<LayoutElement>();
        if (layout == null)
        {
            return;
        }

        var state = GetState(view);
        state.BasePreferredHeight = layout.preferredHeight;
        layout.preferredHeight = ScaleDimension(state.BasePreferredHeight, CurrentScale(view));
    }

    private static void CaptureAndScaleWidth(TooltipBaseView view)
    {
        var layout = view.GetComponent<LayoutElement>();
        if (layout == null)
        {
            return;
        }

        var state = GetState(view);
        state.BasePreferredWidth = layout.preferredWidth;
        layout.preferredWidth = ScaleDimension(state.BasePreferredWidth, CurrentScale(view));
    }

    private static void ApplyStoredDimensions(TooltipBaseView view, ViewState state, float scale)
    {
        var layout = view.GetComponent<LayoutElement>();
        if (layout == null)
        {
            return;
        }

        if (state.BasePreferredHeight >= 0f)
        {
            layout.preferredHeight = ScaleDimension(state.BasePreferredHeight, scale);
        }

        if (state.BasePreferredWidth >= 0f)
        {
            layout.preferredWidth = ScaleDimension(state.BasePreferredWidth, scale);
        }
    }

    private static float ScaleDimension(float dimension, float scale)
    {
        return dimension < 0f ? dimension : dimension / scale;
    }

    [HarmonyPatch(typeof(TooltipBaseView), "BindViewImplementation")]
    private static class TooltipBaseViewBindPatch
    {
        [HarmonyPostfix]
        private static void Postfix(TooltipBaseView __instance)
        {
            RunSafely("scale the unit hover inspector", () => Apply(__instance));
        }
    }

    [HarmonyPatch(typeof(TooltipBaseView), "SetHeight")]
    private static class TooltipBaseViewSetHeightPatch
    {
        [HarmonyPostfix]
        private static void Postfix(TooltipBaseView __instance)
        {
            RunSafely("scale the unit hover inspector height", () => CaptureAndScaleHeight(__instance));
        }
    }

    [HarmonyPatch(typeof(TooltipBaseView), "SetWidth")]
    private static class TooltipBaseViewSetWidthPatch
    {
        [HarmonyPostfix]
        private static void Postfix(TooltipBaseView __instance)
        {
            RunSafely("scale the unit hover inspector width", () => CaptureAndScaleWidth(__instance));
        }
    }

    [HarmonyPatch(typeof(TooltipBaseView), "DestroyViewImplementation")]
    private static class TooltipBaseViewDestroyPatch
    {
        [HarmonyPrefix]
        private static void Prefix(TooltipBaseView __instance)
        {
            RunSafely("restore the unit hover inspector scale", () => Reset(__instance));
        }
    }

    private static void RunSafely(string operation, Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            Main.ModEntry.Logger.Error($"Could not {operation}: {exception}");
        }
    }
}
