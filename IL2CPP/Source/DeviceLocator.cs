using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.Tiles;

namespace BotanistUseSprinkler.Runtime;

internal static class DeviceLocator
{
    // Device placement changes rarely, while this lookup is reached from worker
    // decision ticks. Keep discovery responsive without rebuilding two world
    // maps several times per second on large saves.
    private const float CacheRefreshSeconds = 10f;
    private const float FaultRetrySeconds = 10f;
    private static readonly Dictionary<IntPtr, float> FaultedDevices = new();

#if FEATURE_SOIL
    private static readonly Dictionary<IntPtr, SoilPourer> SoilPourersByPot = new();
    private static readonly Dictionary<PotKey, SoilPourer> SoilPourersByCoordinate = new();
    private static float _nextSoilRefresh;

    internal static SoilPourer? FindSoilPourer(Pot pot)
    {
        // A legitimately empty cache must still honour the refresh deadline.
        // The old Count == 0 condition caused FindObjectsOfType on every lookup
        // in worlds/properties without a usable device.
        if (UnityEngine.Time.realtimeSinceStartup >= _nextSoilRefresh)
        {
            RefreshSoilPourers();
        }

        if (SoilPourersByPot.TryGetValue(pot.Pointer, out SoilPourer? pourer) && IsUsable(pourer))
        {
            return pourer;
        }

        return SoilPourersByCoordinate.TryGetValue(PotKey.From(pot), out pourer) && IsUsable(pourer)
            ? pourer
            : null;
    }

    private static void RefreshSoilPourers()
    {
        _nextSoilRefresh = UnityEngine.Time.realtimeSinceStartup + CacheRefreshSeconds;
        SoilPourersByPot.Clear();
        SoilPourersByCoordinate.Clear();

        foreach (SoilPourer pourer in UnityEngine.Object.FindObjectsOfType<SoilPourer>())
        {
            if (!IsUsable(pourer) || IsCoolingDown(pourer.Pointer))
            {
                continue;
            }

            try
            {
                foreach (Pot pot in pourer.GetPots())
                {
                    if (pot is null)
                    {
                        continue;
                    }

                    SoilPourersByPot.TryAdd(pot.Pointer, pourer);
                    SoilPourersByCoordinate.TryAdd(PotKey.From(pot), pourer);
                }

                FaultedDevices.Remove(pourer.Pointer);
            }
            catch (Exception exception)
            {
                RecordFault(pourer.Pointer, "soil pourer", exception);
            }
        }
    }
#endif

#if FEATURE_SPRINKLER
    private static readonly Dictionary<IntPtr, Sprinkler> SprinklersByPot = new();
    private static readonly Dictionary<PotKey, Sprinkler> SprinklersByCoordinate = new();
    private static float _nextSprinklerRefresh;

    internal static Sprinkler? FindUsableSprinkler(Pot pot)
    {
        if (UnityEngine.Time.realtimeSinceStartup >= _nextSprinklerRefresh)
        {
            RefreshSprinklers();
        }

        Sprinkler? sprinkler = null;
        if (!SprinklersByPot.TryGetValue(pot.Pointer, out sprinkler))
        {
            SprinklersByCoordinate.TryGetValue(PotKey.From(pot), out sprinkler);
        }

        if (!IsUsable(sprinkler))
        {
            return null;
        }

        Sprinkler activeSprinkler = sprinkler!;
        try
        {
            return activeSprinkler.IsSprinkling || activeSprinkler.CanWater() ? activeSprinkler : null;
        }
        catch (Exception exception)
        {
            RecordFault(activeSprinkler.Pointer, "sprinkler", exception);
            return null;
        }
    }

    private static void RefreshSprinklers()
    {
        _nextSprinklerRefresh = UnityEngine.Time.realtimeSinceStartup + CacheRefreshSeconds;
        SprinklersByPot.Clear();
        SprinklersByCoordinate.Clear();

        foreach (Sprinkler sprinkler in UnityEngine.Object.FindObjectsOfType<Sprinkler>())
        {
            if (!IsUsable(sprinkler) || IsCoolingDown(sprinkler.Pointer))
            {
                continue;
            }

            try
            {
                foreach (Pot pot in sprinkler.GetPots())
                {
                    if (pot is null)
                    {
                        continue;
                    }

                    SprinklersByPot.TryAdd(pot.Pointer, sprinkler);
                    SprinklersByCoordinate.TryAdd(PotKey.From(pot), sprinkler);
                }

                FaultedDevices.Remove(sprinkler.Pointer);
            }
            catch (Exception exception)
            {
                RecordFault(sprinkler.Pointer, "sprinkler", exception);
            }
        }
    }
#endif

    private static bool IsUsable(Il2CppScheduleOne.EntityFramework.BuildableItem? device) =>
        device is not null && device.Initialized && !device.IsDestroyed;

    private static bool IsCoolingDown(IntPtr pointer) =>
        FaultedDevices.TryGetValue(pointer, out float retryAt) &&
        UnityEngine.Time.realtimeSinceStartup < retryAt;

    private static void RecordFault(IntPtr pointer, string deviceName, Exception exception)
    {
        bool firstFault = !FaultedDevices.ContainsKey(pointer);
        FaultedDevices[pointer] = UnityEngine.Time.realtimeSinceStartup + FaultRetrySeconds;

        if (firstFault)
        {
            RuntimeSettings.Warning(
                $"Ignored an uninitialized or invalid {deviceName} while resolving an attached device: {exception.Message}");
        }
    }

    private readonly struct PotKey : IEquatable<PotKey>
    {
        private PotKey(IntPtr grid, int x, int y)
        {
            Grid = grid;
            X = x;
            Y = y;
        }

        private IntPtr Grid { get; }
        private int X { get; }
        private int Y { get; }

        internal static PotKey From(Pot pot)
        {
            Coordinate coordinate = new(pot._originCoordinate);
            return new PotKey(pot.OwnerGrid?.Pointer ?? IntPtr.Zero, coordinate.x, coordinate.y);
        }

        public bool Equals(PotKey other) => Grid == other.Grid && X == other.X && Y == other.Y;

        public override bool Equals(object? obj) => obj is PotKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Grid, X, Y);
    }
}
