# Bitshifter

An [Oxygen Not Included](https://www.klei.com/games/oxygen-not-included) mod that lets the **Automation Ribbon Reader** and **Ribbon Writer** select any of bits 1–31 instead of 1–4. Nothing else changes.

## Why

A ribbon's signal is a 32-bit integer; the game only exposes the low four bits. The Reader and Writer already work by shifting the whole value (`value >> bit` and `value << bit`), with no upper bound on the bit, so letting their side screen offer more bits is enough to put up to 31 independent signals on one ribbon. Bit 32 is the sign bit, and the game tests bits with `> 0`, so it would never read as on; it is left out.

## What it does

- The bit selector side screen of both buildings lists Bit 1 … Bit 31. The panel is a fixed height, so the list sits in a scroll view one and a half times the height the four vanilla rows had, with a scrollbar; scroll it with the wheel or the bar.
- A Reader set to a high bit outputs that bit on a 1-bit wire, or the value shifted down by that many bits on a ribbon, exactly as it does for bits 1–4. A Writer shifts its input up by the selected bit.
- The buildings' animations only have a "selected bit" highlight for bits 1–4; with a higher bit selected the building shows its plain idle pose, with the bit and port lights still lit correctly. The selection is shown in the side screen.
- Copy-settings works as before; the selected bit is saved with the building. If the mod is removed, a building set to a bit above 4 keeps working, but the vanilla side screen will not show its selection until you pick a bit again.

## Compatibility

The mod stands down (and says so in the log) when something else already handles this:

- when [Digital CPU](https://steamcommunity.com/sharedfiles/filedetails/?id=3244925649) (ONICPU) is enabled, since it already expands the ribbons, Reader, and Writer to 32 bits;
- when any other mod has already Harmony-patched `GetBitDepth` or `OnSpawn` on `LogicRibbonReader` or `LogicRibbonWriter` by the time all mods are loaded (Digital CPU, for instance, sets the bit depth from an `OnSpawn` postfix).

So whichever mod widens the reader and writer, this one yields to it rather than doubling up.

## Installing

As a local mod:

1. Download `Bitshifter-<version>.zip` from the [latest release](https://github.com/isochronous/bitshifter/releases/latest).
2. Extract it into a new folder named `Bitshifter` inside the game's local mods folder, so that `mod.yaml` ends up directly inside it (create `local` if it does not exist):
   - Windows: `Documents\Klei\OxygenNotIncluded\mods\local\Bitshifter`
   - Linux: `~/.config/unity3d/Klei/Oxygen Not Included/mods/local/Bitshifter`
   - macOS: `~/Library/Application Support/unity.Klei.Oxygen Not Included/mods/local/Bitshifter`
3. Start the game, enable the mod under **Mods** in the main menu, and let the game restart.

## Building

Requires the .NET SDK (8+). Shared build configuration lives in the [oni-mods-common](https://github.com/isochronous/oni-mods-common) submodule, so clone with `--recurse-submodules` (or run `git submodule update --init`). The game DLLs are referenced directly from the game install; override the path if yours differs:

```
dotnet build src/Bitshifter -c Release -p:GameFolder="<path-to>\OxygenNotIncluded"
```

A successful build deploys the mod to `Documents\Klei\OxygenNotIncluded\mods\local\Bitshifter` (disable with `-p:ModDeployFolder=none`).

## Implementation notes

Five Harmony patches, applied in `OnAllModsLoaded` (after every other mod has loaded, so the compatibility check sees their patches): `GetBitDepth` on `LogicRibbonReader` and `LogicRibbonWriter` returns 31; `UpdateVisuals` on both plays `idle` when the selected bit is above 4; and `LogicBitSelectorSideScreen.SetTarget` moves the rows' container into the game's own `KScrollRect` (with `RectMask2D` and a `ContentSizeFitter`) the first time the screen is shown, 1.5x the row list's original height, with a scrollbar cloned from the first vanilla side screen that has one (a flat-colour bar is the fallback). No PLib, no options, no strings.
