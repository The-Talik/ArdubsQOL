using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Kingmaker.UI.MVVM._PCView.ActionBar;
using Kingmaker.UI.MVVM._VM.ActionBar;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArdubsQOL;

internal static class RodIconLabelView
{
    private sealed class LabelState
    {
        internal RectTransform? Clip;
        internal TextMeshProUGUI? Text;
    }

    private static readonly ConditionalWeakTable<ActionBarBaseSlotView, LabelState> Labels = new();
    private static bool _errorReported;

    internal static void Apply(ActionBarBaseSlotView view, Image icon, TextMeshProUGUI template)
    {
        try
        {
            var slot = (view.GetViewModel() as ActionBarSlotVM)?.MechanicActionBarSlot as RodPoolSlot;
            if (slot == null || icon == null || icon.sprite == null
                || !RodPoolRules.TryGetIconLabel(slot.KeyName, out var name, out var grade))
            {
                Hide(view);
                return;
            }
            if (template == null || template.font == null)
                throw new InvalidOperationException("The rod menu's native font is unavailable.");

            var state = Labels.GetValue(view, _ => new LabelState());
            if (state.Clip == null || state.Text == null)
            {
                var clip = new GameObject("ArdubsQOL_RodLabelClip", typeof(RectTransform),
                    typeof(RectMask2D), typeof(LayoutElement));
                clip.layer = icon.gameObject.layer;
                state.Clip = (RectTransform)clip.transform;
                state.Clip.SetParent(icon.rectTransform, false);
                state.Clip.anchorMin = Vector2.zero;
                state.Clip.anchorMax = Vector2.one;
                state.Clip.offsetMin = Vector2.zero;
                state.Clip.offsetMax = Vector2.zero;
                clip.GetComponent<LayoutElement>().ignoreLayout = true;

                var text = new GameObject("ArdubsQOL_RodLabel", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                text.layer = icon.gameObject.layer;
                text.transform.SetParent(state.Clip, false);
                state.Text = text.GetComponent<TextMeshProUGUI>();
                state.Text.raycastTarget = false;
                state.Text.richText = false;
                state.Text.enableAutoSizing = false;
                state.Text.enableWordWrapping = false;
                state.Text.overflowMode = TextOverflowModes.Masking;
                state.Text.alignment = TextAlignmentOptions.BottomLeft;
                state.Text.fontStyle = FontStyles.Normal;
                state.Text.enableVertexGradient = false;
                state.Text.margin = Vector4.zero;
                var rect = state.Text.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.anchoredPosition = new Vector2(0f, 1f);
            }

            // Reuse the real font and material, not the charge-count object or
            // its subscriptions. The full word stays one line and is clipped;
            // it never wraps, shrinks to fit, or gains an ellipsis/background.
            state.Text.font = template.font;
            state.Text.fontSharedMaterial = template.fontSharedMaterial;
            state.Text.fontSize = RodIconLabelRules.FontSize(icon.rectTransform.rect.height);
            state.Text.rectTransform.sizeDelta = new Vector2(-4f, state.Text.fontSize * 1.5f);
            state.Text.text = name;
            var color = RodIconPixels.GradeColor(grade);
            state.Text.color = new Color32((byte)(color >> 16), (byte)(color >> 8), (byte)color, 255);
            state.Clip.gameObject.SetActive(true);
        }
        catch (Exception exception)
        {
            Hide(view);
            if (_errorReported) return;
            _errorReported = true;
            Main.ModEntry.Logger.Error($"Could not display the shared rod icon label: {exception}");
        }
    }

    internal static void Hide(ActionBarBaseSlotView view)
    {
        if (Labels.TryGetValue(view, out var state) && state.Clip != null)
            state.Clip.gameObject.SetActive(false);
    }
}

[HarmonyPatch(typeof(ActionBarBaseSlotView), "SetIcon")]
internal static class RodCaseIconLabelPatch
{
    private static void Postfix(ActionBarBaseSlotView __instance, Image ___m_Icon,
        TextMeshProUGUI ___m_ResourceCount) =>
        RodIconLabelView.Apply(__instance, ___m_Icon, ___m_ResourceCount);
}

[HarmonyPatch(typeof(ActionBarBaseSlotView), "DestroyViewImplementation")]
internal static class RodCaseIconLabelClearPatch
{
    private static void Prefix(ActionBarBaseSlotView __instance) => RodIconLabelView.Hide(__instance);
}
