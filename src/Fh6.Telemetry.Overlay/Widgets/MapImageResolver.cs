using System.IO;
using Fh6.Telemetry.Overlay.Settings;

namespace Fh6.Telemetry.Overlay.Widgets;

/// <summary>
/// Resolves which map image file to display based on the current overlay configuration.
/// Priority: explicit <see cref="OverlayConfig.MapImagePath"/> if non-empty and the file exists;
/// otherwise the bundled seasonal map under assets/maps/.
/// </summary>
public static class MapImageResolver
{
    /// <summary>
    /// Returns the resolved map image path, or null when no usable file is found.
    /// Priority: explicit <see cref="OverlayConfig.MapImagePath"/> if non-empty and exists;
    /// otherwise the bundled seasonal map under assets/maps/ if it exists; otherwise null.
    /// </summary>
    public static string? Resolve(OverlayConfig cfg)
    {
        if (!string.IsNullOrWhiteSpace(cfg.MapImagePath) && File.Exists(cfg.MapImagePath))
            return cfg.MapImagePath;

        var seasonName = cfg.Season.ToString().ToLowerInvariant();
        var seasonPath = Path.Combine(AppContext.BaseDirectory, "assets", "maps", seasonName + ".jpg");
        return File.Exists(seasonPath) ? seasonPath : null;
    }
}
