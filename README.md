# Ardub's QOL

Ardub's QOL is a collection of quality-of-life options for
**Pathfinder: Wrath of the Righteous** fixing things that annoyed me.

## Features

- Chant and Cackle auras. Uses a move action in turn-based combat; caps the caster's speed at 5 feet in real time and outside combat. (Inspired from DarkCodex's version)
- A Rods submenu in Abilities that pools all metamagic rods.  (Inspired from The One Pearl)
- Shifter Aspect now retains the remaining minute buff when you turn it off.
- Shifter Aspects automatically turn off after combat without clearing Chimeric Aspect selections (default on).
- Automatic pausing after a save finishes loading.
- Faster outpost construction loading by deferring heavy asset cleanup until a normal area/save/menu load; uses more memory while managing outposts.
- Previous/next buttons and a settlement dropdown in the construction screen, without returning to the map between outposts.
- Infirmary-point ranges alongside HP and unit counts in crusade healing previews.
- Per-unit infirmary costs in army and battle tooltips, with casualty calculations and recoverable counts during battle.
- Left, right, or automatic screen-edge placement for quickbar hover tooltips.
- Allows shrinking the font of the unit hover infowindow.
- Caster names on  stacking debuffs, like Evil Eye in the hover infowindow.
- Duration remaining on effects in the hover infowindow
- Hover text, gold target outlines, and gold ground circles for Accursed Hex
  retries. (Requires Tabletop Tweaks)
- An optional rule (default to off) that removes one negative level per level-up.
- Optional direct control of monsters raised by Repurpose (default off; no ToyBox required).
- Optional Evil Eye stacking limit: only the strongest penalty per statistic applies, with separate caster durations (default off).

## Requirements

- Pathfinder: Wrath of the Righteous
- Unity Mod Manager

The Accursed Hex display is enabled only when a compatible Tabletop Tweaks Core
implementation is present. All other features work without Tabletop Tweaks.

## Installation

1. Install Unity Mod Manager for Pathfinder: Wrath of the Righteous
2. Install the mod using Unity Mod Manger, or Modfinder (I recommend this).
3. Start the game!

## Save compatibility and uninstalling

Most options change only runtime behavior and add no custom save data.

The Chant/Cackle Aura abilities and marker buffs, and the Rods menu ability,
are exceptions: their native ability facts and action-bar placements are saved
so quickbar placement survives loading. Saves containing these ability blueprints require Ardub's QOL.

To uninstalling the mod:

1. Turn off **Enable Chant Aura** and **Shared Rods Menu** in the mod settings.
2. Let the mod remove their abilities and quickbar entries.
3. Make a new save.
4. Exit the game and remove the `ArdubsQOL` folder.

## Compatibility notes

- DarkCodex is not required. If DarkCodex's separate passive Chant/Cackle
  feature is enabled, disable it while using this implementation so
  the two systems do not overlap.
- Tabletop Tweaks Core is optional. Only the Accursed Hex presentation uses it.
- Chant Aura and Cackle Aura turn off after combat by default. Disable **Stop Chant Auras After Combat** to keep them active.
- Turns can be delayed while Chant/Cackle Aura remains active, provided no other action has been taken; the resumed turn still reserves its move action.
- Rest and world-map travel interrupt Chant and Cackle auras.
- Ardub's QOL does not require Ardub's Enemy Intelligence, but you might as well give that one a try, too.  ;-)

## License

Ardub's QOL's original code and documentation are available under the
[MIT License](LICENSE), copyright 2026 The-Talik.
