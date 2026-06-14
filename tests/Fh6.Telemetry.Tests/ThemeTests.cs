using Fh6.Telemetry.Overlay.Settings;
using Fh6.Telemetry.Overlay.Theming;
using Xunit;

namespace Fh6.Telemetry.Tests;

public class ThemePaletteTests
{
    [Fact]
    public void Known_preset_returns_expected_accent()
    {
        var p = ThemePalette.Resolve("DarkGlass");
        // DarkGlass accent is #FF5AD15A (opaque teal-green)
        Assert.Equal(0xFF, p.Accent.Color.A);
        Assert.Equal(0x5A, p.Accent.Color.R);
        Assert.Equal(0xD1, p.Accent.Color.G);
        Assert.Equal(0x5A, p.Accent.Color.B);
    }

    [Fact]
    public void Unknown_preset_falls_back_to_DarkGlass()
    {
        var p = ThemePalette.Resolve("NotAPreset");
        var dark = ThemePalette.Resolve("DarkGlass");
        Assert.Equal(dark.Accent.Color, p.Accent.Color);
    }

    [Fact]
    public void Null_preset_falls_back_to_DarkGlass()
    {
        var p = ThemePalette.Resolve(null!);
        var dark = ThemePalette.Resolve("DarkGlass");
        Assert.Equal(dark.Accent.Color, p.Accent.Color);
    }

    [Fact]
    public void Custom_hex_overrides_accent()
    {
        // #FF0000 should produce a fully-opaque red accent
        var p = ThemePalette.Resolve("DarkGlass", "#FF0000");
        Assert.Equal(0xFF, p.Accent.Color.A);
        Assert.Equal(0xFF, p.Accent.Color.R);
        Assert.Equal(0x00, p.Accent.Color.G);
        Assert.Equal(0x00, p.Accent.Color.B);
    }

    [Fact]
    public void Custom_AARRGGBB_hex_overrides_accent_with_alpha()
    {
        var p = ThemePalette.Resolve("DarkGlass", "#80AABBCC");
        Assert.Equal(0x80, p.Accent.Color.A);
        Assert.Equal(0xAA, p.Accent.Color.R);
        Assert.Equal(0xBB, p.Accent.Color.G);
        Assert.Equal(0xCC, p.Accent.Color.B);
    }

    [Fact]
    public void Invalid_hex_falls_back_to_preset_accent()
    {
        var preset = ThemePalette.Resolve("SportRed");
        var withBad = ThemePalette.Resolve("SportRed", "not-a-color");
        Assert.Equal(preset.Accent.Color, withBad.Accent.Color);
    }

    [Fact]
    public void Empty_custom_accent_uses_preset_accent()
    {
        var preset = ThemePalette.Resolve("CoolBlue");
        var withEmpty = ThemePalette.Resolve("CoolBlue", "");
        Assert.Equal(preset.Accent.Color, withEmpty.Accent.Color);
    }

    [Fact]
    public void Custom_accent_only_overrides_accent_not_surface()
    {
        var preset = ThemePalette.Resolve("DarkGlass");
        var withAccent = ThemePalette.Resolve("DarkGlass", "#FF0000");
        Assert.Equal(preset.Surface.Color, withAccent.Surface.Color);
        Assert.Equal(preset.Good.Color, withAccent.Good.Color);
    }

    [Fact]
    public void All_preset_names_are_resolvable()
    {
        foreach (var name in ThemePalette.PresetNames)
        {
            var p = ThemePalette.Resolve(name);
            Assert.NotNull(p.Accent);
            Assert.NotNull(p.Surface);
        }
    }

    [Fact]
    public void Returned_brushes_are_frozen()
    {
        var p = ThemePalette.Resolve("DarkGlass");
        Assert.True(p.Accent.IsFrozen);
        Assert.True(p.Surface.IsFrozen);
        Assert.True(p.Good.IsFrozen);
    }
}

public class OverlayConfigThemeNormalizeTests
{
    [Fact]
    public void Valid_preset_name_is_preserved()
    {
        var cfg = new OverlayConfig { ThemePreset = "SportRed" };
        cfg.Normalize(cfg.Layout);
        Assert.Equal("SportRed", cfg.ThemePreset);
    }

    [Fact]
    public void Unknown_preset_is_clamped_to_DarkGlass()
    {
        var cfg = new OverlayConfig { ThemePreset = "BogusTheme" };
        cfg.Normalize(cfg.Layout);
        Assert.Equal("DarkGlass", cfg.ThemePreset);
    }

    [Fact]
    public void Null_custom_accent_stays_null()
    {
        var cfg = new OverlayConfig { CustomAccent = null };
        cfg.Normalize(cfg.Layout);
        Assert.Null(cfg.CustomAccent);
    }

    [Fact]
    public void Valid_6char_custom_accent_is_preserved()
    {
        var cfg = new OverlayConfig { CustomAccent = "#AABB11" };
        cfg.Normalize(cfg.Layout);
        Assert.Equal("#AABB11", cfg.CustomAccent);
    }

    [Fact]
    public void Valid_8char_custom_accent_is_preserved()
    {
        var cfg = new OverlayConfig { CustomAccent = "#FFAABB11" };
        cfg.Normalize(cfg.Layout);
        Assert.Equal("#FFAABB11", cfg.CustomAccent);
    }

    [Fact]
    public void Invalid_custom_accent_is_cleared()
    {
        var cfg = new OverlayConfig { CustomAccent = "not-a-color" };
        cfg.Normalize(cfg.Layout);
        Assert.Null(cfg.CustomAccent);
    }

    [Fact]
    public void Short_hex_without_hash_is_cleared()
    {
        // "ABC" without # and only 3 chars is invalid
        var cfg = new OverlayConfig { CustomAccent = "ABC" };
        cfg.Normalize(cfg.Layout);
        Assert.Null(cfg.CustomAccent);
    }
}
