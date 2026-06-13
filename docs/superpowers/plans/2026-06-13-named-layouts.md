# Named Layouts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add named layouts to the FH6 overlay so users can save, load, rename, and delete named snapshots of their widget arrangement from the Settings dialog.

**Architecture:** Extend `OverlayConfig` with a `SavedLayouts` dictionary and pure methods (`SaveLayoutAs`, `LoadLayout`, `DeleteLayout`, `RenameLayout`) that deep-copy widget configs so snapshots are immutable after saving. Wire a "Saved layouts" section into `SettingsWindow` (code-behind only, matching existing style) so that Load sets `DialogResult = true`, which re-uses the existing `OverlayWindow.OpenSettings` path to call `Normalize`, `ApplyConfig`, and `ConfigStore.Save`.

**Tech Stack:** C# 12 / .NET 8, WPF (code-behind only), System.Text.Json, xUnit 2.5.

---

## File Map

| File | Action | Responsibility |
|---|---|---|
| `src/Fh6.Telemetry.Overlay/Settings/OverlayConfig.cs` | Modify | Add `SavedLayout` class + `SavedLayouts` dict + 4 methods |
| `src/Fh6.Telemetry.Overlay/Settings/SettingsWindow.xaml` | Modify | Add "Saved layouts" group at bottom |
| `src/Fh6.Telemetry.Overlay/Settings/SettingsWindow.xaml.cs` | Modify | Wire up Saved layouts section, handle Save/Load/Rename/Delete buttons |
| `tests/Fh6.Telemetry.Tests/NamedLayoutTests.cs` | Create | Unit tests for all 4 methods + JSON round-trip |

---

## Task 1: Branch setup

**Files:**
- No code changes.

- [ ] **Step 1: Create the feature branch**

```bash
git checkout -b feat/named-layouts
```

Expected output:
```
Switched to a new branch 'feat/named-layouts'
```

---

## Task 2: Add `SavedLayout` class and `SavedLayouts` property to `OverlayConfig`

**Files:**
- Modify: `src/Fh6.Telemetry.Overlay/Settings/OverlayConfig.cs`

The `SavedLayout` class holds a snapshot of `Layout`, `Scale`, and `Widgets`. Adding it to `OverlayConfig` is the only data-model change — `ConfigStore` requires no changes because it just serializes `OverlayConfig` as-is via System.Text.Json, and `SavedLayouts` will serialize as a JSON object with string keys.

- [ ] **Step 1: Add `SavedLayout` sealed class and `SavedLayouts` property**

Open `src/Fh6.Telemetry.Overlay/Settings/OverlayConfig.cs`. The full file after edit:

```csharp
using Fh6.Telemetry.Overlay.Layouts;
using Fh6.Telemetry.Overlay.Widgets;

namespace Fh6.Telemetry.Overlay.Settings;

public enum OverlayLayout
{
    BottomStrip,
    CornerPanel,
    CenterDash,
}

/// <summary>
/// A named snapshot of the current widget arrangement.
/// </summary>
public sealed class SavedLayout
{
    public OverlayLayout BaseLayout { get; set; }
    public double Scale { get; set; } = 1.0;
    public Dictionary<string, WidgetConfig> Widgets { get; set; } = new();
}

public sealed class OverlayConfig
{
    public int Port { get; set; } = 20440;
    public string ListenAddress { get; set; } = "0.0.0.0";
    public OverlayLayout Layout { get; set; } = OverlayLayout.BottomStrip;
    public double Opacity { get; set; } = 0.9;
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double Scale { get; set; } = 1.0;

    /// <summary>
    /// Per-widget customization, keyed by <see cref="WidgetId.ToString()"/>.
    /// Absent keys mean "not yet customized"; <see cref="Normalize"/> fills them from the seed.
    /// </summary>
    public Dictionary<string, WidgetConfig> Widgets { get; set; } = new();

    /// <summary>
    /// User-named layout snapshots. Keyed by the user-chosen name.
    /// </summary>
    public Dictionary<string, SavedLayout> SavedLayouts { get; set; } = new();

    /// <summary>
    /// For each of the six <see cref="WidgetId"/> keys missing from <see cref="Widgets"/>,
    /// seeds from <see cref="LayoutSeeds.For(OverlayLayout)"/>.
    /// Keys that are already present are kept unchanged, except Scale is clamped to [0.5, 2.5].
    /// </summary>
    public void Normalize(OverlayLayout layout)
    {
        var seeds = LayoutSeeds.For(layout);

        // Clamp scale on any pre-existing entries
        foreach (var cfg in Widgets.Values)
            cfg.Scale = Math.Clamp(cfg.Scale, 0.5, 2.5);

        // Seed missing widget keys
        foreach (WidgetId id in Enum.GetValues<WidgetId>())
        {
            var key = id.ToString();
            if (!Widgets.ContainsKey(key))
            {
                var s = seeds[id];
                Widgets[key] = new WidgetConfig
                {
                    Visible = s.Visible,
                    X = s.X,
                    Y = s.Y,
                    Scale = s.Scale,
                };
            }
        }
    }

    /// <summary>
    /// Overwrites X/Y/Scale/Visible for all six widgets from the given seed dictionary.
    /// Does NOT touch Accent or Surface — color overrides survive a preset re-apply.
    /// </summary>
    public void ApplySeed(IReadOnlyDictionary<WidgetId, WidgetSeed> seed)
    {
        foreach (WidgetId id in Enum.GetValues<WidgetId>())
        {
            var key = id.ToString();
            var s = seed[id];

            if (!Widgets.TryGetValue(key, out var cfg))
            {
                cfg = new WidgetConfig();
                Widgets[key] = cfg;
            }

            cfg.X = s.X;
            cfg.Y = s.Y;
            cfg.Scale = s.Scale;
            cfg.Visible = s.Visible;
            // Accent and Surface are intentionally NOT touched.
        }
    }

    // ---- Named layout operations ----

    /// <summary>
    /// Snapshots current <see cref="Widgets"/>, <see cref="Scale"/>, and <see cref="Layout"/>
    /// into <see cref="SavedLayouts"/> under <paramref name="name"/>.
    /// Deep-copies every <see cref="WidgetConfig"/> so future edits do not mutate the snapshot.
    /// </summary>
    public void SaveLayoutAs(string name)
    {
        var snap = new SavedLayout
        {
            BaseLayout = Layout,
            Scale = Scale,
        };
        foreach (var (k, wc) in Widgets)
        {
            snap.Widgets[k] = new WidgetConfig
            {
                Visible = wc.Visible,
                X = wc.X,
                Y = wc.Y,
                Scale = wc.Scale,
                Accent = wc.Accent,
                Surface = wc.Surface,
            };
        }
        SavedLayouts[name] = snap;
    }

    /// <summary>
    /// Replaces current <see cref="Widgets"/>, <see cref="Scale"/>, and <see cref="Layout"/>
    /// from the named snapshot, then calls <see cref="Normalize"/> to fill any widget ids
    /// that were added to the enum after the snapshot was taken.
    /// Returns <c>true</c> on success; <c>false</c> if <paramref name="name"/> is not found.
    /// </summary>
    public bool LoadLayout(string name)
    {
        if (!SavedLayouts.TryGetValue(name, out var snap))
            return false;

        Layout = snap.BaseLayout;
        Scale = snap.Scale;
        Widgets = new Dictionary<string, WidgetConfig>();
        foreach (var (k, wc) in snap.Widgets)
        {
            Widgets[k] = new WidgetConfig
            {
                Visible = wc.Visible,
                X = wc.X,
                Y = wc.Y,
                Scale = wc.Scale,
                Accent = wc.Accent,
                Surface = wc.Surface,
            };
        }
        Normalize(Layout);
        return true;
    }

    /// <summary>
    /// Removes the named layout. Returns <c>true</c> if it existed.
    /// </summary>
    public bool DeleteLayout(string name) => SavedLayouts.Remove(name);

    /// <summary>
    /// Renames a saved layout from <paramref name="from"/> to <paramref name="to"/>.
    /// If <paramref name="from"/> does not exist, returns <c>false</c>.
    /// If <paramref name="to"/> already exists it is overwritten.
    /// </summary>
    public bool RenameLayout(string from, string to)
    {
        if (!SavedLayouts.TryGetValue(from, out var snap))
            return false;
        SavedLayouts.Remove(from);
        SavedLayouts[to] = snap;
        return true;
    }
}
```

- [ ] **Step 2: Build to verify no compile errors**

```bash
dotnet build "P:/Projects/fh6-telemetry/src/Fh6.Telemetry.Overlay/Fh6.Telemetry.Overlay.csproj" --no-incremental
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add "src/Fh6.Telemetry.Overlay/Settings/OverlayConfig.cs"
git commit -m "Add SavedLayout model and SavedLayouts dictionary to OverlayConfig"
```

---

## Task 3: Write unit tests for the named-layout methods

**Files:**
- Create: `tests/Fh6.Telemetry.Tests/NamedLayoutTests.cs`

Write the tests first (TDD). They will compile but all pass once Task 2 is done since the model code is already in place.

- [ ] **Step 1: Create `NamedLayoutTests.cs`**

```csharp
using Fh6.Telemetry.Overlay.Settings;
using Fh6.Telemetry.Overlay.Widgets;

namespace Fh6.Telemetry.Tests;

public class NamedLayoutTests
{
    // ---- Helper: build a config with one normalised widget set ----
    private static OverlayConfig MakeConfig(OverlayLayout layout = OverlayLayout.BottomStrip)
    {
        var cfg = new OverlayConfig { Layout = layout, Scale = 1.5 };
        cfg.Normalize(layout);
        return cfg;
    }

    // ---- SaveLayoutAs ----

    [Fact]
    public void SaveLayoutAs_creates_entry_in_SavedLayouts()
    {
        var cfg = MakeConfig();
        cfg.SaveLayoutAs("race");

        Assert.True(cfg.SavedLayouts.ContainsKey("race"));
    }

    [Fact]
    public void SaveLayoutAs_snapshots_Scale_and_BaseLayout()
    {
        var cfg = MakeConfig(OverlayLayout.CornerPanel);
        cfg.Scale = 1.75;
        cfg.SaveLayoutAs("snap");

        var saved = cfg.SavedLayouts["snap"];
        Assert.Equal(1.75, saved.Scale);
        Assert.Equal(OverlayLayout.CornerPanel, saved.BaseLayout);
    }

    [Fact]
    public void SaveLayoutAs_deep_copies_widgets_so_later_mutation_does_not_affect_snapshot()
    {
        var cfg = MakeConfig();
        // Give one widget a known position.
        cfg.Widgets[WidgetId.Gear.ToString()].X = 100;
        cfg.SaveLayoutAs("snap");

        // Mutate the live config after saving.
        cfg.Widgets[WidgetId.Gear.ToString()].X = 999;

        // Snapshot must still have the original value.
        Assert.Equal(100, cfg.SavedLayouts["snap"].Widgets[WidgetId.Gear.ToString()].X);
    }

    [Fact]
    public void SaveLayoutAs_overwrites_existing_entry()
    {
        var cfg = MakeConfig();
        cfg.Scale = 1.0;
        cfg.SaveLayoutAs("snap");

        cfg.Scale = 2.0;
        cfg.SaveLayoutAs("snap");

        Assert.Equal(2.0, cfg.SavedLayouts["snap"].Scale);
    }

    // ---- LoadLayout ----

    [Fact]
    public void LoadLayout_returns_false_for_unknown_name()
    {
        var cfg = MakeConfig();
        Assert.False(cfg.LoadLayout("nonexistent"));
    }

    [Fact]
    public void LoadLayout_returns_true_and_restores_Scale_and_Layout()
    {
        var cfg = MakeConfig(OverlayLayout.BottomStrip);
        cfg.Scale = 1.5;
        cfg.SaveLayoutAs("snap");

        cfg.Scale = 2.0;
        cfg.Layout = OverlayLayout.CenterDash;

        var result = cfg.LoadLayout("snap");

        Assert.True(result);
        Assert.Equal(1.5, cfg.Scale);
        Assert.Equal(OverlayLayout.BottomStrip, cfg.Layout);
    }

    [Fact]
    public void LoadLayout_replaces_Widgets_with_deep_copy_of_saved_set()
    {
        var cfg = MakeConfig();
        cfg.Widgets[WidgetId.Gear.ToString()].X = 42;
        cfg.SaveLayoutAs("snap");

        // Change live config.
        cfg.Widgets[WidgetId.Gear.ToString()].X = 0;

        cfg.LoadLayout("snap");

        Assert.Equal(42, cfg.Widgets[WidgetId.Gear.ToString()].X);
    }

    [Fact]
    public void LoadLayout_deep_copies_so_post_load_mutation_does_not_corrupt_snapshot()
    {
        var cfg = MakeConfig();
        cfg.Widgets[WidgetId.Speed.ToString()].X = 200;
        cfg.SaveLayoutAs("snap");

        cfg.LoadLayout("snap");

        // Mutate live config after loading.
        cfg.Widgets[WidgetId.Speed.ToString()].X = 999;

        // Saved snapshot must still have the original value.
        Assert.Equal(200, cfg.SavedLayouts["snap"].Widgets[WidgetId.Speed.ToString()].X);
    }

    [Fact]
    public void LoadLayout_calls_Normalize_so_new_widget_ids_are_seeded()
    {
        // Save a snapshot that has NO entry for PowerTorque (simulate old saved layout).
        var cfg = MakeConfig();
        cfg.SaveLayoutAs("old");
        cfg.SavedLayouts["old"].Widgets.Remove(WidgetId.PowerTorque.ToString());

        cfg.LoadLayout("old");

        // After loading, Normalize should have inserted the missing widget.
        Assert.True(cfg.Widgets.ContainsKey(WidgetId.PowerTorque.ToString()));
    }

    // ---- DeleteLayout ----

    [Fact]
    public void DeleteLayout_removes_existing_entry_and_returns_true()
    {
        var cfg = MakeConfig();
        cfg.SaveLayoutAs("to-delete");

        Assert.True(cfg.DeleteLayout("to-delete"));
        Assert.False(cfg.SavedLayouts.ContainsKey("to-delete"));
    }

    [Fact]
    public void DeleteLayout_returns_false_for_unknown_name()
    {
        var cfg = MakeConfig();
        Assert.False(cfg.DeleteLayout("ghost"));
    }

    // ---- RenameLayout ----

    [Fact]
    public void RenameLayout_returns_false_for_unknown_from_name()
    {
        var cfg = MakeConfig();
        Assert.False(cfg.RenameLayout("ghost", "new"));
    }

    [Fact]
    public void RenameLayout_moves_entry_to_new_key()
    {
        var cfg = MakeConfig();
        cfg.Scale = 1.8;
        cfg.SaveLayoutAs("old-name");

        Assert.True(cfg.RenameLayout("old-name", "new-name"));
        Assert.False(cfg.SavedLayouts.ContainsKey("old-name"));
        Assert.True(cfg.SavedLayouts.ContainsKey("new-name"));
        Assert.Equal(1.8, cfg.SavedLayouts["new-name"].Scale);
    }

    [Fact]
    public void RenameLayout_to_existing_name_overwrites_target()
    {
        var cfg = MakeConfig();
        cfg.Scale = 1.0;
        cfg.SaveLayoutAs("a");
        cfg.Scale = 2.0;
        cfg.SaveLayoutAs("b");

        cfg.RenameLayout("b", "a");

        Assert.Equal(2.0, cfg.SavedLayouts["a"].Scale);
        Assert.False(cfg.SavedLayouts.ContainsKey("b"));
    }

    // ---- JSON round-trip ----

    [Fact]
    public void ConfigStore_round_trips_SavedLayouts()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fh6cfg-layouts-{Guid.NewGuid():N}.json");
        try
        {
            var cfg = MakeConfig(OverlayLayout.CornerPanel);
            cfg.Scale = 1.6;
            cfg.Widgets[WidgetId.Gear.ToString()].X = 77;
            cfg.SaveLayoutAs("race");

            ConfigStore.Save(path, cfg);
            var loaded = ConfigStore.Load(path);

            Assert.True(loaded.SavedLayouts.ContainsKey("race"));
            var snap = loaded.SavedLayouts["race"];
            Assert.Equal(1.6, snap.Scale);
            Assert.Equal(OverlayLayout.CornerPanel, snap.BaseLayout);
            Assert.Equal(77, snap.Widgets[WidgetId.Gear.ToString()].X);
        }
        finally { File.Delete(path); }
    }
}
```

- [ ] **Step 2: Run the tests**

```bash
dotnet test "P:/Projects/fh6-telemetry/tests/Fh6.Telemetry.Tests/Fh6.Telemetry.Tests.csproj" --filter "FullyQualifiedName~NamedLayoutTests" -v minimal
```

Expected: All `NamedLayoutTests` pass. (They should pass immediately since the model code was written in Task 2.)

- [ ] **Step 3: Run the full test suite to verify no regressions**

```bash
dotnet test "P:/Projects/fh6-telemetry/tests/Fh6.Telemetry.Tests/Fh6.Telemetry.Tests.csproj" -v minimal
```

Expected: All tests pass (50 existing + the new ones).

- [ ] **Step 4: Commit**

```bash
git add "tests/Fh6.Telemetry.Tests/NamedLayoutTests.cs"
git commit -m "Add NamedLayoutTests covering save/load/rename/delete and JSON round-trip"
```

---

## Task 4: Add the "Saved layouts" section to `SettingsWindow.xaml`

**Files:**
- Modify: `src/Fh6.Telemetry.Overlay/Settings/SettingsWindow.xaml`

Add a new `GroupBox` for saved layouts below the widget rows, before the Apply/Cancel buttons. The `ComboBox` (`LayoutList`), name `TextBox` (`LayoutNameBox`), and buttons (`SaveLayoutBtn`, `LoadLayoutBtn`, `RenameLayoutBtn`, `DeleteLayoutBtn`) are wired in code-behind.

- [ ] **Step 1: Insert the Saved layouts GroupBox into the XAML**

The full `SettingsWindow.xaml` after edit:

```xml
<Window x:Class="Fh6.Telemetry.Overlay.Settings.SettingsWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:s="clr-namespace:Fh6.Telemetry.Overlay.Settings"
        Title="FH6 Overlay Settings" Width="320" SizeToContent="Height"
        WindowStartupLocation="CenterOwner" ResizeMode="NoResize">
    <StackPanel Margin="14">
        <TextBlock Text="Port"/>
        <TextBox x:Name="PortBox" Margin="0,2,0,10"/>

        <TextBlock Text="Listen address"/>
        <TextBox x:Name="AddressBox" Margin="0,2,0,2"/>
        <TextBlock Text="0.0.0.0 = all interfaces (recommended). 127.0.0.1 = loopback only."
                   FontSize="9" Foreground="#99000000" Margin="0,0,0,10" TextWrapping="Wrap"/>

        <TextBlock Text="Layout"/>
        <ComboBox x:Name="LayoutBox" Margin="0,2,0,10"/>

        <TextBlock Text="Opacity"/>
        <Slider x:Name="OpacitySlider" Minimum="0.2" Maximum="1.0" TickFrequency="0.05"
                IsSnapToTickEnabled="True" Margin="0,2,0,10"/>

        <TextBlock Text="HUD scale"/>
        <Slider x:Name="HudScaleSlider" Minimum="0.5" Maximum="2.5" TickFrequency="0.05"
                IsSnapToTickEnabled="True" Margin="0,2,0,14"/>

        <TextBlock Text="Widgets" Margin="0,0,0,4"/>
        <ScrollViewer MaxHeight="220" VerticalScrollBarVisibility="Auto" Margin="0,0,0,14">
            <StackPanel x:Name="WidgetRows"/>
        </ScrollViewer>

        <GroupBox Header="Saved layouts" Margin="0,0,0,14" Padding="6">
            <StackPanel>
                <ComboBox x:Name="LayoutList" Margin="0,0,0,6"/>
                <TextBox x:Name="LayoutNameBox" Margin="0,0,0,6"
                         ToolTip="Name for save / rename target"/>
                <WrapPanel>
                    <Button x:Name="SaveLayoutBtn"   Content="Save"   Width="60" Margin="0,0,4,4"
                            Click="OnSaveLayout"/>
                    <Button x:Name="LoadLayoutBtn"   Content="Load"   Width="60" Margin="0,0,4,4"
                            Click="OnLoadLayout"/>
                    <Button x:Name="RenameLayoutBtn" Content="Rename" Width="60" Margin="0,0,4,4"
                            Click="OnRenameLayout"/>
                    <Button x:Name="DeleteLayoutBtn" Content="Delete" Width="60" Margin="0,0,4,4"
                            Click="OnDeleteLayout"/>
                </WrapPanel>
            </StackPanel>
        </GroupBox>

        <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
            <Button Content="Cancel" Width="70" Margin="0,0,8,0" IsCancel="True"/>
            <Button Content="Apply" Width="70" Click="OnApply" IsDefault="True"/>
        </StackPanel>
    </StackPanel>
</Window>
```

- [ ] **Step 2: Build overlay project to detect XAML parse errors**

```bash
dotnet build "P:/Projects/fh6-telemetry/src/Fh6.Telemetry.Overlay/Fh6.Telemetry.Overlay.csproj" --no-incremental
```

Expected: `Build succeeded.` (code-behind handlers don't exist yet, so this will fail — proceed to Task 5 immediately if it does).

**Note:** The build will fail because the four click handlers (`OnSaveLayout`, `OnLoadLayout`, `OnRenameLayout`, `OnDeleteLayout`) are referenced in XAML but not yet in code-behind. That's expected — implement Task 5 next without committing this file alone.

---

## Task 5: Wire the Saved layouts section in `SettingsWindow.xaml.cs`

**Files:**
- Modify: `src/Fh6.Telemetry.Overlay/Settings/SettingsWindow.xaml.cs`

Key design decisions:
- `RefreshLayoutList()` repopulates `LayoutList.ItemsSource` from `_config.SavedLayouts.Keys` (sorted) and re-selects the previously selected item if it still exists.
- **Load** writes back the form state to `_config` (same as `OnApply`), calls `_config.LoadLayout(selected)`, then sets `DialogResult = true` and closes — this re-enters the same `OverlayWindow.OpenSettings` path that calls `Normalize` + `ApplyConfig` + `Save`.
- **Save** calls `_config.SaveLayoutAs(name)` and refreshes the list; it does NOT close the dialog.
- **Rename** calls `_config.RenameLayout(oldName, newName)` and refreshes.
- **Delete** calls `_config.DeleteLayout(selected)` and refreshes.

- [ ] **Step 1: Replace SettingsWindow.xaml.cs with the full implementation**

```csharp
using System.Windows;
using System.Windows.Controls;
using Fh6.Telemetry.Overlay.Widgets;

namespace Fh6.Telemetry.Overlay.Settings;

public partial class SettingsWindow : Window
{
    private readonly OverlayConfig _config;

    private sealed record WidgetRow(CheckBox VisibleBox, Slider ScaleSlider, WidgetId Id);
    private readonly List<WidgetRow> _widgetRows = new();

    public SettingsWindow(OverlayConfig config)
    {
        InitializeComponent();
        _config = config;

        PortBox.Text = config.Port.ToString();
        AddressBox.Text = config.ListenAddress;
        LayoutBox.ItemsSource = Enum.GetValues(typeof(OverlayLayout));
        LayoutBox.SelectedItem = config.Layout;
        OpacitySlider.Value = config.Opacity;
        HudScaleSlider.Value = config.Scale;

        // Ensure all widget keys exist before reading them.
        config.Normalize(config.Layout);

        foreach (WidgetId id in Enum.GetValues<WidgetId>())
        {
            var wc = _config.Widgets[id.ToString()];

            var nameBlock = new TextBlock
            {
                Text = id.ToString(),
                Width = 90,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var visibleBox = new CheckBox
            {
                IsChecked = wc.Visible,
                Content = "Visible",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 8, 0),
            };

            var scaleLabel = new TextBlock
            {
                Text = "Scale",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0),
            };

            var scaleSlider = new Slider
            {
                Minimum = 0.5,
                Maximum = 2.5,
                TickFrequency = 0.05,
                IsSnapToTickEnabled = true,
                Value = wc.Scale,
                Width = 90,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 4),
            };
            row.Children.Add(nameBlock);
            row.Children.Add(visibleBox);
            row.Children.Add(scaleLabel);
            row.Children.Add(scaleSlider);

            WidgetRows.Children.Add(row);
            _widgetRows.Add(new WidgetRow(visibleBox, scaleSlider, id));
        }

        RefreshLayoutList();
    }

    // ---- Existing apply path ----

    private void OnApply(object sender, RoutedEventArgs e)
    {
        WriteFormToConfig();
        DialogResult = true;
        Close();
    }

    // ---- Saved layouts section ----

    private void RefreshLayoutList()
    {
        var previous = LayoutList.SelectedItem as string;
        LayoutList.ItemsSource = _config.SavedLayouts.Keys.OrderBy(k => k).ToList();
        if (previous is not null && _config.SavedLayouts.ContainsKey(previous))
            LayoutList.SelectedItem = previous;
        else if (LayoutList.Items.Count > 0)
            LayoutList.SelectedIndex = 0;
    }

    private void OnSaveLayout(object sender, RoutedEventArgs e)
    {
        var name = LayoutNameBox.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            MessageBox.Show("Enter a name before saving.", "Saved layouts",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Snapshot the live config state (but do NOT close the dialog).
        WriteFormToConfig();
        _config.SaveLayoutAs(name);
        RefreshLayoutList();
        LayoutList.SelectedItem = name;
    }

    private void OnLoadLayout(object sender, RoutedEventArgs e)
    {
        var name = LayoutList.SelectedItem as string;
        if (name is null)
        {
            MessageBox.Show("Select a layout to load.", "Saved layouts",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Write current form fields first (port/address survive the load).
        WriteFormToConfig();
        _config.LoadLayout(name);

        // Close with DialogResult = true so OverlayWindow re-runs ApplyConfig + Save.
        DialogResult = true;
        Close();
    }

    private void OnRenameLayout(object sender, RoutedEventArgs e)
    {
        var oldName = LayoutList.SelectedItem as string;
        var newName = LayoutNameBox.Text.Trim();

        if (oldName is null)
        {
            MessageBox.Show("Select a layout to rename.", "Saved layouts",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (string.IsNullOrEmpty(newName))
        {
            MessageBox.Show("Enter the new name in the text box.", "Saved layouts",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _config.RenameLayout(oldName, newName);
        RefreshLayoutList();
        LayoutList.SelectedItem = newName;
    }

    private void OnDeleteLayout(object sender, RoutedEventArgs e)
    {
        var name = LayoutList.SelectedItem as string;
        if (name is null)
        {
            MessageBox.Show("Select a layout to delete.", "Saved layouts",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show($"Delete layout \"{name}\"?", "Saved layouts",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        _config.DeleteLayout(name);
        RefreshLayoutList();
    }

    // ---- Private helpers ----

    /// <summary>
    /// Writes the current form control values back into <see cref="_config"/>.
    /// Called by OnApply, OnSaveLayout, and OnLoadLayout before acting.
    /// </summary>
    private void WriteFormToConfig()
    {
        if (int.TryParse(PortBox.Text, out var port))
            _config.Port = port;
        _config.ListenAddress = AddressBox.Text.Trim();
        if (LayoutBox.SelectedItem is OverlayLayout layout)
            _config.Layout = layout;
        _config.Opacity = OpacitySlider.Value;
        _config.Scale = HudScaleSlider.Value;

        foreach (var row in _widgetRows)
        {
            var wc = _config.Widgets[row.Id.ToString()];
            wc.Visible = row.VisibleBox.IsChecked == true;
            wc.Scale = Math.Clamp(row.ScaleSlider.Value, 0.5, 2.5);
        }
    }
}
```

- [ ] **Step 2: Build overlay project**

```bash
dotnet build "P:/Projects/fh6-telemetry/src/Fh6.Telemetry.Overlay/Fh6.Telemetry.Overlay.csproj" --no-incremental
```

Expected: `Build succeeded.`

- [ ] **Step 3: Run full test suite**

```bash
dotnet test "P:/Projects/fh6-telemetry/tests/Fh6.Telemetry.Tests/Fh6.Telemetry.Tests.csproj" -v minimal
```

Expected: All tests pass.

- [ ] **Step 4: Commit both XAML and code-behind together**

```bash
git add "src/Fh6.Telemetry.Overlay/Settings/SettingsWindow.xaml" \
        "src/Fh6.Telemetry.Overlay/Settings/SettingsWindow.xaml.cs"
git commit -m "Add Saved layouts section to SettingsWindow (save/load/rename/delete)"
```

---

## Task 6: Merge to main and delete feature branch

- [ ] **Step 1: Switch to main**

```bash
git checkout main
```

- [ ] **Step 2: Merge with --no-ff**

```bash
git merge --no-ff feat/named-layouts -m "Add named layouts: save, load, rename, delete"
```

- [ ] **Step 3: Delete the feature branch**

```bash
git branch -d feat/named-layouts
```

- [ ] **Step 4: Confirm final state**

```bash
git log --oneline -6
```

Expected output (most recent first):
```
<hash> Add named layouts: save, load, rename, delete
<hash> Add Saved layouts section to SettingsWindow (save/load/rename/delete)
<hash> Add NamedLayoutTests covering save/load/rename/delete and JSON round-trip
<hash> Add SavedLayout model and SavedLayouts dictionary to OverlayConfig
<hash> Branch setup: feat/named-layouts   ← (not present if no setup commit was made)
```

---

## Self-Review

### Spec coverage

| Requirement | Task |
|---|---|
| `SavedLayout` class with `BaseLayout`, `Scale`, `Widgets` | Task 2 |
| `SavedLayouts` dictionary on `OverlayConfig` | Task 2 |
| `SaveLayoutAs` deep-copy | Task 2 |
| `LoadLayout` deep-copy + `Normalize` | Task 2 |
| `DeleteLayout` | Task 2 |
| `RenameLayout` | Task 2 |
| JSON round-trip via `ConfigStore` | Task 3 |
| Deep-copy isolation tests | Task 3 |
| `SettingsWindow` Saved layouts section | Tasks 4+5 |
| Save, Load, Rename, Delete buttons | Task 5 |
| Load triggers Apply path (DialogResult = true) | Task 5 |
| Branch `feat/named-layouts`, merge `--no-ff`, delete | Tasks 1+6 |

### Placeholder scan

No TBD, TODO, or vague steps. Every code block is complete.

### Type consistency

- `SavedLayout.BaseLayout` (Task 2) is referenced by name consistently in tests (Task 3) and in code-behind (Task 5).
- `WriteFormToConfig()` is a private helper (Task 5) called in `OnApply`, `OnSaveLayout`, and `OnLoadLayout` — all within the same class.
- `RefreshLayoutList()` is called from constructor and all four handlers.

### Load→Apply wiring concern

`OnLoadLayout` calls `WriteFormToConfig()` (so port/address/opacity changes in the dialog survive), then `_config.LoadLayout(name)` (which replaces `Widgets`, `Scale`, `Layout`), then sets `DialogResult = true` and closes. `OverlayWindow.OpenSettings` then runs:
```csharp
Opacity = Math.Clamp(_config.Opacity, 0.2, 1.0);
_config.Normalize(_config.Layout);
FreeLayoutHost.ApplyConfig(_config);
ConfigStore.Save(ConfigStore.DefaultPath, _config);
SettingsApplied?.Invoke(this, EventArgs.Empty);
```
This is correct — `_config.LoadLayout` already calls `Normalize` internally, and `OverlayWindow` calls it again (harmless, idempotent). `ApplyConfig` re-positions all widgets from the now-loaded state. The save persists the change. No additional wiring is needed.
