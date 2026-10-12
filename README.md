# Ardub's QOL

Ardub's QOL is a collection of focused quality-of-life options for
**Pathfinder: Wrath of the Righteous** fixing things that annoyed me.

## Features

- Chant and Cackle auras (Turn-based only).
- A Rods submenu in Abilities that pools all metamagic rods.  (Inspired from The One Pearl)
- Shifter Aspect now retains the remaining minute buff when you turn it off.
- Automatic pausing after a save finishes loading.
- Left, right, or automatic screen-edge placement for quickbar hover tooltips.
- Allows shrinking the font of the unit hover infowindow.
- Caster names on  stacking debuffs, like Evil Eye in the hover infowindow.
- Duration remaining on effects in the hover infowindow
- Hover text and gold target outlines for Accursed Hex
  retries. (Requires Tabletop Tweaks)
- An optional rule (default to off) that removes one negative level after a level-up.

## Requirements

- Pathfinder: Wrath of the Righteous
- Unity Mod Manager

The Accursed Hex display is enabled only when a compatible Tabletop Tweaks Core
implementation is present. All other features work without Tabletop Tweaks.

## Installation

1. Install Unity Mod Manager for Pathfinder: Wrath of the Righteous
2. Install the mod using Unity Mod Manger, or Modfinder(I recommend this).
3. Start the game!

## Save compatibility and uninstalling

Most options change only runtime behavior and add no custom save data.

The Chant/Cackle Aura abilities and marker buffs, and the Rods menu ability,
are exceptions: their native ability facts and action-bar placements are saved
so quickbar placement survives loading. Rod selections and pools add no custom
saved state. Saves containing these ability blueprints require Ardub's QOL.

To prepare a campaign for uninstalling the mod:

1. Turn off **Enable Chant Aura** and **Shared Rods Menu** in the mod settings.
2. Let the mod remove their abilities and quickbar entries.
3. Make a new save.
4. Exit the game and remove the `ArdubsQOL` folder.

## Compatibility notes

- DarkCodex is not required. If DarkCodex's separate passive Chant/Cackle
  feature is enabled, disable it while using or testing this implementation so
  the two systems do not overlap.
- Tabletop Tweaks Core is optional. Only the Accursed Hex presentation uses it.
- Chant Aura and Cackle Aura are supported only in turn-based combat.
- Ardub's QOL does not require Ardub's Enemy Intelligence, but you might as well give that one a try, too.  ;-)

## License

Ardub's QOL's original code and documentation are available under the
[MIT License](LICENSE), copyright 2026 The-Talik.

This license does not grant rights to Pathfinder: Wrath of the Righteous,
Owlcat Games assets or trademarks, Unity Mod Manager, Tabletop Tweaks,
DarkCodex, or other third-party material. Those remain subject to their
respective owners and licenses.