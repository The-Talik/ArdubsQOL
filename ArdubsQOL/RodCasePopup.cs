using System;
using System.Linq;
using HarmonyLib;
using Kingmaker.UI.MVVM._PCView.ActionBar;
using Kingmaker.UI.MVVM._VM.ActionBar;
using UnityEngine;
using UnityEngine.UI;

namespace ArdubsQOL;

internal sealed class RodCasePopup : MonoBehaviour
{
    private const float ScreenMargin = 16f;
    private ActionBarConvertedPCView? _view;
    private RectTransform? _container;
    private RectTransform? _viewport;
    private RectTransform? _content;
    private RectTransform? _canvas;
    private GridLayoutGroup? _nativeGrid;
    private ContentSizeFitter? _nativeFitter;
    private GridLayoutGroup? _grid;
    private ScrollRect? _scroll;
    private Vector2 _nativeSize;
    private Vector2 _nativePosition;
    private bool _gridEnabled;
    private bool _fitterEnabled;
    private bool _active;
    private int _count;
    private Vector2 _canvasSize;
    private readonly Vector3[] _corners = new Vector3[4];

    internal static bool IsRodMenu(ActionBarConvertedVM? model) => model != null
        && model.Slots.Count > 0 && model.Slots.All(slot => slot.MechanicActionBarSlot is RodPoolSlot);

    internal static int ColumnCount(int count, float cellWidth, float spacing, float padding, float availableWidth)
    {
        var compactColumns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(Math.Max(1, count))));
        var fittingColumns = Math.Max(1, (int)Math.Floor((availableWidth - padding + spacing) / (cellWidth + spacing)));
        return Math.Min(compactColumns, fittingColumns);
    }

    internal void Open(ActionBarConvertedPCView view, RectTransform container)
    {
        Restore();
        _nativeGrid = container.GetComponent<GridLayoutGroup>();
        _nativeFitter = container.GetComponent<ContentSizeFitter>();
        _canvas = view.GetComponentInParent<Canvas>()?.rootCanvas.transform as RectTransform;
        if (_nativeGrid == null || _nativeFitter == null || _canvas == null)
            throw new InvalidOperationException("The Rod Box popup's native layout is unavailable.");
        _view = view;
        _container = container;
        _nativeSize = container.sizeDelta;
        _nativePosition = container.anchoredPosition;
        _gridEnabled = _nativeGrid.enabled;
        _fitterEnabled = _nativeFitter.enabled;
        _active = true;
        if (_viewport == null) CreateViewport(container);
        _nativeGrid.enabled = false;
        _nativeFitter.enabled = false;
        _grid!.cellSize = _nativeGrid.cellSize;
        _grid.spacing = _nativeGrid.spacing;
        _grid.padding = _nativeGrid.padding;
        _grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        _grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        _grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        _grid.childAlignment = TextAnchor.UpperLeft;
        foreach (var slot in view.m_Slots) slot.transform.SetParent(_content, false);
        _viewport!.gameObject.SetActive(true);
        _count = view.m_Slots.Count;
        Resize();
        _scroll!.verticalNormalizedPosition = 1f;
    }

    private void CreateViewport(RectTransform container)
    {
        var viewport = new GameObject("ArdubsQOL_RodMenuViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        viewport.SetActive(false);
        viewport.layer = container.gameObject.layer;
        _viewport = (RectTransform)viewport.transform;
        _viewport.SetParent(container, false);
        _viewport.anchorMin = Vector2.zero;
        _viewport.anchorMax = Vector2.one;
        _viewport.offsetMin = _viewport.offsetMax = Vector2.zero;
        viewport.GetComponent<Image>().color = Color.clear;

        var content = new GameObject("ArdubsQOL_RodMenuContent", typeof(RectTransform), typeof(GridLayoutGroup));
        content.layer = container.gameObject.layer;
        _content = (RectTransform)content.transform;
        _content.SetParent(_viewport, false);
        _content.anchorMin = _content.anchorMax = new Vector2(0f, 1f);
        _content.pivot = new Vector2(0f, 1f);
        _content.anchoredPosition = Vector2.zero;
        _grid = content.GetComponent<GridLayoutGroup>();
        _scroll = viewport.GetComponent<ScrollRect>();
        _scroll.content = _content;
        _scroll.viewport = _viewport;
        _scroll.horizontal = false;
        _scroll.movementType = ScrollRect.MovementType.Clamped;
        _scroll.inertia = false;
    }

    private void LateUpdate()
    {
        if (!_active || _view == null || _canvas == null) return;
        try
        {
            if (!Main.Settings.EnableRodCase)
            {
                (_view.GetViewModel() as ActionBarConvertedVM)?.Close();
                return;
            }
            if (_canvasSize != _canvas.rect.size) Resize();
            KeepOnScreen();
        }
        catch (Exception exception)
        {
            Restore();
            Main.ModEntry.Logger.Error($"Could not resize the Rod Box menu: {exception}");
        }
    }

    private void Resize()
    {
        if (_container == null || _canvas == null || _grid == null || _content == null) return;
        _canvasSize = _canvas.rect.size;
        var available = _canvasSize - Vector2.one * ScreenMargin * 2f;
        var columns = ColumnCount(_count, _grid.cellSize.x, _grid.spacing.x, _grid.padding.horizontal, available.x);
        var rows = 1 + (Math.Max(1, _count) - 1) / columns;
        _grid.constraintCount = columns;
        var width = _grid.padding.horizontal + columns * _grid.cellSize.x + (columns - 1) * _grid.spacing.x;
        var height = _grid.padding.vertical + rows * _grid.cellSize.y + (rows - 1) * _grid.spacing.y;
        _container.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        _container.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Min(height, Mathf.Max(_grid.cellSize.y, available.y)));
        _content.sizeDelta = new Vector2(width, height);
        _scroll!.vertical = height > _container.rect.height;
        _scroll.scrollSensitivity = _grid.cellSize.y + _grid.spacing.y;
        LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        KeepOnScreen();
    }

    private void KeepOnScreen()
    {
        if (_container == null || _canvas == null) return;
        // Measure the visible window, excluding clipped content below it.
        _container.GetWorldCorners(_corners);
        var minimum = _canvas.InverseTransformPoint(_corners[0]);
        var maximum = minimum;
        foreach (var corner in _corners)
        {
            var point = _canvas.InverseTransformPoint(corner);
            minimum = Vector3.Min(minimum, point);
            maximum = Vector3.Max(maximum, point);
        }
        var screen = _canvas.rect;
        var dx = Mathf.Max(0f, screen.xMin + ScreenMargin - minimum.x)
            + Mathf.Min(0f, screen.xMax - ScreenMargin - maximum.x);
        var dy = Mathf.Max(0f, screen.yMin + ScreenMargin - minimum.y)
            + Mathf.Min(0f, screen.yMax - ScreenMargin - maximum.y);
        _container.position += _canvas.TransformVector(new Vector3(dx, dy));
    }

    internal void Restore()
    {
        if (!_active) return;
        _active = false;
        if (_view != null && _container != null)
            foreach (var slot in _view.m_Slots)
                if (slot != null) slot.transform.SetParent(_container, false);
        if (_viewport != null) _viewport.gameObject.SetActive(false);
        if (_container != null)
        {
            _container.sizeDelta = _nativeSize;
            _container.anchoredPosition = _nativePosition;
        }
        if (_nativeGrid != null) _nativeGrid.enabled = _gridEnabled;
        if (_nativeFitter != null) _nativeFitter.enabled = _fitterEnabled;
        _view = null;
    }

    private void OnDestroy() => Restore();
}

[HarmonyPatch(typeof(ActionBarConvertedView), "BindViewImplementation")]
internal static class RodCasePopupOpenPatch
{
    private static void Postfix(ActionBarConvertedView __instance, RectTransform ___m_Container)
    {
        if (__instance is not ActionBarConvertedPCView view
            || !RodCasePopup.IsRodMenu(view.GetViewModel() as ActionBarConvertedVM)) return;
        var popup = view.GetComponent<RodCasePopup>() ?? view.gameObject.AddComponent<RodCasePopup>();
        try { popup.Open(view, ___m_Container); }
        catch (Exception exception)
        {
            popup.Restore();
            Main.ModEntry.Logger.Error($"Could not create the flexible Rod Box menu: {exception}");
        }
    }
}

[HarmonyPatch(typeof(ActionBarConvertedView), "DestroyViewImplementation")]
internal static class RodCasePopupClosePatch
{
    private static void Prefix(ActionBarConvertedView __instance) => __instance.GetComponent<RodCasePopup>()?.Restore();
}
