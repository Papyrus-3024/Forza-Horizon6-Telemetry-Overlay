using System.Windows;

namespace Fh6.Telemetry.Overlay.Theming;

/// <summary>
/// Writes the active <see cref="ThemePalette"/> into <see cref="Application.Current.Resources"/>
/// under stable string keys so redesigned widgets can bind via <c>{DynamicResource Fh6.Accent}</c>.
/// </summary>
public static class ThemeApplier
{
    public const string KeyAccent        = "Fh6.Accent";
    public const string KeyAccentDim     = "Fh6.AccentDim";
    public const string KeySurface       = "Fh6.Surface";
    public const string KeyBorder        = "Fh6.Border";
    public const string KeyTextPrimary   = "Fh6.TextPrimary";
    public const string KeyTextSecondary = "Fh6.TextSecondary";
    public const string KeyGood          = "Fh6.Good";
    public const string KeyWarn          = "Fh6.Warn";
    public const string KeyDanger        = "Fh6.Danger";

    /// <summary>
    /// Resolves the palette from <paramref name="preset"/> / <paramref name="customAccentHex"/>
    /// and injects all brushes into <c>Application.Current.Resources</c>.
    /// Safe to call before the main window is shown; no-ops if there is no current Application.
    /// </summary>
    public static void Apply(string preset, string? customAccentHex)
    {
        if (Application.Current is not { } app)
            return;

        try
        {
            var p = ThemePalette.Resolve(preset, customAccentHex);
            var res = app.Resources;
            res[KeyAccent]        = p.Accent;
            res[KeyAccentDim]     = p.AccentDim;
            res[KeySurface]       = p.Surface;
            res[KeyBorder]        = p.Border;
            res[KeyTextPrimary]   = p.TextPrimary;
            res[KeyTextSecondary] = p.TextSecondary;
            res[KeyGood]          = p.Good;
            res[KeyWarn]          = p.Warn;
            res[KeyDanger]        = p.Danger;
        }
        catch
        {
            // Never let a theming failure crash the overlay.
        }
    }
}
