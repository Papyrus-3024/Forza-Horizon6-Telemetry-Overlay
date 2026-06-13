using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fh6.Telemetry.Overlay.Settings;

public static class ConfigStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Default config path under %AppData%/fh6-overlay/config.json.</summary>
    public static string DefaultPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "fh6-overlay", "config.json");

    public static OverlayConfig Load(string path)
    {
        try
        {
            if (!File.Exists(path))
                return new OverlayConfig();
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<OverlayConfig>(json, Options) ?? new OverlayConfig();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return new OverlayConfig();
        }
    }

    public static void Save(string path, OverlayConfig config)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(config, Options));
    }
}
