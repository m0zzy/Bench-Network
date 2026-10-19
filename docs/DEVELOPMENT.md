# Development

Technical notes for working on BenchNetwork. Player-facing docs are in the [README](../README.md).

## Overview

- A Valheim mod built on BepInEx, Harmony and [Jotunn](https://github.com/Valheim-Modding/Jotunn). Jotunn
  syncs and locks the server's config and enforces the version check.
- Two stations are linked when `distance < rangeA + rangeB` (horizontal distance, height ignored). Links chain
  through union-find.
- A station's effective range is `m_rangeBuild + m_extraRangePerLevel × (level − 1)`.
- Each station type in a network counts at the level of its best-upgraded instance.
- Build and repair checks run on each client, so the mod is required on the server and every client
  (`CompatibilityLevel.EveryoneMustHaveMod`, `VersionStrictness.Minor`).

## Code layout

| File | Purpose |
| --- | --- |
| `BenchNetwork/Plugin.cs` | Plugin entry point, Jotunn attributes, patching, config-change handling |
| `BenchNetwork/ModConfig.cs` | Config entries and the parsed range / extra-stations view |
| `BenchNetwork/StationRanges.cs` | Applies configured ranges, keeps the game's values for restore, resizes circles |
| `BenchNetwork/StationNetworks.cs` | Network cache (union-find) and lookups |
| `BenchNetwork/Patches/` | Harmony patches |
| `BenchNetwork/Package/icon.png` | Thunderstore icon |
| `.github/workflows/build.yml` | CI build and release workflow |

## Config entries

Config file: `BepInEx/config/m0zzy.BenchNetwork.cfg`. Synced entries use Jotunn's `IsAdminOnly`.

| Section | Entry | Default | Synced | Notes |
| --- | --- | --- | --- | --- |
| General | Enabled | `true` | yes | Off = vanilla: original ranges, no linking. |
| General | Link building | `true` | yes | Build, remove and hammer-repair pieces through the network. |
| General | Link repair | `true` | yes | Repair items through the network at a station. |
| General | Extra stations | *(empty)* | yes | Modded stations that join networks; see below. |
| Ranges | *Station* range | `-1` | yes | Level 1 build range. `-1` keeps the game's value. |
| Ranges | *Station* range per level | `-1` | yes | Range added per upgrade level. `-1` keeps the game's value. |
| Client | Network refresh interval | `2` | no | Seconds between rebuilds while the hammer is out. |
| Client | Show network in hover text | `true` | no | Show or hide the network line. |

Station name tokens: `$piece_workbench`, `$piece_forge`, `$piece_stonecutter`, `$piece_artisanstation`,
`$piece_blackforge`, `$piece_magetable`. Extra stations use the format `token[:range[:rangePerLevel]]`.

## How it works

| Piece | Hook | What it does |
| --- | --- | --- |
| Range override | `CraftingStation.Start` postfix | Sets `m_rangeBuild` / `m_extraRangePerLevel` from the config and resizes the circle (`m_areaMarker`'s `CircleProjector`). The game's values are kept so turning the mod off restores them. Reapplied to all loaded stations when the config changes or syncs. |
| Network manager | `StationNetworks` | Union-find over `CraftingStation.m_allStations`, using each station's effective range (`m_rangeBuild + m_extraRangePerLevel × (level − 1)`). Caches each network's members, circles, types and best level per type. Rebuilds when a station starts or is destroyed, when an upgrade (`StationExtension`) appears or disappears (plus one more rebuild 2.5 s later, after the game re-counts levels), when the config changes, and every *refresh interval* while the hammer is equipped. |
| Build / remove | `CraftingStation.HaveBuildStationInRange` postfix | If the game finds no station, returns the best station of the required type from a network whose circles cover the point. `Player.HaveRequirements` (placing), `Player.CheckCanRemovePiece` and `Player.Repair` (hammer) all call this method. |
| Item repair | `InventoryGui.CanRepair` postfix | If the game says no: takes the player's current station, finds its network, and allows the repair when the network has the recipe's repair or crafting station at `m_minStationLevel` or above. `HaveRepairableItems` and `RepairOneItem` both call `CanRepair`. |
| Network highlight | `NetworkHighlight` | While `Player.InPlaceMode()`, activates `m_areaMarker` on every member of the network covering the player (cache only) and deactivates them when the player leaves it, changes network or puts the hammer away. Re-activates every frame because the game hides markers on its own timer. Doesn't call `ShowAreaMarker()`, which also marks the station as in use. The toggle key flips the `Show network while building` entry, so the choice is saved. |
| Hover text | `CraftingStation.GetHoverText` postfix | Appends the network line, read from the cache only. Hidden for stations that aren't linked to anything. |

Each patch class is applied on its own: if a game update renames a method, that one feature is turned off
and an error is logged, and the rest of the mod keeps working.

### Game members used

Confirm these in `assembly_valheim.dll` (dnSpy or ILSpy) after a game update. The mod compiles against the
publicized assembly, so private members are accessed directly.

- `CraftingStation`: `m_name`, `m_rangeBuild`, `m_extraRangePerLevel`, `m_areaMarker`, `m_allStations`
  (static), `Start()`, `OnDestroy()`, `GetLevel()`, `GetHoverText()`, `HaveBuildStationInRange(string, Vector3)`
  (returns `CraftingStation`)
- `StationExtension`: `Awake()`, `OnDestroy()`
- `CircleProjector`: `m_radius`, `m_nrOfSegments`
- `InventoryGui.CanRepair(ItemDrop.ItemData)`, and that `HaveRepairableItems` / `RepairOneItem` call it
- `Player.m_localPlayer`, `Player.GetCurrentCraftingStation()`, `Player.InPlaceMode()`, `Character.Message(MessageHud.MessageType, string)`
- `Console.IsVisible()`, `TextInput.IsVisible()`, `Menu.IsVisible()`, `Chat.instance.HasFocus()` (toggle key is ignored while typing)
- `Player.HaveRequirements`, `Player.CheckCanRemovePiece` and `Player.Repair` calling `HaveBuildStationInRange`
- `ObjectDB.GetRecipe(ItemDrop.ItemData)`, `Recipe.m_craftingStation`, `m_repairStation`, `m_minStationLevel`
- `ItemDrop.ItemData.SharedData.m_canBeReparied` (the game's spelling)

Untouched on purpose: the workbench's PlayerBase (spawn blocking) radius, upgrade placement rules, roof,
exposure and fire checks, and crafting recipes.

## Troubleshooting from logs

Everything goes to `BepInEx/LogOutput.log` at Info level or above, which BepInEx writes to disk by default.

- **Always logged:** `BenchNetwork <version> loaded: N of N patches applied.` (a warning plus an error per
  failed patch otherwise), the settings at start and whenever they change, and `Received settings from the
  server.` when Jotunn syncs them.
- **With `Debug logging` on:** each change in the set of networks (members with name token, level, range and
  map position), and each build or repair check the mod decided, logged when its outcome changes so per-frame
  checks don't flood the file.
- **`benchnetwork` console command:** prints the settings, every network and which one the player is in, to the
  console and the log.

Player-facing reporting steps are in the README, and `.github/ISSUE_TEMPLATE/bug_report.yml` asks for the log.

## Releases

GitHub Actions (`.github/workflows/build.yml`) builds the mod on every push. It downloads the free Valheim
dedicated server for the game assemblies and BepInExPack for BepInEx, so nothing has to be installed locally.
Each run keeps the zip as an artifact on the run's page.

To publish a release: **Actions → Build → Run workflow**, pick `main`, enter a version such as `0.1.0`, and
run it. The workflow builds with that version (plugin, `manifest.json` and zip all match) and creates the
GitHub release `v0.1.0` with the zip attached. The version must be higher than the last one; Jotunn's version
check compares major.minor, so bump the minor version (`0.2.0`) for changes everyone must update to.

A release runs three jobs: **build**, then **hexium** (upload to Hexium), then **release** (GitHub release).
The GitHub release is only created when the Hexium upload succeeded or was skipped, so a rejected upload can
be fixed and re-run with the same version.

### Publishing to Hexium

The **hexium** job uploads the same zip with the third-party action
[Klastic/publish-to-hexium](https://github.com/Klastic/publish-to-hexium), which uses Hexium's API at
`hexium.gg/api/experimental`. It runs in its own job with read-only repository access, so the action only
ever sees the Hexium token. Until it's set up, the job is skipped and releases go to GitHub only.

One-time setup:

1. On [hexium.gg](https://hexium.gg), sign in, create a team (this becomes the author name on the mod's page),
   and create an API token in the team settings.
2. In this repository on GitHub: **Settings → Secrets and variables → Actions**.
   - On the **Secrets** tab, add `HEXIUM_AUTH_TOKEN` with the token.
   - On the **Variables** tab, add `HEXIUM_TEAM` with the team name exactly as Hexium shows it.

From then on, every release made with **Run workflow** goes to Hexium as well. Untick **Also publish the
release to Hexium** to skip it for one release. Notes:

- Each version can be uploaded to Hexium only once.
- The upload is listed under Valheim in the categories `Mods` and `Client & Server`; change them in the
  `community-categories` line of the workflow. A category name Hexium doesn't know makes the upload fail.
- If Hexium accepts the upload but keeps it hidden (for example while it's reviewed), the run shows a warning.
- Hexium shows the package's `README.md` as the mod page. The build rewrites the README's repo-relative links
  to full `github.com` / `raw.githubusercontent.com` URLs on `main`, so the images and links work there as
  long as the repository is public.

## README diagrams

The diagrams are drawn as SVG in `docs/images/` and shown as PNG, which every mod site displays. After editing
an SVG, run `docs/images/render.sh` (needs Chromium and Python with Pillow) to re-make the PNGs, and commit
both.

## Building locally

Only needed if you want to build on your own machine instead of using the workflow.

1. Install the .NET SDK (6 or newer), plus BepInEx in your Valheim folder.
2. Copy `Environment.props.example` to `Environment.props` and set `VALHEIM_INSTALL` (Jotunn also tries the
   usual Steam paths if you skip this). Set `BEPINEX_PATH` to build against and deploy into an r2modman
   profile instead.
3. `dotnet build -c Release`

On the first build Jotunn's build task writes publicized copies of the game assemblies to
`Valheim_Data/Managed/publicized_assemblies`. When `BEPINEX_PATH/plugins` exists the DLL is copied to
`plugins/BenchNetwork/`. Every build also writes the Thunderstore zip next to the DLL.

The version comes from `<Version>` in `BenchNetwork/BenchNetwork.csproj` (or `-p:Version=1.2.3`) and is used
for the plugin, the manifest and the zip name.

## Test checklist

1. With **Enabled** off, everything behaves like vanilla.
2. Workbench overlapping a Forge: a sconce can be placed at the Workbench's far edge.
3. A three-station chain (A–B–C, where A and C don't overlap) works as one network.
4. Destroying the middle station splits the network.
5. Repairing a Forge item at the Workbench works only if the network's Forge level is high enough.
6. Ranges match on a dedicated server with two clients, and a client can't override the server's values.
7. A station's hover text lists every type in its network with the correct highest level, and updates after
   a station is added or removed.
8. A stress base with 50+ stations shows no frame hitches.
9. With the hammer out inside a network, every circle in that network is shown; they disappear when you walk
   out of the network or put the hammer away. F7 toggles this with a message, the choice survives a restart,
   and F7 does nothing while typing in chat or the console.
