# EvuMagnets

Craftable magnets that pull items on the ground in from farther away. The pull is still Valheim's own: items slide toward you, and a drop is left behind when picking it up would put you over your carry weight.

## Features

- Five trinket magnets: iron, silver, black metal, flametal, and bloodgold. The first four reach farther at each forge upgrade. Bloodgold is a single strength, cast in the frost foundry.
- With no magnet equipped, pickup stays at the vanilla 2 meters.
- Only one magnet works at a time. Equipping another unequips the first.
- With AzuExtendedPlayerInventory installed, magnets prefer a Magnet slot over the trinket slot.
- Players can always pull items inside their own wards. `PullThroughAllWards` is what also reaches other players' wards.
- A global toggle turns the bonus off without deleting crafted magnets. Alt+V pauses your own magnet and leaves vanilla auto-pickup on.

## Installation

Install [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) and [Jotunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/). The mod has to be installed on the server and on every client. Copy `EvuMagnets.dll` and `EvuMagnets.Core.dll` into `BepInEx/plugins`. Both files have to sit in the same folder.

Start Valheim once. Settings are written to `BepInEx/config/evu.evumagnets.cfg`. The server copy of that file is what players use. An admin can change a synced setting in Configuration Manager, or edit the file while the server is running, and the new value reaches the other players. Alt+V and the local Active checkbox stay on each client.

## Recipes

The first four magnets are crafted at a forge. Flametal uses the black forge. Each starts with 10 iron, 10 bars of its own metal (20 iron on the iron magnet), one thunder stone, and one extra item. Upgrades cost 10, then 20, then 40 bars of that metal, plus 1, then 2, then 4 thunder stones.

Bloodgold has no forge recipe and no upgrades. Craft a magnet cast at the black forge, then set the cast in the frost foundry with liquid frost as fuel. The foundry returns one bloodgold magnet.

| Item | Station | Craft |
| --- | --- | --- |
| Iron magnet | Forge 1 | 20 Iron, 1 Thunderstone, 5 Ectoplasm |
| Silver magnet | Forge 2 | 10 Iron, 10 Silver, 1 Thunderstone, 5 Obsidian |
| Black metal magnet | Forge 3 | 10 Iron, 10 Black metal, 1 Thunderstone, 10 Crystal |
| Flametal magnet | Black forge 1 | 10 Iron, 10 Flametal, 1 Thunderstone, 5 Refined eitr |
| Magnet cast | Black forge 1 | 10 Iron, 1 Thunderstone, 10 Bloodgold, 5 Frostcore |
| Bloodgold magnet | Frost foundry | Magnet cast, with liquid frost as fuel |

Default pickup radius in meters, from quality 1 to 4:

- Iron: 4, 5, 6, 8
- Silver: 8, 10, 12, 14
- Black metal: 14, 16, 18, 20
- Flametal: 20, 22, 24, 28
- Bloodgold: 36

The full list is in [docs/CONFIGURATION.md](docs/CONFIGURATION.md).

## Build

See [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md). `make verify` builds the plugin and runs the tests.

## License

[MIT](LICENSE)
