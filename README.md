# Bench-Network

A Valheim mod (BepInEx + Jotunn) that links crafting stations into networks.

- Each crafting station type has its own build range, set in the config and synced from the server.
- Two stations are linked when their range circles overlap (`distance < rangeA + rangeB`). Links chain, so
  A–B–C form one network even if A and C don't overlap.
- Anywhere inside a network's circles you can **build, remove and hammer-repair** pieces as if every station
  type in the network were in range.
- At any station in a network you can **repair items** whose station type is in that network at the recipe's
  minimum level.
- Each station type counts at the level of its best-upgraded instance in the network.
- A station's hover text lists its network, e.g. `Network: Workbench 5, Forge 7, Stonecutter`.
- **Crafting is not shared.** Recipes still need the real station.

## Install

Install on the **server and every client**. Build and repair checks run on each client, so a server-only
install can't work, and Jotunn refuses to connect a client whose version differs from the server's
(major.minor).

Requirements: [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) and
[Jotunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/) 2.30.2 or newer.

### With Gale (or r2modman)

Building (see [Building](#building)) also writes a Thunderstore-format package to
`BenchNetwork/bin/Release/BenchNetwork-<version>.zip` (`manifest.json`, `icon.png`, `README.md`,
`BenchNetwork.dll`).

1. In Gale, open the Valheim profile you play on.
2. Use Gale's **Import → Local mod** and pick the zip. Gale reads the manifest and installs BepInExPack and
   Jotunn as dependencies if the profile doesn't have them.
3. Every player and the server need the same build. To give it to the other players, send them the zip to
   import the same way, or export the profile from Gale and share the code or file.

### Manually

Copy `BenchNetwork.dll` into `BepInEx/plugins/BenchNetwork/`.

## Configuration

File: `BepInEx/config/m0zzy.BenchNetwork.cfg`. Entries marked *synced* come from the server and are locked
for clients. Server admins can change them in game (e.g. with ConfigurationManager); everyone else gets the
server's values.

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

Range entries exist for the Workbench (`$piece_workbench`), Forge (`$piece_forge`), Stonecutter
(`$piece_stonecutter`), Artisan table (`$piece_artisanstation`), Black forge (`$piece_blackforge`) and
Galdr table (`$piece_magetable`).

**Extra stations** is a comma-separated list of station name tokens (the station's `m_name`). Each entry can
carry a range and a range per level: `$piece_mystation` keeps that station's own range,
`$piece_mystation:30:4` sets range 30 plus 4 per level. Only listed types (and the six above) join networks.

## How it works

| Piece | Hook | What it does |
| --- | --- | --- |
| Range override | `CraftingStation.Start` postfix | Sets `m_rangeBuild` / `m_extraRangePerLevel` from the config and resizes the circle (`m_areaMarker`'s `CircleProjector`). The game's values are kept so turning the mod off restores them. Reapplied to all loaded stations when the config changes or syncs. |
| Network manager | `StationNetworks` | Union-find over `CraftingStation.m_allStations`, using each station's effective range (`m_rangeBuild + m_extraRangePerLevel × (level − 1)`). Caches each network's members, circles, types and best level per type. Rebuilds when a station starts or is destroyed, when an upgrade (`StationExtension`) appears or disappears (plus one more rebuild 2.5 s later, after the game re-counts levels), when the config changes, and every *refresh interval* while the hammer is equipped. |
| Build / remove | `CraftingStation.HaveBuildStationInRange` postfix | If the game finds no station, returns the best station of the required type from a network whose circles cover the point. `Player.HaveRequirements` (placing), `Player.CheckCanRemovePiece` and `Player.Repair` (hammer) all call this method. |
| Item repair | `InventoryGui.CanRepair` postfix | If the game says no: takes the player's current station, finds its network, and allows the repair when the network has the recipe's repair or crafting station at `m_minStationLevel` or above. `HaveRepairableItems` and `RepairOneItem` both call `CanRepair`. |
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
- `Player.m_localPlayer`, `Player.GetCurrentCraftingStation()`, `Player.InPlaceMode()`
- `Player.HaveRequirements`, `Player.CheckCanRemovePiece` and `Player.Repair` calling `HaveBuildStationInRange`
- `ObjectDB.GetRecipe(ItemDrop.ItemData)`, `Recipe.m_craftingStation`, `m_repairStation`, `m_minStationLevel`
- `ItemDrop.ItemData.SharedData.m_canBeReparied` (the game's spelling)

Untouched on purpose: the workbench's PlayerBase (spawn blocking) radius, upgrade placement rules, roof,
exposure and fire checks, and crafting recipes.

## Known limitation

Only stations in loaded areas count. If part of a very large network is in an unloaded area, the network can
split until that area loads.

## Building

1. Install the .NET SDK (6 or newer), plus BepInEx in your Valheim folder.
2. Copy `Environment.props.example` to `Environment.props` and set `VALHEIM_INSTALL` (Jotunn also tries the
   usual Steam paths if you skip this). Set `BEPINEX_PATH` to build against and deploy into an r2modman
   profile instead.
3. `dotnet build -c Release`

On the first build Jotunn's build task writes publicized copies of the game assemblies to
`Valheim_Data/Managed/publicized_assemblies`. When `BEPINEX_PATH/plugins` exists the DLL is copied to
`plugins/BenchNetwork/`. Every build also writes the Thunderstore zip next to the DLL.

To release a new version, change `<Version>` in `BenchNetwork/BenchNetwork.csproj` (used for the zip and
`manifest.json`) and `PluginVersion` in `BenchNetwork/Plugin.cs` (used for Jotunn's version check) together.

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

## Phase 2 (not done)

- While holding the hammer, highlight every circle in the network the player is standing in.
