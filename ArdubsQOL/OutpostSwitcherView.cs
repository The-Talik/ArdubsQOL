using System;
using System.Collections.Generic;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Crusade;
using Kingmaker.Kingdom;
using Kingmaker.Kingdom.Settlements;
using Kingmaker.UI.Settlement;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ArdubsQOL;

internal sealed class OutpostSwitcherView : MonoBehaviour
{
    private RectTransform? _root;
    private TextMeshProUGUI? _nativeName;
    private Color _nativeNameColor;
    private TMP_Dropdown? _dropdown;
    private Button? _previous;
    private Button? _next;
    private List<SettlementState> _settlements = new();
    private SettlementState? _displayed;
    private bool _failed;

    internal void Bind(TextMeshProUGUI nativeName)
    {
        Detach();
        _nativeName = nativeName;
        _nativeNameColor = nativeName.color;
        _failed = false;
        Refresh();
    }

    private void Update()
    {
        if (_nativeName == null || _failed) return;
        try { Refresh(); }
        catch (Exception exception)
        {
            _failed = true;
            Detach();
            Main.ModEntry.Logger.Error($"Could not display the outpost switcher: {exception}");
        }
    }

    private void Refresh()
    {
        if (_nativeName == null) return;
        var enabled = Main.Settings.EnableOutpostSwitcher
            && Game.Instance.CurrentlyLoadedArea is SettlementBlueprintArea
            && KingdomState.Instance?.CurrentSettlement != null;
        if (!enabled)
        {
            if (_root != null) _root.gameObject.SetActive(false);
            _nativeName.color = _nativeNameColor;
            return;
        }
        if (_root == null) CreateControls(_nativeName);
        _root!.gameObject.SetActive(true);
        // Keep the native text enabled so any ContentSizeFitter/LayoutGroup
        // still measures it. Only its own glyphs become transparent.
        _nativeName.color = new Color(_nativeNameColor.r, _nativeNameColor.g, _nativeNameColor.b, 0f);
        var current = KingdomState.Instance!.CurrentSettlement;
        if (!ReferenceEquals(_displayed, current))
        {
            _settlements = OutpostNavigation.GetSettlements();
            _displayed = current;
            _dropdown!.ClearOptions();
            _dropdown.AddOptions(_settlements.ConvertAll(settlement => settlement.Name));
            _dropdown.SetValueWithoutNotify(Math.Max(0, _settlements.IndexOf(current)));
            _dropdown.RefreshShownValue();
        }
        var interactable = _settlements.Count > 1 && !OutpostNavigation.Busy;
        _dropdown!.interactable = interactable;
        _previous!.interactable = interactable;
        _next!.interactable = interactable;
    }

    private void CreateControls(TextMeshProUGUI template)
    {
        _root = Rect("ArdubsQOL_OutpostSwitcher", template.rectTransform);
        _root.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        var layout = _root.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 4f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        var height = Mathf.Max(32f, template.fontSize * 1.4f);
        _previous = CreateButton("Previous", "<", template, height, () => Step(-1));

        var center = Rect("Settlements", _root);
        center.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        var background = Background(center);
        _dropdown = center.gameObject.AddComponent<TMP_Dropdown>();
        _dropdown.targetGraphic = background;
        SetColors(_dropdown);
        var caption = Text("Settlement", center, template);
        caption.rectTransform.offsetMin = new Vector2(8f, 0f);
        caption.rectTransform.offsetMax = new Vector2(-26f, 0f);
        _dropdown.captionText = caption;
        var arrow = Text("Arrow", center, template);
        arrow.text = "v";
        arrow.fontSize *= 0.7f;
        arrow.rectTransform.anchorMin = new Vector2(1f, 0f);
        arrow.rectTransform.sizeDelta = new Vector2(22f, 0f);
        arrow.rectTransform.anchoredPosition = new Vector2(-11f, 0f);

        var list = Rect("Template", center);
        list.anchorMin = Vector2.zero;
        list.anchorMax = Vector2.zero;
        list.pivot = new Vector2(0f, 1f);
        list.anchoredPosition = new Vector2(0f, -4f);
        // The closed control stays inside the native title. Give the popup
        // enough width for names; TMP keeps it inside the screen automatically.
        list.sizeDelta = new Vector2(Mathf.Max(template.rectTransform.rect.width, template.fontSize * 14f),
            height * OutpostNavigationRules.VisibleRows(OutpostNavigation.GetSettlements().Count));
        Background(list).color = new Color(0.13f, 0.11f, 0.08f, 0.98f);
        var viewport = Rect("Viewport", list);
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = Rect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = new Vector2(0f, height);
        var item = Rect("Item", content);
        item.anchorMin = new Vector2(0f, 0.5f);
        item.anchorMax = new Vector2(1f, 0.5f);
        item.sizeDelta = new Vector2(0f, height);
        var toggle = item.gameObject.AddComponent<Toggle>();
        toggle.targetGraphic = Background(item);
        SetColors(toggle);
        var selected = Rect("Selected", item);
        selected.anchorMax = new Vector2(0f, 1f);
        selected.sizeDelta = new Vector2(4f, -6f);
        selected.anchoredPosition = new Vector2(3f, 0f);
        var selectedGraphic = selected.gameObject.AddComponent<Image>();
        selectedGraphic.color = new Color(0.92f, 0.74f, 0.36f, 1f);
        selectedGraphic.raycastTarget = false;
        toggle.graphic = selectedGraphic;
        var itemText = Text("Name", item, template);
        itemText.alignment = TextAlignmentOptions.MidlineLeft;
        itemText.rectTransform.offsetMin = new Vector2(12f, 0f);
        itemText.rectTransform.offsetMax = new Vector2(-8f, 0f);
        _dropdown.itemText = itemText;
        var scroll = list.gameObject.AddComponent<ScrollRect>();
        scroll.content = content;
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = height;
        _dropdown.template = list;
        list.gameObject.SetActive(false);
        _dropdown.onValueChanged.AddListener(Select);
        _next = CreateButton("Next", ">", template, height, () => Step(1));
    }

    private Button CreateButton(string name, string label, TextMeshProUGUI template, float width, Action clicked)
    {
        var rect = Rect(name, _root!);
        rect.gameObject.AddComponent<LayoutElement>().preferredWidth = width;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = Background(rect);
        SetColors(button);
        Text("Label", rect, template).text = label;
        button.onClick.AddListener(() => clicked());
        return button;
    }

    private void Step(int direction) => Select(OutpostNavigationRules.StepIndex(
        _settlements.IndexOf(KingdomState.Instance.CurrentSettlement), direction, _settlements.Count));

    private void Select(int index)
    {
        if (index < 0 || index >= _settlements.Count) return;
        _dropdown?.Hide();
        if (!OutpostNavigation.TrySwitch(_settlements[index]) && _dropdown != null)
        {
            _dropdown.SetValueWithoutNotify(Math.Max(0, _settlements.IndexOf(KingdomState.Instance.CurrentSettlement)));
            _dropdown.RefreshShownValue();
        }
    }

    internal void Detach()
    {
        if (_root != null)
        {
            _root.gameObject.SetActive(false);
            Object.Destroy(_root.gameObject);
        }
        if (_nativeName != null) _nativeName.color = _nativeNameColor;
        _root = null;
        _nativeName = null;
        _dropdown = null;
        _previous = _next = null;
        _displayed = null;
        _settlements.Clear();
    }

    private void OnDestroy() => Detach();

    private static RectTransform Rect(string name, RectTransform parent)
    {
        var gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.layer = parent.gameObject.layer;
        var rect = (RectTransform)gameObject.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        return rect;
    }

    private static Image Background(RectTransform rect)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.color = Color.white;
        return image;
    }

    private static void SetColors(Selectable control)
    {
        var colors = ColorBlock.defaultColorBlock;
        colors.normalColor = new Color(0.13f, 0.11f, 0.08f, 0.95f);
        colors.highlightedColor = colors.selectedColor = new Color(0.32f, 0.26f, 0.13f, 1f);
        colors.pressedColor = new Color(0.44f, 0.34f, 0.15f, 1f);
        colors.disabledColor = new Color(0.12f, 0.11f, 0.1f, 0.65f);
        control.colors = colors;
    }

    private static TextMeshProUGUI Text(string name, RectTransform parent, TextMeshProUGUI template)
    {
        var text = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = template.font;
        text.fontSharedMaterial = template.fontSharedMaterial;
        text.fontSize = template.fontSize;
        text.fontStyle = template.fontStyle;
        text.color = template.color;
        text.alignment = TextAlignmentOptions.Midline;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.richText = false;
        text.raycastTarget = false;
        return text;
    }
}

[HarmonyPatch(typeof(SettlementToolbar), nameof(SettlementToolbar.Initialize))]
internal static class OutpostSwitcherBindPatch
{
    private static void Postfix(SettlementToolbar __instance, TextMeshProUGUI ___m_SettlementName)
    {
        if (___m_SettlementName == null) return;
        try
        {
            var view = __instance.GetComponent<OutpostSwitcherView>() ?? __instance.gameObject.AddComponent<OutpostSwitcherView>();
            view.Bind(___m_SettlementName);
        }
        catch (Exception exception)
        {
            __instance.GetComponent<OutpostSwitcherView>()?.Detach();
            Main.ModEntry.Logger.Error($"Could not initialize the outpost switcher: {exception}");
        }
    }
}

[HarmonyPatch(typeof(SettlementToolbar), nameof(SettlementToolbar.Dispose))]
internal static class OutpostSwitcherDisposePatch
{
    private static void Prefix(SettlementToolbar __instance) => __instance.GetComponent<OutpostSwitcherView>()?.Detach();
}
