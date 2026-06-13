namespace Fh6.Telemetry.Overlay.Settings;

public sealed class WidgetConfig
{
    public bool Visible { get; set; } = true;

    /// <summary>Absolute position inside the overlay Canvas, in DIPs. Null => use seed position.</summary>
    public double? X { get; set; }
    public double? Y { get; set; }

    /// <summary>Uniform scale applied via ScaleTransform. 1.0 = design size. Clamped 0.5..2.5 on Normalize.</summary>
    public double Scale { get; set; } = 1.0;

    /// <summary>Value text / progress foreground accent as "#AARRGGBB". Null => widget theme default.</summary>
    public string? Accent { get; set; }

    /// <summary>Widget background (Border.Background) as "#AARRGGBB". Null => widget theme default.</summary>
    public string? Surface { get; set; }
}
