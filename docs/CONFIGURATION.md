# Configuration

Settings are written to `BepInEx/config/evu.evumagnets.cfg` the first time the mod loads. The mod has to be on the server. Jotunn sends the server's gameplay values to clients.

A configuration manager is optional. Synced settings show an S button there. An admin can press it to send that value to the server, and the server then sends it to the other players. Editing the server's file while it is running does the same. Players who are not on the server's admin list cannot change those rows while connected. The descriptions below are the same text shown in Configuration Manager.

## Local

These stay on the client. They have no S button.

| Setting | Default | Description |
| --- | --- | --- |
| Active | On | When off, your magnet does not extend pickup. Vanilla auto-pickup stays on. |
| Toggle | Left Alt + V | Key that flips Active. |
| MaxHits | 2048 | Most nearby colliders one magnet pass will search. The search starts at 128 and grows to this cap. Lower it if a large pull hitches. |

## General

| Setting | Default | Description |
| --- | --- | --- |
| Enabled | On | When off, magnets do not extend pickup range. Crafted magnets stay in the world. |
| PullThroughAllWards | Off | When on, magnets also pull items that sit inside other players' wards. Players can always pull items inside their own wards. |

## Forged magnets

Iron, Silver, Black metal, and Flametal each have the same keys.

| Setting | Description |
| --- | --- |
| Range1–Range4 | Pickup radius in meters at that quality. The floor is the vanilla 2 meters. |
| Craft | Ingredients for a new magnet. Item prefab names and amounts, separated by commas, such as `Iron:20,Thunderstone:1,Ectoplasm:5`. |
| Upgrades | Three upgrade steps, separated by semicolons. Each step is `Item:Amount` pairs separated by commas. A step may use items that are not in Craft; those rows are hidden at quality 1. |
| Station | Crafting station prefab name. Iron, silver, and black metal use `forge`. Flametal uses `blackforge`. |
| StationLevel | Station level required to make quality 1. Each upgrade asks for one level more. |

Default radii:

- Iron: 4, 5, 6, 8
- Silver: 8, 10, 12, 14
- Black metal: 14, 16, 18, 20
- Flametal: 20, 22, 24, 28

Default crafts:

- Iron, forge level 1: `Iron:20,Thunderstone:1,Ectoplasm:5`
- Silver, forge level 2: `Iron:10,Silver:10,Thunderstone:1,Obsidian:5`
- Black metal, forge level 3: `Iron:10,BlackMetal:10,Thunderstone:1,Crystal:10`
- Flametal, black forge level 1: `Iron:10,FlametalNew:10,Thunderstone:1,Eitr:5`

`FlametalNew` is the Ashlands bar from a blast furnace. The older prefab `Flametal` is Ancient Metal, which the game no longer produces. A config that still has that old default is rewritten to `FlametalNew` the next time it loads. A craft or upgrade line that was edited stays as written.

Default upgrades, for each tier's own bar:

`Bar:10,Thunderstone:1;Bar:20,Thunderstone:2;Bar:40,Thunderstone:4`

## Bloodgold magnet

Bloodgold has one key, `Range1`, default 36. It is not crafted or upgraded. The frost foundry produces it from a magnet cast.

## Magnet cast

| Setting | Default | Description |
| --- | --- | --- |
| Craft | `Iron:10,Thunderstone:1,Gold:10,FrostCore:5` | Ingredients for one cast. |
| Station | `blackforge` | Crafting station prefab name. |
| StationLevel | 1 | Station level required. |
| Foundry | `piece_FrostFoundry` | Station that turns a cast into a bloodgold magnet. Liquid frost is that station's fuel. Read when a world loads. |
| CookTime | 50 | Seconds for one cast. |
