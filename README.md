# Valley Exchange

A Stardew Valley SMAPI mod by Rachman Nurhakim.

Version: 1.34.0

## Features

- A stock market for farmers.
- Oil wells and refining.
- A hired crew for farming, ranching, fishing, mining, crafting, deliveries, banking, research, and more.
- A Guild Master with an endless dungeon.
- Optional integration with ValleyTalk (`dandm1.ValleyTalk`).

## Requirements

- Stardew Valley.
- SMAPI 4.0.0 or newer, as specified by the mod manifest.
- .NET 6 SDK to build the source.

## Build

The project uses `Pathoschild.Stardew.ModBuildConfig` to resolve game assemblies and package the mod.

The project file currently points to the author's local game installation. Override `GamePath` for your installation:

```sh
dotnet build ValleyExchange/ValleyExchange.csproj -c Release -p:GamePath="PATH_TO_STARDEW_VALLEY"
```

## Install

Build the project, then use its generated mod package. Place the `ValleyExchange` folder containing `manifest.json`, `ValleyExchange.dll`, and `assets` in your game's `Mods` directory and launch the game through SMAPI.

## Repository layout

- `ValleyExchange/`: mod source and assets.
- `MarketCheck/`: market-checking utility.
- `tools/`: sprite-generation scripts.

The internal `evant.ValleyExchange` identifiers are retained for compatibility with existing saves. Build output and local development history are not included in this source snapshot.
