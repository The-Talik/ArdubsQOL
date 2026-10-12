using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker.ResourceLinks;
using Kingmaker.UnitLogic.ActivatableAbilities;
using Kingmaker.Visual.Particles;
using Kingmaker.Visual.Particles.FxSpawnSystem;
using Kingmaker.Visual.Sound;
using UnityEngine;

namespace ArdubsQOL;

internal static class SilentRangeIndicator
{
    private const string RangeIndicatorFxId = "79665f3d500fdf44083feccf4cbfc00a";

    private sealed class IndicatorState
    {
        internal IFxHandle? Handle;
    }

    private static readonly PrefabLink RangeIndicatorLink = new() { AssetId = RangeIndicatorFxId };
    private static readonly Dictionary<ActivatableAbility, IndicatorState> Indicators = new();

    private static GameObject? _silentPrefab;
    private static GameObject? _prefabHolder;
    private static bool _errorReported;

    internal static void Show(ActivatableAbility aura)
    {
        if (!Indicators.ContainsKey(aura))
        {
            Indicators.Add(aura, new IndicatorState());
        }

        EnsureSpawned(aura, Indicators[aura]);
    }

    internal static void Hide(ActivatableAbility aura)
    {
        if (!Indicators.TryGetValue(aura, out var state))
        {
            return;
        }

        Destroy(state.Handle);
        Indicators.Remove(aura);
    }

    internal static void Update()
    {
        foreach (var pair in Indicators.ToArray())
        {
            var aura = pair.Key;
            if (!aura.IsOn || !aura.IsStarted || !Main.Settings.EnableChantAura)
            {
                Hide(aura);
                continue;
            }

            if (pair.Value.Handle == null || pair.Value.Handle.IsDestroyed)
            {
                pair.Value.Handle = null;
                EnsureSpawned(aura, pair.Value);
            }
        }
    }

    private static void EnsureSpawned(ActivatableAbility aura, IndicatorState state)
    {
        if (state.Handle != null && !state.Handle.IsDestroyed)
        {
            return;
        }

        try
        {
            var view = aura.Owner.Unit.View;
            if (view == null)
            {
                return;
            }

            var prefab = GetSilentPrefab();
            state.Handle = FxHelper.SpawnFxOnUnit(prefab, view);
            _errorReported = false;
        }
        catch (Exception exception)
        {
            ReportError(exception);
        }
    }

    private static GameObject GetSilentPrefab()
    {
        if (_silentPrefab != null)
        {
            return _silentPrefab;
        }

        var source = RangeIndicatorLink.Load()
            ?? throw new InvalidOperationException("Could not load the aura range-indicator effect.");

        _prefabHolder = new GameObject("ArdubsQOL_SilentFxPrefabs")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        _prefabHolder.SetActive(false);
        UnityEngine.Object.DontDestroyOnLoad(_prefabHolder);

        // Clone below an inactive parent so none of the source prefab's OnEnable audio
        // can fire before the soundtrack-bearing components are removed.
        _silentPrefab = UnityEngine.Object.Instantiate(source, _prefabHolder.transform, false);
        _silentPrefab.name = "ArdubsQOL_SilentChantRange";
        _silentPrefab.hideFlags = HideFlags.HideAndDontSave;

        foreach (var sound in _silentPrefab.GetComponentsInChildren<SoundFx>(includeInactive: true))
        {
            UnityEngine.Object.DestroyImmediate(sound);
        }

        // The source is Inspire Competence's range effect. Its border and glow are a
        // useful, accurate range marker, but the separate Notes objects make Chant
        // and Cackle look like bard performances.
        foreach (var child in _silentPrefab.GetComponentsInChildren<Transform>(includeInactive: true)
                     .Where(transform => transform.name.StartsWith("Notes", StringComparison.OrdinalIgnoreCase))
                     .ToArray())
        {
            UnityEngine.Object.DestroyImmediate(child.gameObject);
        }

        // A runtime clone is not a stable pool key. Disabling pooling makes each aura
        // instance self-contained and lets FxHelper destroy it normally on deactivation.
        var pooledFx = _silentPrefab.GetComponent<PooledGameObject>();
        if (pooledFx != null)
        {
            pooledFx.enabled = false;
        }

        Main.LogDetail("Prepared a sound-free, note-free Chant/Cackle aura range indicator.");
        return _silentPrefab;
    }

    private static void Destroy(IFxHandle? handle)
    {
        if (handle != null && !handle.IsDestroyed)
        {
            FxHelper.Destroy(handle, immediate: true);
        }
    }

    private static void ReportError(Exception exception)
    {
        if (_errorReported)
        {
            return;
        }

        _errorReported = true;
        Main.ModEntry.Logger.Error(
            $"Could not display the sound-free Chant/Cackle aura range indicator; it will retry: {exception}");
    }
}
