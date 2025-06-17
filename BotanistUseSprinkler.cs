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

[assembly: MelonInfo(typeof(BotanistUseSprinklerMod), "Botanist Use Sprinkler", "1.0.0", "Fortis")]
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

        if (_config.Enabled)
            _harmony = HarmonyLib.Harmony.CreateAndPatchAll(typeof(BotanistUseSprinklerMod), "com.fortis.botanistusesprinkler");

        Log("Initialized", LogLevel.Info);
    }

    [HarmonyPatch(typeof(PotActionBehaviour), "ActiveMinPass")]
    public static class Test
    {
        [HarmonyPostfix]
        private static void Postfix(PotActionBehaviour __instance)
        {
            if (!InstanceFinder.IsServer)
                return;
            if (__instance.CurrentState != PotActionBehaviour.EState.PerformingAction)
                return;
            if (__instance.CurrentActionType != PotActionBehaviour.EActionType.Water)
                return;
            if (!IsAtPot(__instance))
                return;

            List<Sprinkler> sprinklers = GetSprinklers(__instance.AssignedPot);
            Log($"(PotActionBehaviour/ActiveMinPass) Sprinklers found: {sprinklers.Count}", LogLevel.Debug);
            if (sprinklers.Count <= 0)
                return;

            Coordinate assignedPot = new Coordinate(__instance.AssignedPot.OriginCoordinate);
            foreach (Sprinkler sprinkler in sprinklers)
            {
                List<Pot> pots = GetPots(sprinkler);
                if (pots is null)
                    continue;
                if (pots.Count <= 0)
                    continue;

                Log($"(PotActionBehaviour/ActiveMinPass) Pot 0 coords: x: {pots[0].OriginCoordinate.x}, y: {pots[0].OriginCoordinate.y}", LogLevel.Debug);
                Log($"(PotActionBehaviour/ActiveMinPass) Assigned Pot coords: x: {__instance.AssignedPot.OriginCoordinate.x}, y: {__instance.AssignedPot.OriginCoordinate.y}", LogLevel.Debug);
                Coordinate pot = new Coordinate(pots.First().OriginCoordinate);
                if (pot.x == assignedPot.x && pot.y == assignedPot.y)
                {
                    if (sprinkler.IsSprinkling)
                        continue;

                    Log("(PotActionBehaviour/ActiveMinPass) Activating sprinkler", LogLevel.Debug);
                    sprinkler.Interacted();
                }
            }

            __instance.Disable_Networked(null);
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

        private static List<Sprinkler> GetSprinklers(Pot pot)
        {
            Coordinate og = new Coordinate(pot.OriginCoordinate);
            List<Tile> tiles = new List<Tile>();
            for (int i = 0; i < 3; i++)
            {
                Coordinate posX = new Coordinate(og.x + i, og.y);
                Coordinate negX = new Coordinate(og.x - i, og.y);
                Coordinate posY = new Coordinate(og.x, og.y + i);
                Coordinate negY = new Coordinate(og.x, og.y - i);

                Tile posXTile = pot.OwnerGrid.GetTile(posX);
                Tile negXTile = pot.OwnerGrid.GetTile(negX);
                Tile posYTile = pot.OwnerGrid.GetTile(posY);
                Tile negYTile = pot.OwnerGrid.GetTile(negY);

                if (posXTile is not null)
                    tiles.Add(posXTile);
                if (negXTile is not null)
                    tiles.Add(negXTile);
                if (posYTile is not null)
                    tiles.Add(posYTile);
                if (negYTile is not null)
                    tiles.Add(negYTile);
            }

            if (tiles.Count <= 0)
            {
                Log("Test tile count 0", LogLevel.Debug);
                return new List<Sprinkler>();
            }

            List<Sprinkler> sprinklers = new List<Sprinkler>();
            foreach (Tile tile in tiles)
            {
                Sprinkler? sprinkler = TryGetSprinkler(tile.BuildableOccupants);
                if (sprinkler is null)
                    continue;

                Coordinate originCoords = new Coordinate(sprinkler.OriginCoordinate);
                Log($"TestSprinkler Sprinkler found at coords: x: {originCoords.x}, y: {originCoords.y}", LogLevel.Debug);
                sprinklers.Add(sprinkler);
            }

            return sprinklers;
        }

        private static List<Pot> GetPots(Sprinkler sprinkler)
        {
            List<Pot> pots = new List<Pot>();
            Coordinate coord1 = new Coordinate(sprinkler.OriginCoordinate) + Coordinate.RotateCoordinates(new Coordinate(0, 1), sprinkler.Rotation);
            Coordinate coord2 = new Coordinate(sprinkler.OriginCoordinate) + Coordinate.RotateCoordinates(new Coordinate(1, 1), sprinkler.Rotation);
            Tile tile = sprinkler.OwnerGrid.GetTile(coord1);
            Tile tile2 = sprinkler.OwnerGrid.GetTile(coord2);
            if (tile != null && tile2 != null)
            {
                Pot pot = null;
                foreach (GridItem item in tile.BuildableOccupants)
                {
                    if (item is Pot)
                    {
                        pot = item as Pot;
                        break;
                    }
                }

                if (pot != null && tile2.BuildableOccupants.Contains(pot))
                {
                    pots.Add(pot);
                }
            }

            return pots;
        }

        private static Sprinkler? TryGetSprinkler(Il2CppSystem.Collections.Generic.List<GridItem> gridItems)
        {
            Sprinkler? sprinkler = null;
            foreach (GridItem item in gridItems)
            {
                Log($"(TryGetSprinkler) Grid Item: {item.name}", LogLevel.Debug);
                if (item.TryGetComponent<Sprinkler>(out Sprinkler potSprinkler))
                {
                    sprinkler = potSprinkler;
                    return sprinkler;
                }
            };

            return null;
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
