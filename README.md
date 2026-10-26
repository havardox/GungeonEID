# External Item Descriptions

Walk up to a gun or item in Enter the Gungeon to see its details:

- **Name, quality, type and quote**, with the item's sprite.
- **Gun stats:** DPS, damage, fire delay, reload, magazine, max ammo, shot speed, range, force and spread.
- **What it does.**
- **Synergies**, with each partner item's sprite and name.

Data comes from the [Enter the Gungeon Wiki](https://enterthegungeon.wiki.gg/).

## Install

Requires BepInEx (BepInExPack_EtG). Import the zip as a local mod in Thunderstore Mod Manager or r2modman, or put `GungeonEID.dll` and `eid_data.json` in a folder under `BepInEx/plugins/`.

**F4** shows or hides the descriptions.

## Settings

In `BepInEx/config/gungeon.eid.externalitemdescriptions.cfg`:

| Setting | Default | |
|---|---|---|
| `Enabled` | `true` | Show descriptions. |
| `ToggleKey` | `F4` | Show/hide key. |
| `Scale` | `0` | Text and sprite size; 0 picks one for your resolution. |
| `MaxWidth` | `50` | Line length in characters. |
| `OwnedColor` | `#4DA6FF` | Color for things you have. |
| `UnownedSpriteOpacity` | `0.35` | Opacity of synergy partners you don't have. |
| `Outline` | `true` | Outline text and sprites. |
| `ShowQuote`, `ShowGunStats`, `ShowDescription`, `ShowSynergies`, `ShowSynergySprites` | `true` | Which parts to show. |

If you also use ItemTips, turn it off in its Mod Config menu to avoid two descriptions.

## Credits

Item data from the [Enter the Gungeon Wiki](https://enterthegungeon.wiki.gg/), CC BY-SA.
