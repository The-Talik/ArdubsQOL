# Ardub's QOL

Ardub's QOL is a collection of focused quality-of-life options for
**Pathfinder: Wrath of the Righteous**. It is a standalone Unity Mod Manager
mod: DarkCodex is not required, and Tabletop Tweaks is only used for one
optional display integration.

Current version: **0.9.2**

## Features

- Turn-based Chant and Cackle auras that preserve eligible hex durations while
  their original targets remain within 30 feet.
- Correct handling of the already-paid portion of Shifter Aspect's one-minute
  resource intervals.
- Automatic pausing after a save finishes loading.
- Left, right, or automatic screen-edge placement for quickbar hover tooltips.
- A configurable, fully scaled unit hover inspector that can display much more
  information.
- Caster names on ambiguous stacking debuffs, including Evil Eye.
- Compact remaining durations on timed hover-inspector effects.
- Hover text and gold target outlines for active Tabletop Tweaks Accursed Hex
  retries.
- An optional rule that removes one negative level after a normal class
  level-up.
- A shared Rods submenu that pools ordinary metamagic rod charges and lets
  multiple spellcasters use them without equipping the rods.

## Requirements

- Pathfinder: Wrath of the Righteous
- Unity Mod Manager

There are no required gameplay-mod dependencies.

The Accursed Hex display is enabled only when a compatible Tabletop Tweaks Core
implementation is present. All other features work without Tabletop Tweaks.

## Installation

1. Install Unity Mod Manager for Pathfinder: Wrath of the Righteous.
2. Extract the release into the game's `Mods` directory.
3. Confirm that the resulting layout is:

   ```text
   Pathfinder Second Adventure/
   `-- Mods/
       `-- ArdubsQOL/
           |-- ArdubsQOL.dll
           `-- Info.json
   ```

4. Fully restart the game. Unity Mod Manager loads mod assemblies only during
   startup.

To update, replace `ArdubsQOL.dll` and `Info.json`. Keep `Settings.xml` if you
want to retain your preferences.

If upgrading from version 0.7.3 or earlier, move `Settings.xml` from the legacy
pre-0.8 mod folder into `Mods/ArdubsQOL`, then remove the legacy folder. Do not
leave both copies installed.

## Options and defaults

| Option | Default | Effect |
| --- | --- | --- |
| Enable Chant Aura | On | Adds the Chant Aura and Cackle Aura abilities. |
| Preserve Shifter Aspect Time | On | Lets an already-paid one-minute Shifter Aspect interval expire naturally. |
| Pause at Savegame Load | On | Pauses after a loaded save and its destination area are ready. |
| Side Quickbar Tooltips | On, Auto | Moves quickbar hover tooltips to the far left or right; Auto chooses the side opposite the active character. Disable to restore normal placement. |
| Shared Rods Menu | On | Under **Gameplay Changes**; lets spellcasters use pooled metamagic rod charges from the shared inventory without equipping rods. |
| Remove Negative Level on Level Up | Off | Under **Gameplay Changes**; removes one negative level after a completed normal class level-up. |
| Hover Inspector Scale | 100% | Scales the entire hover inspector from 50% to 100%. |
| Show Stackable Debuff Casters | On | Identifies casters on Evil Eye and ambiguous duplicate hostile debuffs. |
| Show Accursed Hex Retries | On | Shows retry text and gold target outlines when Tabletop Tweaks supplies the retry state. |
| Detailed Diagnostics | Off | Writes detailed aura, shared rod charge, and negative-level recovery diagnostics to the Unity Mod Manager log. |

## Chant and Cackle auras

Characters with the base Shaman Chant or Witch Cackle feature receive a
matching aura toggle. While it is active in turn-based combat, eligible hex
timers stop counting down on targets within 30 feet of the original caster.
Leaving the aura or switching it off resumes each timer from its preserved
duration; the aura does not add duration or restore an expired effect.

The aura uses the following action rules:

- Turning it on during the caster's turn immediately reserves the move action.
- Starting a turn with it active reserves that turn's move action.
- Turning it off returns only the move action reserved by the aura.
- It cannot be activated after the character has already spent a move action
  that turn.
- Manual Chant or Cackle remains usable while the aura is active and uses the
  standard action instead.
- The aura switches off when combat ends.

The aura is intentionally unavailable in real-time combat and outside combat.
It cannot preserve effects during travel, rest, or ordinary exploration.

Aura abilities appear immediately after their matching base abilities. Their
bright gold frames distinguish them from manual Chant and Cackle. Switching an
aura on plays the matching native Chant or Cackle sound and displays a silent,
note-free 30-foot ground-range indicator.

The aura uses the same footprint-aware range and line-of-sight test as the base
Chant and Cackle abilities. A currently suspended inspector entry is marked
`(Chant)` or `(Cackle)`; the marker disappears when its timer resumes.

### Eligible hexes

Chant Aura preserves these Shaman hex buffs:

- Evil Eye: Armor Class, attack, and saving throws
- Fortune
- Fury
- Misfortune
- Protective Luck

Cackle Aura preserves these Witch hex buffs:

- Evil Eye: Armor Class, attack, and saving throws
- Fortune
- Misfortune
- Protective Luck

These lists match the buffs extended by the base Chant and Cackle abilities.
Only effects created by the aura's caster qualify. Cooldown buffs, secondary
effects, unrelated buffs, summons, and other timers are unchanged.

## Preserve Shifter Aspect Time

The base Shifter Aspect toggle spends its resource in one-minute intervals but
normally removes its buff immediately when switched off. With this option
enabled, the current paid interval remains active until its existing timer
expires. Further resource spending stops immediately.

Activating a different primary Shifter Aspect removes a lingering aspect first,
so rapidly cycling aspects cannot stack their benefits. The option applies only
to primary abilities in the base game's `ShifterAspect` group that spend their
resource once per minute. It does not affect Wild Shape, Drovier Animal Aspect,
secondary Chimeric Aspect toggles, or unrelated abilities.

## Pause at Savegame Load

This option applies the game's normal pause state after the save and destination
area finish loading. It is intended for long loads where the player has switched
to another application, preventing game time and effect timers from advancing in
the background.

## Quickbar tooltip placement

Under **QOL**, Side Quickbar Tooltips moves quickbar hover descriptions to the
screen edge and centers them vertically. Choose **Left**, **Right**, or **Auto**.
Auto uses the screen position of the character whose quickbar you are hovering:
left of center opens on the right, and right of center opens on the left. A
centered character or unavailable camera position uses the right side.

The side is chosen when the tooltip appears, not while the character moves.
This also applies to quickbar ability, spell, and item submenus. Inventory
tooltips, unit inspectors, and click-opened information windows are unchanged.
The option can be disabled and adds no save data.

## Hover inspector improvements

The Hover Inspector Scale slider scales the complete unit hover inspector,
including text, icons, padding, gaps, borders, and scroll elements. Ordinary
tooltips are not changed.

Show Stackable Debuff Casters adds the source character in parentheses to Witch
and Shaman Evil Eye entries. It also annotates other simultaneously visible,
same-named hostile debuffs when their source would otherwise be ambiguous.

Timed effects in the hover inspector include the game's compact remaining
duration immediately after the effect name, such as `Evil Eye — Saving Throws
6s (Camellia) (Chant)`. An effect that remains mechanically attached at the turn
boundary is shown as `0s` instead of being silently omitted.

When Tabletop Tweaks Core provides an active Accursed Hex retry, Show Accursed
Hex Retries adds a line such as `Accursed Hex retry: Slumber (Ember).` to the
target's hover inspector. Selecting that exact hex outlines its visible retry
targets in gold. This integration only reads Tabletop Tweaks' existing temporary
combat state; it adds no dependency and stores no state of its own. Retry cues
are limited to hexes with Tabletop Tweaks' once-per-target restriction and are
not shown for repeatable hexes.

## Remove Negative Level on Level Up

When enabled, this removes one negative level after the main character, a
companion, or a mercenary completes a normal class level-up. It prefers a
permanent negative level and otherwise removes a temporary one.

Character creation, respec rebuilding, mythic ranks, pets, summons, cancelled
level-ups, and save loading do not trigger recovery.

## Shared Rods menu

Characters with a spellbook containing a spell list receive a **Rods** ability.
Drag it from the abilities menu to a quickbar slot. Clicking it opens the game's
normal expandable submenu, with one entry per ordinary metamagic rod type in
the identified shared party inventory. Each entry shows its pooled remaining
charges and indicates whether that type is selected for the current character.

For example, two lesser Extend rods with three charges each appear as a single
lesser Extend entry with six charges. A regular Extend rod remains a separate
entry with three charges. All characters draw from the same pools; selecting a
type for one character does not reserve or lock any physical rod. Multiple
casters can select and use the same type.

Each character can select one rod type at a time. Click the selected entry again
to turn it off. The normal metamagic eligibility and spell-level limits apply,
including Quicken's swift-action behavior. Casting an eligible spell spends one
charge from a real rod in the pool. Previews, cancelled casts, duplicate spell
effects, and delivery of an already-cast touch spell do not spend extra charges.
Once the pool is exhausted, its selection turns off for every character using it.

Only ordinary lesser, regular, and greater Bolster, Empower, Extend, Maximize,
Persistent, Quicken, Reach, Selective, Intensified, and Piercing rods are included.
Unique rods, enhanced extra-charge variants, and kinetic rods are excluded.
Rods in storage or unidentified rods are excluded. Equipped ordinary rods still
contribute their charges to the shared inventory pool. Selecting a case entry
turns off that caster's equipped metamagic rod; activating an equipped rod
turns off that caster's case selection.

Selections turn off when a save loads and when the party rests. The **Rods**
button and its quickbar placement persist, while individual submenu entries and
selections are runtime-only. Charges remain on the original inventory items,
so the game's normal saving and rest replenishment handle them. This feature
works in turn-based combat, real-time combat, and exploration, and requires
neither Tabletop Tweaks nor DarkCodex.

## Save compatibility and uninstalling

Most options change only runtime behavior and add no custom save data.

The Chant/Cackle Aura abilities and marker buffs, and the **Rods** menu ability,
are exceptions: their native ability facts and action-bar placements are saved
so quickbar placement survives loading. Rod selections and pools add no custom
saved state. Saves containing these ability blueprints require Ardub's QOL.

To prepare a campaign for uninstalling the mod:

1. Turn off **Enable Chant Aura** and **Shared Rods Menu** in the mod settings.
2. Let the mod remove their abilities and quickbar entries.
3. Make a new save.
4. Exit the game and remove the `ArdubsQOL` folder.

Older saves made before aura persistence was introduced remain compatible. If a
returning aura is not on the quickbar, place it once and save again.

Version 0.8.2 changes the assembly and source-project identity only. The saved
aura blueprint IDs are unchanged, so existing Ardub's QOL saves remain
compatible.

## Compatibility notes

- DarkCodex is not required. If DarkCodex's separate passive Chant/Cackle
  feature is enabled, disable it while using or testing this implementation so
  the two systems do not overlap.
- Tabletop Tweaks Core is optional. Only the Accursed Hex presentation uses it.
- Chant Aura and Cackle Aura are supported only in turn-based combat.
- Ardub's QOL does not require Ardub's Enemy Intelligence.

## Troubleshooting

### Unity Mod Manager shows an old version

Fully exit and restart the game. Check that only `Mods/ArdubsQOL` exists and
that no legacy copy remains.

### The aura ability is missing

Confirm that Enable Chant Aura is checked, the character has the base Chant or
Cackle feature, and the game is in turn-based combat.

### Accursed Hex cues are missing

The caster must have Tabletop Tweaks' Accursed Hex implementation, the target
must have passed the first saving throw, the retry window must still be active,
and the exact matching hex must be selected for the gold outline.

For bug reports, include the mod version, reproduction steps, relevant settings,
and the Unity Mod Manager section of `Player.log`.

## License

Ardub's QOL's original code and documentation are available under the
[MIT License](LICENSE), copyright 2026 The-Talik.

This license does not grant rights to Pathfinder: Wrath of the Righteous,
Owlcat Games assets or trademarks, Unity Mod Manager, Tabletop Tweaks,
DarkCodex, or other third-party material. Those remain subject to their
respective owners and licenses.

## Building from source

```powershell
dotnet build .\ArdubsQOL\ArdubsQOL.csproj -c Release -p:WrathInstallDir="C:\Program Files (x86)\Steam\steamapps\common\Pathfinder Second Adventure"
dotnet run --project .\tests\ArdubsQOLPolicyTests.csproj -c Release
```

The shared-rod smoke check binds its Harmony patches against the installed game
in an isolated .NET Framework process and checks the ordinary rod catalog:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\RodCaseGameSmoke.ps1 -ManagedPath "C:\Program Files (x86)\Steam\steamapps\common\Pathfinder Second Adventure\Wrath_Data\Managed" -ModPath .\ArdubsQOL\bin\Release\ArdubsQOL.dll -BlueprintZipPath "C:\Program Files (x86)\Steam\steamapps\common\Pathfinder Second Adventure\blueprints.zip"
```

The release output is `ArdubsQOL/bin/Release/ArdubsQOL.dll` plus
`ArdubsQOL/Info.json`.

`tests/QuickbarTooltipGameSmoke.ps1` checks tooltip tracking hooks against the
installed game assemblies and placement settings against an existing settings
file. The positioning target is resolved, but its Unity-native calls prevent
binding/rendering it outside the game; visual placement still needs an in-game
check. Screen-edge calculations are covered by the policy tests.
