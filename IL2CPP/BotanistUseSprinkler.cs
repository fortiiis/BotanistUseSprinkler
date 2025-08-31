using BotanistUseSprinkler;
using HarmonyLib;
using Il2CppFishNet;
using Il2CppScheduleOne.EntityFramework;
using Il2CppScheduleOne.NPCs.Behaviour;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.Tiles;
using MelonLoader;
using System.Text.Json.Serialization;
using System.Text.Json;
using UnityEngine;

[assembly: MelonInfo(typeof(BotanistUseSprinklerMod), "Botanist Use Sprinkler", "1.0.2", "Fortis")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace BotanistUseSprinkler;

public class BotanistUseSprinklerMod : MelonMod
{
    private HarmonyLib.Harmony _harmony;
    private static readonly string ConfigDirectoryPath = $"{Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "BotanistUseSprinkler")}";
    private static readonly string ConfigFilePath = $"{Path.Combine(ConfigDirectoryPath, "configuration.json")}";
    private static Configuration _config;

    public override void OnInitializeMelon()
    {
        Log("Initializing...", LogLevel.Info);
        LoadConfigFromFile();

        Log($"Enabled: {_config.Enabled}", LogLevel.Info);
        if (_config.Enabled)
        {
            _harmony = HarmonyLib.Harmony.CreateAndPatchAll(typeof(BotanistUseSprinklerMod), "com.fortis.botanistusesprinkler");

            if (_config.Debug)
                Log("Debug Mode Enabled", LogLevel.Debug);

            Log("Initialized", LogLevel.Info);
        }
    }

    public override void OnDeinitializeMelon()
    {
        if (_harmony is not null)
        {
            Log("Unpatching Mod", LogLevel.Info);
            _harmony.UnpatchSelf();
            Log("Unpatched", LogLevel.Info);
        }
    }

    // Perform Action patches don't work on IL2CPP version so ActiveMinPass it is
    // Start Action fires before botanist is at pot, which negates the point
    // As far as I can tell no other method works on IL2CPP, ActiveMinPass is not ideal
    [HarmonyPatch(typeof(PotActionBehaviour), "ActiveMinPass")]
    public static class PotActionBehaviourActiveMinPassPatch
    {
        public static List<Pot> ActiveHandWateredPots = new List<Pot>();

        [HarmonyPostfix]
        private static void Postfix(PotActionBehaviour __instance)
        {
            if (!_config.Enabled)
                return;
            if (!InstanceFinder.IsServer)
                return;
            if (__instance.CurrentState != PotActionBehaviour.EState.PerformingAction)
                return;
            if (__instance.CurrentActionType != PotActionBehaviour.EActionType.Water)
                return;
            if (!IsAtPot(__instance))
                return;

            Log($"(PotActionBehaviourActiveMinPassPatch/Postfix) ActiveHandWateredPots Count: {ActiveHandWateredPots.Count}", LogLevel.Debug);
            if (ActiveHandWateredPots.FirstOrDefault(x => x.OriginCoordinate == __instance.AssignedPot.OriginCoordinate) is not null)
            {
                Coordinate assignedPotCoords = new Coordinate(__instance.AssignedPot.OriginCoordinate);
                Log($"(PotActionBehaviourActiveMinPassPatch/Postfix) Pot at x: {assignedPotCoords.x}, y: {assignedPotCoords.y} is currently being hand watered, skipping", LogLevel.Debug);
                return;
            }

            Sprinkler? sprinkler = GetPotSprinkler(__instance.AssignedPot);
            if (sprinkler is null)
            {
                Log($"(PotActionBehaviourActiveMinPassPatch/Postfix) Pot has no sprinklers, adding to active hand watered pots", LogLevel.Debug);
                ActiveHandWateredPots.Add(__instance.AssignedPot);
                return;
            }

            Coordinate sprinklerCoords = new Coordinate(sprinkler.OriginCoordinate);
            Log($"(PotActionBehaviourActiveMinPassPatch/Postfix) Sprinkler at x: {sprinklerCoords.x}, y: {sprinklerCoords.y} with IsSprinkling: {sprinkler.IsSprinkling}", LogLevel.Debug);
            if (!sprinkler.IsSprinkling)
                sprinkler.Interacted();

            __instance.StopPerformAction();
            __instance.CompleteAction();
            __instance.SendEnd();
            __instance.botanist.SetIdle(true);
        }

        private static bool IsAtPot(PotActionBehaviour behaviour)
        {
            if (behaviour.AssignedPot is null)
                return false;
            
            for (int i = 0; i < behaviour.AssignedPot.AccessPoints.Length; i++)
            {
                if (Vector3.Distance(behaviour.Npc.transform.position, behaviour.AssignedPot.AccessPoints[i].position) < 0.4f)
                    return true;

                if (behaviour.Npc.Movement.IsAsCloseAsPossible(behaviour.AssignedPot.AccessPoints[i].transform.position, 0.4f))
                    return true;
            }

            return false;
        }

        private static Sprinkler? GetPotSprinkler(Pot pot)
        {
            Coordinate assignedPotCoords = new Coordinate(pot.OriginCoordinate);
            Log($"(PotActionBehaviourActiveMinPassPatch/GetPotSprinklers) Assigned Pot Coords x: {assignedPotCoords.x}, y: {assignedPotCoords.y}", LogLevel.Debug);
            List<Sprinkler> sprinklersAroundPot = new List<Sprinkler>();
            for (int t = 1; t < 4; t++)
            {
                Coordinate posXCoord = new Coordinate(assignedPotCoords.x + t, assignedPotCoords.y);
                Coordinate negXCoord = new Coordinate(assignedPotCoords.x - t, assignedPotCoords.y);
                Coordinate posYCoord = new Coordinate(assignedPotCoords.x, assignedPotCoords.y + t);
                Coordinate negYCoord = new Coordinate(assignedPotCoords.x, assignedPotCoords.y - t);


                Tile posXTile = pot.OwnerGrid.GetTile(posXCoord);
                Tile negXTile = pot.OwnerGrid.GetTile(negXCoord);
                Tile posYTile = pot.OwnerGrid.GetTile(posYCoord);
                Tile negYTile = pot.OwnerGrid.GetTile(negYCoord);

                Tile[] tiles = { posXTile, negXTile, posYTile, negYTile };

                for (int i = 0; i < tiles.Length; i++)
                {
                    if (tiles[i] is null)
                    {
                        Log($"(PotActionBehaviourActiveMinPassPatch/GetPotSprinklers) Tile at index {i} is null, skipping", LogLevel.Debug);
                        continue;
                    }
                    Log($"(PotActionBehaviourActiveMinPassPatch/GetPotSprinklers) Checking tile at x: {tiles[i].x}, y: {tiles[i].y}", LogLevel.Debug);
                    if (!TryGetSprinkler(tiles[i], out Sprinkler? sprinkler))
                    {
                        Log($"(PotActionBehaviourActiveMinPassPatch/GetPotSprinklers) Tile at x: {tiles[i].x}, y: {tiles[i].y} contains no sprinklers", LogLevel.Debug);
                        continue;
                    }
                    if (sprinkler is null)
                    {
                        Log($"(PotActionBehaviourActiveMinPassPatch/GetPotSprinklers) Tile at x: {tiles[i].x}, y: {tiles[i].y} contains a sprinkler but sprinkler returned is null", LogLevel.Warn);
                        continue;
                    }

                    Log($"(PotActionBehaviourActiveMinPassPatch/GetPotSprinklers) Sprinkler found at x: {tiles[i].x}, y: {tiles[i].y}", LogLevel.Debug);
                    sprinklersAroundPot.Add(sprinkler);
                }
            }

            Sprinkler? potSprinkler = null;
            if (sprinklersAroundPot.Count <= 0)
                return potSprinkler;

            Log($"(PotActionBehaviourActiveMinPassPatch/GetPotSprinklers) sprinklersAroundPot Count: {sprinklersAroundPot.Count}", LogLevel.Debug);

            for (int i = 0; i < sprinklersAroundPot.Count; i++)
            {
                Coordinate sprinklerCoords = new Coordinate(sprinklersAroundPot[i].OriginCoordinate);
                var pots = sprinklersAroundPot[i].GetPots();
                if (pots.Count <= 0)
                {
                    Log($"(PotActionBehaviourActiveMinPassPatch/GetPotSprinklers) Sprinkler at x: {sprinklerCoords.x}, y: {sprinklerCoords.y} contains no pots", LogLevel.Debug);
                    break;
                }

                foreach (var sprinklerPot in pots)
                {
                    if (sprinklerPot is null)
                    {
                        Log("(PotActionBehaviourActiveMinPassPatch/GetPotSprinklers) Sprinkler Pot is null", LogLevel.Debug);
                        continue;
                    }

                    Coordinate potCoords = new Coordinate(sprinklerPot.OriginCoordinate);
                    Log($"(PotActionBehaviourActiveMinPassPatch/GetPotSprinklers) Checking if pot matches assigned pot", LogLevel.Debug);
                    if (potCoords.x == assignedPotCoords.x && potCoords.y == assignedPotCoords.y)
                    {
                        Log("(PotActionBehaviourActiveMinPassPatch/GetPotSprinklers) Pot matched, returning sprinkler", LogLevel.Debug);
                        potSprinkler = sprinklersAroundPot[i];
                    }
                }
            }

            return potSprinkler;
        }

        private static bool TryGetSprinkler(Tile tile, out Sprinkler? sprinkler)
        {
            sprinkler = null;
            foreach (GridItem item in tile.BuildableOccupants)
            {
                if (item is null)
                    continue;

                Log($"(PotActionBehaviourActiveMinPassPatch/TryGetSprinkler) Grid Item: {item.name}", LogLevel.Debug);

                if (item.TryGetComponent<Sprinkler>(out Sprinkler potSprinkler))
                {
                    sprinkler = potSprinkler;
                    return true;
                }
            }

            return false;
        }
    }

    // Pots with no sprinklers on them cause the mod to check for sprinklers every second until Botanist finishes hand watering
    // This is hopefully a temporary fix to this, this method is only called once so its not expensive
    [HarmonyPatch(typeof(PotActionBehaviour), nameof(PotActionBehaviour.CompleteAction))]
    public static class PotActionBehaviourCompleteActionPatch
    {
        [HarmonyPostfix]
        private static void Postfix(PotActionBehaviour __instance)
        {
            if (__instance.CurrentActionType is not PotActionBehaviour.EActionType.Water)
                return;

            if (__instance.AssignedPot is not null)
            {
                if (PotActionBehaviourActiveMinPassPatch.ActiveHandWateredPots.FirstOrDefault(x => x.OriginCoordinate == __instance.AssignedPot.OriginCoordinate) is not null)
                {
                    Log($"(PotActionBehaviourCompleteActionPatch/Postfix) Pot finished watering, removing pot from active hand watered", LogLevel.Debug);
                    PotActionBehaviourActiveMinPassPatch.ActiveHandWateredPots.Remove(__instance.AssignedPot);
                }
            }
        }
    }

    private static void LoadConfigFromFile()
    {
        var config = new Configuration();
        config.Enabled = true;
        config.Debug = false;

        if (!Directory.Exists(ConfigDirectoryPath))
            Directory.CreateDirectory(ConfigDirectoryPath);

        if (!File.Exists(ConfigFilePath))
        {
            CreateConfigFile();
            _config = config;
            return;
        }

        string text = File.ReadAllText(ConfigFilePath);
        var json = JsonSerializer.Deserialize<Configuration>(text);
        if (json is null)
        {
            _config = config;
            return;
        }

        config.Enabled = json.Enabled;
        config.Debug = json.Debug;

        _config = config;
    }

    private static void CreateConfigFile()
    {
        JsonSerializerOptions options = new JsonSerializerOptions();
        options.WriteIndented = true;

        Configuration baseConfig = new Configuration();
        baseConfig.Enabled = true;
        baseConfig.Debug = false;

        var json = JsonSerializer.Serialize(baseConfig, options);
        File.WriteAllText(ConfigFilePath, json);
    }

    private static void Log(string message, LogLevel level)
    {
        string prefix = GetLogPrefix(level);
        if (level is LogLevel.Debug)
        {
            if (!_config.Debug)
                return;
        }

        MelonLogger.Msg($"{prefix} {message}");
    }

    private static string GetLogPrefix(LogLevel level)
    {
        switch (level)
        {
            case LogLevel.Debug:
                return "[DEBUG]";
            case LogLevel.Info:
                return "[INFO]";
            case LogLevel.Warn:
                return "[WARN]";
            case LogLevel.Error:
                return "[ERROR]";
            case LogLevel.Fatal:
                return "[FATAL]";
            default:
                return "[MISC]";
        }
    }

    private enum LogLevel
    {
        Debug,
        Info,
        Warn,
        Error,
        Fatal
    }
}

public class Configuration
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("debug")]
    public bool Debug { get; set; }
}
