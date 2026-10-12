using System;
using UnityEngine;
using UnityModManagerNet;

namespace ArdubsQOL;

public sealed class Settings : UnityModManager.ModSettings
{
    private const float MinimumSectionHeadingWidth = 120f;
    private const float MinimumOptionNameWidth = 260f;
    private const float OptionColumnGap = 12f;
    private const float OptionRowGap = 3f;
    private const float InspectorSliderWidth = 90f;

    private const string ChantAuraLabel = "Enable Chant Aura (Default On)";
    private const string ShifterAspectLabel = "Preserve Shifter Aspect Time (Default On)";
    private const string PauseOnLoadLabel = "Pause at Savegame Load (Default On)";
    private const string SideTooltipsLabel = "Side Quickbar Tooltips (Default On)";
    private const string DebuffCastersLabel = "Show Stackable Debuff Casters (Default On)";
    private const string HexRetriesLabel = "Show Accursed Hex Retries (Default On)";
    private const string RodCaseLabel = "Shared Rods Menu (Default On)";
    private const string NegativeLevelLabel = "Remove Negative Level on Level Up (Default Off)";
    private const string DiagnosticsLabel = "Detailed Diagnostics (Default Off)";
    private static readonly string[] PlacementChoices = { "Auto", "Left", "Right" };

    public bool EnableChantAura = true;
    public bool PreserveShifterAspectTime = true;
    public bool PauseAtSavegameLoad = true;
    public bool EnableSideQuickbarTooltips = true;
    public QuickbarTooltipSide QuickbarTooltipPlacement = QuickbarTooltipSide.Auto;
    public bool RemoveNegativeLevelOnLevelUp;
    public bool EnableRodCase = true;
    public float HoverInspectorFontScale = 1f;
    public bool ShowStackableDebuffCasters = true;
    public bool ShowAccursedHexRetries = true;
    public bool DetailedLogging;

    public void Draw()
    {
        var descriptionStyle = new GUIStyle(GUI.skin.label)
        {
            wordWrap = true
        };
        descriptionStyle.normal.textColor = new Color(0.67f, 0.78f, 0.84f);

        var headingStyle = new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold,
            fontSize = Math.Max(GUI.skin.label.fontSize + 2, 15),
            wordWrap = false
        };
        var headingWidth = Mathf.Max(MinimumSectionHeadingWidth,
            MeasureWidth(headingStyle, "Gameplay Changes"));
        var placementLabelWidth = MeasureWidth(GUI.skin.label, "Placement");
        var placementButtonWidth = Mathf.Max(64f,
            MeasureWidth(GUI.skin.button, "Auto"),
            MeasureWidth(GUI.skin.button, "Left"),
            MeasureWidth(GUI.skin.button, "Right"));
        var placementToolbarWidth = placementButtonWidth * PlacementChoices.Length;
        var inspectorLabelWidth = MeasureWidth(GUI.skin.label, "Hover Inspector Scale");
        var percentageWidth = MeasureWidth(GUI.skin.label, "100%");
        var optionColumnWidth = Mathf.Max(MinimumOptionNameWidth,
            placementLabelWidth + OptionColumnGap + placementToolbarWidth + 24f,
            inspectorLabelWidth + InspectorSliderWidth + percentageWidth + OptionColumnGap * 2f + 24f);
        foreach (var name in new[]
        {
            ChantAuraLabel, ShifterAspectLabel, PauseOnLoadLabel, SideTooltipsLabel,
            DebuffCastersLabel, HexRetriesLabel, RodCaseLabel, NegativeLevelLabel, DiagnosticsLabel
        })
        {
            optionColumnWidth = Mathf.Max(optionColumnWidth, MeasureWidth(GUI.skin.toggle, name));
        }

        DrawSection("QOL", () =>
        {
            EnableChantAura = DrawToggleRow(
                EnableChantAura,
                ChantAuraLabel,
                "Adds 30-foot Chant and Cackle auras that freeze eligible hex timers in turn-based combat.",
                descriptionStyle, optionColumnWidth);
            PreserveShifterAspectTime = DrawToggleRow(
                PreserveShifterAspectTime,
                ShifterAspectLabel,
                "Keeps a paid Shifter Aspect buff active until its current one-minute interval expires.",
                descriptionStyle, optionColumnWidth);
            PauseAtSavegameLoad = DrawToggleRow(
                PauseAtSavegameLoad,
                PauseOnLoadLabel,
                "Pauses the game when a loaded save and its destination area are ready.",
                descriptionStyle, optionColumnWidth);

            EnableSideQuickbarTooltips = DrawToggleRow(
                EnableSideQuickbarTooltips,
                SideTooltipsLabel,
                "Moves quickbar hover tooltips to the screen edge; Auto chooses the side opposite the active character.",
                descriptionStyle, optionColumnWidth);

            GUILayout.BeginHorizontal();
            GUILayout.BeginHorizontal(GUILayout.Width(optionColumnWidth));
            GUILayout.Label("Placement", GUILayout.Width(placementLabelWidth));
            GUILayout.Space(OptionColumnGap);
            QuickbarTooltipPlacement = (QuickbarTooltipSide)GUILayout.Toolbar(
                (int)QuickbarTooltipRules.Normalize(QuickbarTooltipPlacement),
                PlacementChoices, GUILayout.Width(placementToolbarWidth));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(OptionRowGap);

            GUILayout.BeginHorizontal();
            GUILayout.BeginHorizontal(GUILayout.Width(optionColumnWidth));
            GUILayout.Label("Hover Inspector Scale", GUILayout.Width(inspectorLabelWidth));
            GUILayout.Space(OptionColumnGap);
            var previousInspectorScale = HoverInspectorFontScale;
            HoverInspectorFontScale = HoverInspectorScale.Normalize(
                GUILayout.HorizontalSlider(HoverInspectorFontScale, 0.5f, 1f, GUILayout.Width(InspectorSliderWidth)));
            GUILayout.Space(OptionColumnGap);
            GUILayout.Label($"{HoverInspectorFontScale:P0}", GUILayout.Width(percentageWidth));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(OptionColumnGap);
            GUILayout.Label(
                "Scales the complete unit hover inspector so more information fits in the window.",
                descriptionStyle,
                GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            GUILayout.Space(OptionRowGap);

            if (!Mathf.Approximately(previousInspectorScale, HoverInspectorFontScale))
            {
                HoverInspectorScale.RefreshVisibleInspectors();
            }

            ShowStackableDebuffCasters = DrawToggleRow(
                ShowStackableDebuffCasters,
                DebuffCastersLabel,
                "Adds caster names to Evil Eye and other ambiguous duplicate debuffs in the unit hover inspector.",
                descriptionStyle, optionColumnWidth);
            ShowAccursedHexRetries = DrawToggleRow(
                ShowAccursedHexRetries,
                HexRetriesLabel,
                "Shows Tabletop Tweaks Accursed Hex retries in the hover inspector and outlines matching targets in gold while selecting the hex.",
                descriptionStyle, optionColumnWidth);
        }, headingStyle, headingWidth);

        DrawSection("Gameplay Changes", () =>
        {
            EnableRodCase = DrawToggleRow(
                EnableRodCase,
                RodCaseLabel,
                "Lets spellcasters use shared, pooled metamagic rod charges without equipping rods.",
                descriptionStyle, optionColumnWidth);
            RemoveNegativeLevelOnLevelUp = DrawToggleRow(
                RemoveNegativeLevelOnLevelUp,
                NegativeLevelLabel,
                "Removes one negative level when a party character completes a normal class level-up.",
                descriptionStyle, optionColumnWidth);
        }, headingStyle, headingWidth);

        DrawSection("Logging", () =>
        {
            DetailedLogging = DrawToggleRow(
                DetailedLogging,
                DiagnosticsLabel,
                "Writes detailed aura, shared rod charge, and negative-level recovery diagnostics to the UMM log.",
                descriptionStyle, optionColumnWidth);
        }, headingStyle, headingWidth);
    }

    private static float MeasureWidth(GUIStyle style, string text)
    {
        // UMM's font and UI scaling do not guarantee that a fixed pixel width fits
        // the text. Measure with the same skin that renders the control each frame.
        var measuringStyle = new GUIStyle(style)
        {
            fixedWidth = 0f,
            wordWrap = false
        };
        return Mathf.Ceil(measuringStyle.CalcSize(new GUIContent(text)).x)
            + measuringStyle.margin.horizontal + 8f;
    }

    private static void DrawSection(string heading, Action content, GUIStyle headingStyle, float headingWidth)
    {
        GUILayout.Space(8f);
        GUILayout.Box(GUIContent.none, GUILayout.Height(1f), GUILayout.ExpandWidth(true));
        GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
        GUILayout.Label(heading, headingStyle, GUILayout.Width(headingWidth));
        GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        try
        {
            content();
        }
        finally
        {
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }
    }

    private static bool DrawToggleRow(
        bool value,
        string name,
        string description,
        GUIStyle descriptionStyle,
        float optionColumnWidth)
    {
        GUILayout.BeginHorizontal();
        value = GUILayout.Toggle(value, name, GUILayout.Width(optionColumnWidth));
        GUILayout.Space(OptionColumnGap);
        GUILayout.Label(description, descriptionStyle, GUILayout.ExpandWidth(true));
        GUILayout.EndHorizontal();
        GUILayout.Space(OptionRowGap);
        return value;
    }

    public override void Save(UnityModManager.ModEntry modEntry)
    {
        Save(this, modEntry);
    }
}
