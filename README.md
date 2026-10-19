<p align="center"><img src="BenchNetwork/Package/icon.png" width="112" alt=""></p>

<h1 align="center">BenchNetwork</h1>

<p align="center">
  Link your crafting stations together in Valheim.<br>
  Build anywhere in your base without putting a spare forge in every corner.
</p>

<p align="center"><a href="https://github.com/m0zzy/Bench-Network/releases/latest"><b>Download the latest release</b></a></p>

---

## What it does

Normally every crafting station has a circle around it, and you can only build a piece while you stand inside
the circle of the station that piece needs. Big bases end up with extra forges and stonecutters dotted around
just to keep building.

With BenchNetwork, stations work together. When two stations' circles touch, they **link**. Linked stations
form a **network**, and a network can grow as big as you like: each station only has to touch one other
station in it.

![Three stations in a chain form one network; a station on its own forms its own network](docs/images/linking.png)

### Build anywhere in the network

Stand inside any circle of a network and you can build as if every station in that network were right next to
you. The same goes for removing pieces and repairing them with the hammer.

![Without the mod you need to stand in the forge's circle; with it, the workbench's circle counts too](docs/images/building.png)

### Repair at any station

At any station, you can repair items that belong to another station in the same network. Each station type
counts at its **best upgrade level** in the network, so a level 7 forge anywhere in the network lets you repair
forge items at the workbench.

![At the workbench you can repair workbench and forge items, but forge recipes still need the forge](docs/images/repair.png)

### See what's in a network

Look at any station to see its network and the best level of each station type in it:

![Hover text reading: Network: Workbench 5, Forge 7, Stonecutter](docs/images/hover.png)

The line appears only when the station is linked to at least one other station.

## What's shared and what isn't

| | Shared through the network |
| --- | :---: |
| Building pieces | ✅ |
| Removing pieces | ✅ |
| Repairing pieces with the hammer | ✅ |
| Repairing items at a station | ✅ if the network's station is upgraded enough |
| Crafting | ❌ go to the real station |
| The area around your base where enemies can't spawn | ❌ unchanged |
| Upgrade placement, roof and fire requirements | ❌ unchanged |

## Installing

### With Gale

1. Download `BenchNetwork-<version>.zip` from the
   [latest release](https://github.com/m0zzy/Bench-Network/releases/latest). Don't unzip it.
2. In Gale, open your Valheim profile and choose **Import → Local mod**, then pick the zip.
   Gale adds BepInEx and Jotunn for you if you don't have them.
3. Start Valheim from Gale.

r2modman works the same way through its **Import local mod** option.

<details>
<summary>Installing by hand</summary>

1. Install [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) and
   [Jotunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/).
2. Unzip the release and copy `BenchNetwork.dll` into `BepInEx/plugins/BenchNetwork/` in your Valheim folder.

</details>

### Playing with friends

- **Everyone needs it**, including the server, and everyone needs the **same version**. If versions don't
  match, the game tells you when you join and won't connect. Update everyone to the same release.
- **Dedicated servers** need BepInEx, Jotunn and BenchNetwork installed, the same as players.
- **Single player** just works.

## Settings

Most people don't need to change anything. Out of the box everything is on and every station keeps its normal
range.

To change settings, open the in-game config menu (F1, with
[ConfigurationManager](https://thunderstore.io/c/valheim/p/Azumatt/Official_BepInEx_ConfigurationManager/)
installed), or edit `BepInEx/config/m0zzy.BenchNetwork.cfg`.

**On a server, the server's settings win.** Everything except the last two rows below comes from the server
and is locked for players. Only server admins can change it.

| Setting | Default | What it does |
| --- | --- | --- |
| Enabled | On | Turns the whole mod off. Everything goes back to the normal game. |
| Link building | On | Build, remove and hammer-repair anywhere in a network. |
| Link repair | On | Repair items from other stations in the network. |
| *Station* range | `-1` | How far the station's circle reaches. `-1` keeps the normal range. Bigger circles link more easily. |
| *Station* range per level | `-1` | Extra reach for each upgrade. `-1` keeps the normal amount. |
| Extra stations | Empty | Stations from other mods that should join networks. See below. |
| Show network in hover text | On | Shows the network line when you look at a station. |
| Network refresh interval | `2` | How often, in seconds, networks are re-checked while you hold the hammer. Best left alone. |

There are range settings for the **Workbench, Forge, Stonecutter, Artisan table, Black forge** and
**Galdr table**. When you change a range, the circle you see around the station grows or shrinks to match.

<details>
<summary>Adding stations from other mods</summary>

Add each station's internal name, separated by commas, for example `$piece_mystation`. To give it its own
range, add the range and the range per level after colons: `$piece_mystation:30:4` means a 30 m circle that
grows 4 m per upgrade. The internal name comes from that mod; its author or its files will have it.

</details>

## Good to know

- **Only nearby stations count.** The game only loads the world around players. If a network stretches far
  away, the far part may not count until someone is near it.
- **Safe to remove.** The mod saves nothing into your world. Pieces you built stay where they are. You just
  need to be near the right stations again to build more.
- **Can't join a server?** Check that you have the same BenchNetwork version as the server.

## Problems or ideas?

[Open an issue](https://github.com/m0zzy/Bench-Network/issues). For problems, include what you did, what
happened, and your `BepInEx/LogOutput.log` file (it is inside your Gale profile folder).

## For developers

How the mod works, how to build it and how releases are made: [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md).

## License

BenchNetwork is free and open source under the [MIT License](LICENSE): you can use, change and share it,
including in modpacks, as long as the license notice stays with it.

BenchNetwork is a fan-made mod and isn't affiliated with or endorsed by Iron Gate or Coffee Stain. Valheim is
a trademark of its owners.
