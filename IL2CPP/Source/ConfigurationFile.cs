using System.Text.Json;
using MelonLoader;

namespace BotanistUseSprinkler.Runtime;

internal static class ConfigurationFile
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    internal static T Load<T>(string directoryName, Func<T> createDefaults)
        where T : class
    {
        string directoryPath = Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, directoryName);
        string filePath = Path.Combine(directoryPath, "configuration.json");
        T defaults = createDefaults();

        try
        {
            Directory.CreateDirectory(directoryPath);

            if (!File.Exists(filePath))
            {
                Save(filePath, defaults);
                return defaults;
            }

            string json = File.ReadAllText(filePath);
            T? loaded = JsonSerializer.Deserialize<T>(json, SerializerOptions);
            if (loaded is not null)
            {
                // Rewrite successfully loaded files so newly added options appear with their
                // default values instead of remaining invisible until the user recreates them.
                Save(filePath, loaded);
                return loaded;
            }

            RuntimeSettings.Warning($"Configuration at '{filePath}' was empty; using defaults.");
        }
        catch (Exception exception)
        {
            RuntimeSettings.Warning($"Could not read configuration at '{filePath}': {exception.Message}. Using defaults.");
        }

        return defaults;
    }

    private static void Save<T>(string filePath, T value)
    {
        string json = JsonSerializer.Serialize(value, SerializerOptions);
        File.WriteAllText(filePath, json);
    }
}
