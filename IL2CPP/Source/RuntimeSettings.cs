using MelonLoader;
using Il2CppFishNet;

namespace BotanistUseSprinkler.Runtime;

internal static class RuntimeSettings
{
    internal static bool SoilPourerEnabled { get; private set; } = true;

    internal static bool SprinklerEnabled { get; private set; } = true;

    internal static bool DebugEnabled { get; private set; }

    internal static bool AutoRefillSoilPourers { get; private set; } = true;

    private static int _configuredSoilPourerCapacity = 2;
    private static int? _networkSoilPourerCapacity;

    internal static int SoilPourerCapacity =>
        InstanceFinder.IsServer
            ? _configuredSoilPourerCapacity
            : _networkSoilPourerCapacity ?? _configuredSoilPourerCapacity;

    internal static bool AllowManualMultiBagFill { get; private set; } = true;

    internal static void Configure(
        bool soilPourerEnabled,
        bool sprinklerEnabled,
        bool debugEnabled,
        bool autoRefillSoilPourers = true,
        int soilPourerCapacity = 2,
        bool allowManualMultiBagFill = true)
    {
        SoilPourerEnabled = soilPourerEnabled;
        SprinklerEnabled = sprinklerEnabled;
        DebugEnabled = debugEnabled;
        AutoRefillSoilPourers = autoRefillSoilPourers;
        _configuredSoilPourerCapacity = Math.Clamp(soilPourerCapacity, 1, 8);
        _networkSoilPourerCapacity = null;
        AllowManualMultiBagFill = allowManualMultiBagFill;
    }

    internal static void ApplyHostSoilPourerCapacity(int capacity)
    {
        if (!InstanceFinder.IsServer)
        {
            _networkSoilPourerCapacity = Math.Clamp(capacity, 1, 8);
        }
    }

    internal static void Debug(string message)
    {
        if (DebugEnabled)
        {
            MelonLogger.Msg($"[DEBUG] {message}");
        }
    }

    internal static void Info(string message) => MelonLogger.Msg($"[INFO] {message}");

    internal static void Warning(string message) => MelonLogger.Warning(message);

    internal static void Error(string message) => MelonLogger.Error(message);
}
