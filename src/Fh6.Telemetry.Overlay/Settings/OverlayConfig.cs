namespace Fh6.Telemetry.Overlay.Settings;

public enum OverlayLayout
{
    BottomStrip,
    CornerPanel,
    CenterDash,
}

public sealed class OverlayConfig
{
    public int Port { get; set; } = 20440;
    public string ListenAddress { get; set; } = "0.0.0.0";
    public OverlayLayout Layout { get; set; } = OverlayLayout.BottomStrip;
    public double Opacity { get; set; } = 0.9;
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
}
