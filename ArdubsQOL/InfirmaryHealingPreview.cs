using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Kingmaker.Armies;
using Kingmaker.Armies.TacticalCombat;
using Kingmaker.Armies.TacticalCombat.Parts;
using Kingmaker.UI.MVVM._PCView.TacticalCombat;
using Kingmaker.UI.MVVM._VM.TacticalCombat;
using Kingmaker.UnitLogic.Parts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ArdubsQOL;

internal static class InfirmaryHealingPreview
{
    private sealed class ViewState
    {
        internal RectTransform? Row;
        internal TextMeshProUGUI? Text;
        internal Image? Icon;
    }

    private static readonly ConditionalWeakTable<TacticalCombatAttackTooltipPCView, ViewState> Views = new();
    private static bool _errorReported;

    internal static void Apply(TacticalCombatAttackTooltipPCView view,
        TextMeshProUGUI unitCountText, Image unitCountIcon)
    {
        try
        {
            var model = view.GetViewModel() as TacticalCombatAttackTooltipVM;
            var target = model?.Target;
            var squad = target?.Get<UnitPartTacticalCombat>();
            if (model == null || squad == null || !TacticalCombatHelper.IsActive
                || !InfirmaryPreviewRules.CanDisplay(Main.Settings.ShowInfirmaryHealingPreview,
                    model.Heal, squad.Faction == ArmyFaction.Crusaders,
                    target!.Get<UnitPartSummonedMonster>() != null))
            {
                Hide(view);
                return;
            }

            // Use the same troop value as the native infirmary, not HP or a
            // hand-maintained table. Previewing never changes live squad counts.
            var unitValue = squad.GetResults().UnitExperience;
            InfirmaryPreviewRules.GetRange(model.MinDeath, model.MaxDeath,
                squad.OriginalCount, squad.Count, unitValue, out var low, out var high);
            var state = Views.GetValue(view, _ => new ViewState());
            if (state.Row == null || state.Text == null || state.Icon == null)
                CreateRow(view, state, unitCountText, unitCountIcon);
            state.Text!.text = InfirmaryPreviewRules.FormatRange(low, high);
            state.Text.color = unitCountText.color;
            state.Icon!.color = unitCountIcon.color;
            state.Row!.gameObject.SetActive(true);
            _errorReported = false;
        }
        catch (Exception exception)
        {
            Hide(view);
            if (_errorReported) return;
            _errorReported = true;
            Main.ModEntry.Logger.Error($"Could not display the crusade infirmary healing preview: {exception}");
        }
    }

    private static void CreateRow(TacticalCombatAttackTooltipPCView view, ViewState state,
        TextMeshProUGUI unitCountText, Image unitCountIcon)
    {
        var template = unitCountText.rectTransform.parent as RectTransform;
        if (template == null || unitCountIcon.rectTransform.parent != template
            || template.GetComponent<HorizontalLayoutGroup>() == null
            || template.parent.GetComponent<VerticalLayoutGroup>() == null)
            throw new InvalidOperationException("The crusade preview's native row layout is unrecognized.");

        var combatView = view.GetComponentInParent<TacticalCombatPCView>();
        var hospital = combatView?.m_HospitalProgressBarPCView;
        var hospitalIcon = hospital?.transform.Find("CurrentUnit/HospitalIcon/Icon")?.GetComponent<Image>();
        if (hospitalIcon == null || hospitalIcon.sprite == null)
            throw new InvalidOperationException("The crusade infirmary's native icon is unavailable.");

        if (state.Row != null) Object.Destroy(state.Row.gameObject);
        // This native row contains only layout, image, and TMP components. Its
        // parent already fits its contents, so the paper expands automatically.
        state.Row = Object.Instantiate(template, template.parent, false);
        state.Row.name = "ArdubsQOL_InfirmaryHealingPreview";
        state.Row.gameObject.SetActive(false);
        state.Row.SetSiblingIndex(template.GetSiblingIndex() + 1);
        state.Text = state.Row.GetComponentInChildren<TextMeshProUGUI>(true);
        state.Icon = state.Row.GetComponentInChildren<Image>(true);
        if (state.Text == null || state.Icon == null)
            throw new InvalidOperationException("The copied crusade preview row has no text or icon.");
        state.Text.raycastTarget = false;
        state.Icon.sprite = hospitalIcon.sprite;
        state.Icon.preserveAspect = true;
        state.Icon.raycastTarget = false;
    }

    internal static void Hide(TacticalCombatAttackTooltipPCView view)
    {
        if (Views.TryGetValue(view, out var state) && state.Row != null)
            state.Row.gameObject.SetActive(false);
    }
}

[HarmonyPatch(typeof(TacticalCombatAttackTooltipPCView), "UpdateText")]
internal static class InfirmaryHealingPreviewPatch
{
    private static void Postfix(TacticalCombatAttackTooltipPCView __instance,
        TextMeshProUGUI ___m_KillText, Image ___m_KillImage) =>
        InfirmaryHealingPreview.Apply(__instance, ___m_KillText, ___m_KillImage);
}

[HarmonyPatch(typeof(TacticalCombatAttackTooltipPCView), "DestroyViewImplementation")]
internal static class InfirmaryHealingPreviewClearPatch
{
    private static void Prefix(TacticalCombatAttackTooltipPCView __instance) =>
        InfirmaryHealingPreview.Hide(__instance);
}
