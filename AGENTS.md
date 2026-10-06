# Agent Instructions

These instructions apply to the EvuMagnets repository.

## Read Order

1. Read [README.md](README.md) for what the mod does.
2. Read [ROADMAP.md](ROADMAP.md) before adding a feature that is listed as later work.
3. Read [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) for the toolchain, project split, and verification.
4. Read [docs/CONFIGURATION.md](docs/CONFIGURATION.md) before changing settings, defaults, or recipes.

## Product Boundary

EvuMagnets adds five trinket magnets. Equipping one raises the local player's auto-pickup radius. The pull, the ownership handoff, and the carry check stay on Valheim's pickup path. A global Enabled setting, on by default, leaves that radius at 2 meters when off.

The mod loads on a dedicated server so the items exist, and on every client. Gameplay settings sync from that server. The local magnet pause does not. Jotunn is a hard dependency. AzuExtendedPlayerInventory is a soft dependency: when it is present, magnets equip into a Magnet slot ahead of the trinket slot.

## Project Split

- `src/EvuMagnets.Core` is `netstandard2.0` and has no Unity types. Ranges, recipes, carry checks, and the single-magnet rule live here so they can be tested without the game.
- `src/EvuMagnets` is the `net48` BepInEx plugin. It registers the items, reads config, and patches pickup.
- `tests/EvuMagnets.Core.Tests` covers the core. In-game checks are manual.

## Verification

Run `make verify` after code changes. That fetches reference assemblies when needed, builds the solution, and runs the tests.

In-game installs are `make install`, which reads `GALE_PROFILE` from gitignored `.local.mk`.

Do not commit `.refs/`, `bin/`, `obj/`, or `dist/`. Valheim, BepInEx, and Jotunn binaries stay out of git.

## Commits

Use [Conventional Commits](CONTRIBUTING.md). release-please opens release pull requests from those messages. A `fix: rebuild against Valheim <buildid>` commit is reserved for the game-update workflow.
