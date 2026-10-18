# BotanistUseSprinkler

Botanists can use linked sprinklers while watering pots. This repository was created by Fortis.

Version 1.1.0 updates the IL2CPP build for Schedule I 0.4.7f12.

## What changed

- Uses the current `WaterPotBehaviour` task instead of the removed `PotActionBehaviour` type.
- Detects linked small and large sprinklers.
- Activates a suitable sprinkler when a botanist starts a watering task.
- Releases the botanist immediately after activation so the sprinkler handles the watering.
- Suppresses the normal watering action only when the sprinkler path succeeds.
- Falls back to the normal watering-can workflow when a sprinkler cannot be used.
- Caches device lookups for 10 seconds to reduce repeated scene scans.

## Configuration

The configuration file is created at:

`UserData/BotanistUseSprinkler/configuration.json`

Default configuration:

```json
{
  "enabled": true,
  "debug": false
}
```

- `enabled` turns the mod on or off.
- `debug` enables extra diagnostic logging.

## Installation

1. Install MelonLoader for the IL2CPP version of Schedule I.
2. Put `BotanistUseSprinkler.dll` in the game's `Mods` folder.
3. In multiplayer, every participant should use the same DLL and configuration.

The Mono project is retained for reference. It is not part of this compatibility update.

## Building

Build with .NET 6 and point `MelonLoaderRoot` at the game's MelonLoader directory:

```powershell
dotnet build IL2CPP/BotanistUseSprinkler.csproj --configuration Release --property:MelonLoaderRoot="C:\Program Files (x86)\Steam\steamapps\common\Schedule I\MelonLoader"
```

Game and MelonLoader assemblies are referenced from the local installation. They are not committed to this repository.

## Credits

- Original mod by Fortis.
- Schedule I 0.4.7f12 compatibility update contributed by GSVS UK ACM / xboxnuker-rgb.
