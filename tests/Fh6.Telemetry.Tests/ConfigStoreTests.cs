using Fh6.Telemetry.Overlay.Settings;
using Xunit;

namespace Fh6.Telemetry.Tests;

public class ConfigStoreTests
{
    [Fact]
    public void Save_then_load_round_trips_values()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fh6cfg-{Guid.NewGuid():N}.json");
        try
        {
            var config = new OverlayConfig
            {
                Port = 20555,
                ListenAddress = "127.0.0.1",
                Layout = OverlayLayout.CornerPanel,
                Opacity = 0.75,
                WindowLeft = 100,
                WindowTop = 200,
            };

            ConfigStore.Save(path, config);
            var loaded = ConfigStore.Load(path);

            Assert.Equal(20555, loaded.Port);
            Assert.Equal("127.0.0.1", loaded.ListenAddress);
            Assert.Equal(OverlayLayout.CornerPanel, loaded.Layout);
            Assert.Equal(0.75, loaded.Opacity);
            Assert.Equal(100, loaded.WindowLeft);
            Assert.Equal(200, loaded.WindowTop);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_missing_file_returns_defaults()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fh6cfg-missing-{Guid.NewGuid():N}.json");
        var loaded = ConfigStore.Load(path);

        Assert.Equal(20440, loaded.Port);
        Assert.Equal("0.0.0.0", loaded.ListenAddress);
        Assert.Equal(OverlayLayout.BottomStrip, loaded.Layout);
    }

    [Fact]
    public void Load_corrupt_file_returns_defaults()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fh6cfg-bad-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "{ not valid json");
        try
        {
            var loaded = ConfigStore.Load(path);
            Assert.Equal(20440, loaded.Port);
        }
        finally { File.Delete(path); }
    }
}
