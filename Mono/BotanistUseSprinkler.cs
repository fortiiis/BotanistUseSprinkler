using BotanistUseSprinkler;
using HarmonyLib;
using MelonLoader;
using Newtonsoft.Json;
using ScheduleOne.Employees;
using ScheduleOne.EntityFramework;
using ScheduleOne.NPCs.Behaviour;
using ScheduleOne.ObjectScripts;
using ScheduleOne.Tiles;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

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

    [HarmonyPatch(typeof(PotActionBehaviour), nameof(PotActionBehaviour.PerformAction))]
    public static class PotActionBehaviourPerformActionPatch
    {
        private static MethodInfo GetPots = typeof(Sprinkler).GetMethod("GetPots", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        private static MethodInfo StopPerformAction = typeof(PotActionBehaviour).GetMethod("StopPerformAction", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        private static MethodInfo CompleteAction = typeof(PotActionBehaviour).GetMethod("CompleteAction", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

        [HarmonyPostfix]
        private static void Postfix(PotActionBehaviour __instance)
        {
            if (!_config.Enabled)
                return;
            if (GetPots is null)
            {
                Log("(PotActionBehaviourPerformActionPatch/Postfix) GetPots method is null, cannot execute", LogLevel.Warn);
                return;
            }
            if (StopPerformAction is null)
            {
                Log("(PotActionBehaviourPerformActionPatch/Postfix) StopPerformAction method is null, cannot execute", LogLevel.Warn);
                return;
            }
            if (CompleteAction is null)
            {
                Log("(PotActionBehaviourPerformActionPatch/Postfix) CompleteAction method is null, cannot execute", LogLevel.Warn);
                return;
            }

            Log($"(PotActionBehaviourPeformActionPatch/Postfix) Postfix fired with action type: {__instance.CurrentActionType.ToString()} and current state: {__instance.CurrentState.ToString()}", LogLevel.Debug);
            if (__instance.CurrentActionType != PotActionBehaviour.EActionType.Water)
                return;

            Sprinkler? sprinkler = GetPotSprinkler(__instance.AssignedPot);
            if (sprinkler is null)
                return;

            Coordinate sprinklerCoords = new Coordinate(sprinkler.OriginCoordinate);
            Log($"(PotActionBehaviourPeformActionPatch/Postfix) Sprinkler at x: {sprinklerCoords.x}, y: {sprinklerCoords.y} with IsSprinkling: {sprinkler.IsSprinkling}", LogLevel.Debug);
            if (!sprinkler.IsSprinkling)
                sprinkler.Interacted();

            StopPerformAction.Invoke(__instance, null);
            CompleteAction.Invoke(__instance, null);

            __instance.SendEnd();
            Botanist botanist = __instance.Npc as Botanist;
            if (botanist is not null)
                botanist.SetIdle(true);
        }

        private static Sprinkler? GetPotSprinkler(Pot pot)
        {
            Coordinate assignedPotCoords = new Coordinate(pot.OriginCoordinate);
            Log($"(PotActionBehaviourPeformActionPatch/GetPotSprinklers) Assigned Pot Coords x: {assignedPotCoords.x}, y: {assignedPotCoords.y}", LogLevel.Debug);
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
                        Log($"(PotActionBehaviourPerformActionPatch/GetPotSprinklers) Tile at index {i} is null, skipping", LogLevel.Debug);
                        continue;
                    }
                    Log($"(PotActionBehaviourPerformActionPatch/GetPotSprinklers) Checking tile at x: {tiles[i].x}, y: {tiles[i].y}", LogLevel.Debug);
                    if (!TryGetSprinkler(tiles[i], out Sprinkler? sprinkler))
                    {
                        Log($"(PotActionBehaviourPerformActionPatch/GetPotSprinklers) Tile at x: {tiles[i].x}, y: {tiles[i].y} contains no sprinklers", LogLevel.Debug);
                        continue;
                    }
                    if (sprinkler is null)
                    {
                        Log($"(PotActionBehaviourPerformActionPatch/GetPotSprinklers) Tile at x: {tiles[i].x}, y: {tiles[i].y} contains a sprinkler but sprinkler returned is null", LogLevel.Warn);
                        continue;
                    }

                    Log($"(PotActionBehaviourPerformActionPatch/GetPotSprinklers) Sprinkler found at x: {tiles[i].x}, y: {tiles[i].y}", LogLevel.Debug);
                    sprinklersAroundPot.Add(sprinkler);
                }
            }

            Sprinkler? potSprinkler = null;
            if (sprinklersAroundPot.Count <= 0)
                return potSprinkler;

            Log($"(PotActionBehaviourPerformActionPatch/GetPotSprinklers) sprinklersAroundPot Count: {sprinklersAroundPot.Count}", LogLevel.Debug);

            for (int i = 0; i < sprinklersAroundPot.Count; i++)
            {
                Coordinate sprinklerCoords = new Coordinate(sprinklersAroundPot[i].OriginCoordinate);
                var pots = GetPots.Invoke(sprinklersAroundPot[i], null);
                if (pots is null)
                {
                    Log($"(PotActionBehaviourPerformActionPatch/GetPotSprinklers) Pots for sprinkler at x: {sprinklerCoords.x}, y: {sprinklerCoords.y} is null", LogLevel.Debug);
                    continue;
                }
                if (pots is not List<Pot> sprinklerPots)
                {
                    Log($"(PotActionBehaviourPerformActionPatch/GetPotSprinklers) Returned value for GetPots is not List<Pot>", LogLevel.Debug);
                    continue;
                }
                if (sprinklerPots is null)
                {
                    Log($"(PotActionBehaviourPeformActionPatch/GetPotSprinklers) Sprinkler at x: {sprinklerCoords.x}, y: {sprinklerCoords.y} returned pots is null", LogLevel.Debug);
                    continue;
                }
                if (sprinklerPots.Count <= 0)
                {
                    Log($"(PotActionBehaviourPeformActionPatch/GetPotSprinklers) Sprinkler at x: {sprinklerCoords.x}, y: {sprinklerCoords.y} has not pots", LogLevel.Debug);
                    continue;
                }

                foreach (var sprinklerPot in sprinklerPots)
                {
                    if (sprinklerPot is null)
                    {
                        Log("(PotActionBehaviourPerformActionPatch/GetPotSprinklers) Sprinkler pot is pull", LogLevel.Debug);
                        continue;
                    }

                    Coordinate potCoords = new Coordinate(sprinklerPot.OriginCoordinate);
                    Log("(PotActionBehaviourPerformActionPatch/GetPotSprinklers) Checking if pot matches assigned pot", LogLevel.Debug);
                    if (potCoords.x == assignedPotCoords.x && potCoords.y == assignedPotCoords.y)
                    {
                        Log("(PotActionBehaviourPerformActionPatch/GetPotSprinklers) Pot matched, returning sprinkler", LogLevel.Debug);
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

                if (item is Sprinkler)
                {
                    sprinkler = (Sprinkler)item;
                    break;
                }
            }

            return sprinkler;
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
        var json = JsonConvert.DeserializeObject<Configuration>(text);
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
        JsonSerializerSettings options = new JsonSerializerSettings();
        options.Formatting = Formatting.Indented;

        Configuration baseConfig = new Configuration();
        baseConfig.Enabled = true;
        baseConfig.Debug = false;

        var json = JsonConvert.SerializeObject(baseConfig, options);
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
    [JsonProperty("enabled")]
    public bool Enabled { get; set; }

    [JsonProperty("debug")]
    public bool Debug { get; set; }
}
