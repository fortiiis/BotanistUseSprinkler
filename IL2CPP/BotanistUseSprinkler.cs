using System.Text.Json.Serialization;
using BotanistUseSprinkler.Runtime;
using MelonLoader;

[assembly: MelonInfo(typeof(BotanistUseSprinkler.BotanistUseSprinklerMod), "Botanist Use Sprinkler", "1.1.0", "Fortis")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace BotanistUseSprinkler;

public sealed class BotanistUseSprinklerMod : MelonMod
{
    public override void OnInitializeMelon()
    {
        Configuration configuration = ConfigurationFile.Load(
            "BotanistUseSprinkler",
            static () => new Configuration());

        RuntimeSettings.Configure(
            soilPourerEnabled: false,
            configuration.Enabled,
            configuration.Debug);

        RuntimeSettings.Info(
            $"Botanist Use Sprinkler 1.1.0 initialized for Schedule I 0.4.7f12. " +
            $"Enabled: {configuration.Enabled}; debug: {configuration.Debug}.");
    }
}

internal sealed class Configuration
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("debug")]
    public bool Debug { get; set; }
}
