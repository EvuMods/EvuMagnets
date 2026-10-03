# Development

## Layout

- `src/EvuMagnets.Core` holds the tier table, recipe text, carry check, and the rule for which magnet is active. It targets `netstandard2.0` and does not reference Unity.
- `src/EvuMagnets` is the BepInEx plugin (`net48`). It clones a vanilla trinket, registers forge recipes through Jotunn, and patches pickup.
- `Requirement.GetAmount` is postfixed so upgrade costs can be 10, 20, and 40 instead of a linear step.
- When `PullThroughAllWards` is off, vanilla auto-pickup still runs at 2 meters. A second pass pulls only the extra ring, and it skips items inside a ward the player cannot access. The pass requests ownership and moves a drop only after this client owns it.

## Prerequisites

- .NET SDK 8. `make` uses `~/.dotnet` when that install exists, including when `/usr/bin/dotnet` is only a runtime.
- `make`, `curl`, `unzip`, and `python3`
- Either a local Valheim install (`VALHEIM_INSTALL` pointing at the game directory) or SteamCMD, which `scripts/fetch-refs.sh` downloads for you

The script also downloads the BepInEx pack version in `deps/bepinex-pack.version` and the Jotunn version in `deps/jotunn.version`. Assemblies land in `.refs/`, which is gitignored. `BepInEx.AssemblyPublicizer.MSBuild` publicizes `assembly_valheim.dll` during the plugin build.

Reference assemblies come from the Valheim dedicated server (Steam app 896660). `VALHEIM_INSTALL` copies from a local client instead.

## Commands

```sh
make fetch-refs
make build
make test
make verify
make package
```

`make verify` is the gate: fetch references if needed, build, and test. `make package` writes `dist/EvuMagnets-<version>.zip` in the Hexium layout: `manifest.json`, `icon.png`, `README.md`, `CHANGELOG.md`, `EvuMagnets.dll`, and `EvuMagnets.Core.dll` all at the zip root. `icon.png` must be 256×256. `version_number` in the packaged manifest is taken from `version.txt`. Jotunn is a manifest dependency. BepInExPack is not; Hexium assumes it and strips that entry on upload.

The release workflow attaches that zip and the two raw DLLs to the GitHub release. Publishing that release also publishes the same version to Thunderstore. A release created from this workflow does not start other workflows by itself, because it uses `GITHUB_TOKEN`, so release-please dispatches the Thunderstore workflow with the new tag. A release published any other way starts that workflow directly. The manual Action remains: leave the tag empty to package the selected branch, or set a release tag such as `v0.2.1`. It publishes team `EvuMods` to the Valheim community with categories Mods, AI Generated, Gear, and the update slug from the `game_category` input (`deep-north-update` when a release starts it). NSFW is off. The service account token belongs in the `TCLI_AUTH_TOKEN` repository secret, not in the repo.

## Version

The release pull request is the only edit of `version.txt` and of `version_number` in `manifest.json`. release-please writes those, plus `CHANGELOG.md`, from the conventional commits since the last release. Merging that pull request tags `vX.Y.Z`. `make package` on that tag reads `version.txt`, so the zip is `EvuMagnets-X.Y.Z.zip` and the packaged manifest uses the same number. `Directory.Build.props` reads `version.txt` for the plugin version. A later hand edit of either file is overwritten on the next release.

## Game updates

`.valheim-buildid` is the dedicated-server build this tree was last verified against. The Valheim update workflow compares it with the public Steam build. A green `make verify` opens a pull request whose message is `fix: rebuild against Valheim <buildid>`. A failed build opens a GitHub issue and does not release.

A green compile means the referenced members and the core tests still hold. It does not play the game. Check a magnet in a client after a game update that you care about.

For that chain to publish on its own, the release-please workflow dispatches itself after merging a release pull request that contains only those rebuild commits. GitHub does not start a new workflow from `GITHUB_TOKEN` alone, so the dispatch is explicit. See `.github/workflows/`.

## In-game check

There is no automated playtest here. After an item or pickup change, confirm:

- Turning Enabled off leaves pickup at 2 meters. A crafted magnet stays in the inventory.
- Alt+V pauses that player's magnet and leaves vanilla auto-pickup on. The same pause is the Local Active checkbox. Neither one is sent to the server.
- An iron magnet at quality 1 pulls from 4 meters, and quality 4 pulls from 8 meters. The item slides in. It is not teleported.
- A stack that would put you over your carry weight stays on the ground.
- Two magnets cannot stay equipped. The bonus stays off if they do.
- With AzuExtendedPlayerInventory, a picked-up magnet lands in the Magnet slot.
- Inside your own ward, the extra range still works. Inside someone else's ward, it does not, until PullThroughAllWards is on.
- The forge recipes match the config, including the 10, 20, and 40 upgrade steps.
