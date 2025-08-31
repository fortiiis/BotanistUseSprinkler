# BotanistUseSprinkler
A Schedule I quality of life mod. This mod makes Botanist use a sprinkler if it exists, this improves the time it takes for them to water.

Works with multiplayer, per testing only the host needs the mod.

# Installation
- Install [MelonLoader](https://melonwiki.xyz/#/?id=requirements)

## If Base Game (IL2CPP) Version
- Drag `BotanistUseSprinkler.dll` into the `Mods` folder

## If Alternate Game (Mono) Version
- Drag `BotanistUseSprinkler_Mono.dll` into the `Mods` folder

# Changelog

## v1.0.3
- Tweaked how mod gets sprinklers to hopefully fix cases where certain sprinkler placements may not be triggered by Botanist

## v1.0.2
IL2CPP Only

- Fixed issue where if a pot has no sprinkler, mod would try to get pot sprinklers every second while botanist hand watered
Mono

- Optional update, only fixed version shown in console when loading and matched IL2CPP version (1.0.2)

## v1.0.1
- Fixed animation bug which also improves speed
- Slightly tweaked how it gets sprinklers


Mono Only

- Changed check from ActiveMinPass to PerformAction (ActiveMinPass is called per game tick, whereas PerformAction is only called once)

## v1.0.0
- Initial release