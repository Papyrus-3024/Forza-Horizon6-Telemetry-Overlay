# Expand Widgets (mph, G-force, Power/Torque) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add SpeedMph, G-force traction-circle, and Power/Torque widgets to the FH6 telemetry overlay, matching existing widget styling.

**Architecture:** Extend `TelemetryReadout` with computed conversion properties, wire them through `TelemetryViewModel`, update `SpeedWidget` XAML for dual units, create `GForceWidget` (Canvas-based traction circle with DependencyProperties) and `PowerTorqueWidget` (simple label panel), then register both in `WidgetId`, `FreeLayout`, and `LayoutSeeds`. TDD throughout — tests must fail before implementation exists.

**Tech Stack:** C# 12, .NET 8, WPF (UserControl / DependencyProperty pattern), xUnit, `dotnet build` / `dotnet test`.

---

## File Map

| File | Action | Purpose |
|------|--------|---------|
| `src/Fh6.Telemetry.Core/TelemetryReadout.cs` | Modify | Add SpeedMph, LatG, LongG, VertG, PowerHp, TorqueLbFt computed properties |
| `tests/Fh6.Telemetry.Tests/TelemetryReadoutTests.cs` | Modify | Add tests for the new computed properties |
| `src/Fh6.Telemetry.Overlay/ViewModels/TelemetryViewModel.cs` | Modify | Add SpeedMph, GLat, GLong, GText, PowerHp, TorqueLbFt properties + BoundNames |
| `src/Fh6.Telemetry.Overlay/Widgets/SpeedWidget.xaml` | Modify | Show mph large + km/h small beneath |
| `src/Fh6.Telemetry.Overlay/Widgets/GForceWidget.xaml` | Create | Traction-circle Canvas layout |
| `src/Fh6.Telemetry.Overlay/Widgets/GForceWidget.xaml.cs` | Create | DependencyProperties LatG/LongG + Redraw() logic |
| `src/Fh6.Telemetry.Overlay/Widgets/PowerTorqueWidget.xaml` | Create | Simple label panel for power + torque |
| `src/Fh6.Telemetry.Overlay/Widgets/PowerTorqueWidget.xaml.cs` | Create | Minimal code-behind |
| `src/Fh6.Telemetry.Overlay/Widgets/WidgetId.cs` | Modify | Add GForce and PowerTorque enum values |
| `src/Fh6.Telemetry.Overlay/Layouts/FreeLayout.xaml.cs` | Modify | Instantiate and add the two new widgets |
| `src/Fh6.Telemetry.Overlay/Layouts/LayoutSeeds.cs` | Modify | Add seed positions for GForce and PowerTorque in all three presets |

---

## Task 1: Git branch setup

**Files:**
- (git operations only)

- [ ] **Step 1: Create and switch to feature branch**

```bash
cd P:/Projects/fh6-telemetry
git checkout -b feat/expand-widgets
```

Expected: `Switched to a new branch 'feat/expand-widgets'`

---

## Task 2: Write failing tests for TelemetryReadout computed properties

**Files:**
- Modify: `tests/Fh6.Telemetry.Tests/TelemetryReadoutTests.cs`

- [ ] **Step 1: Add the failing test methods**

Open `tests/Fh6.Telemetry.Tests/TelemetryReadoutTests.cs` and append these test methods inside the `TelemetryReadoutTests` class (before the closing `}`). The `DrivingReadout()` helper and `DrivingFrameB64` constant already exist — do not duplicate them.

```csharp
[Fact]
public void SpeedMph_converts_from_driving_frame()
{
    // Real frame Speed = 10.3697 m/s → 10.3697 * 2.2369363 ≈ 23.2 mph
    var r = DrivingReadout();
    Assert.Equal(23.2, r.SpeedMph, 1);
}

[Fact]
public void LatG_and_LongG_from_synthetic_packet()
{
    // Acceleration X=9.80665 → LatG=1.0; Z=19.6133 → LongG=2.0
    var packet = new TelemetryPacket
    {
        Acceleration = new Vec3(9.80665f, 0f, 19.6133f),
    };
    var r = new TelemetryReadout(packet);
    Assert.Equal(1.0, r.LatG, 2);
    Assert.Equal(2.0, r.LongG, 2);
}

[Fact]
public void PowerHp_converts_watts_to_horsepower()
{
    // 745699.9 W / 745.6999 ≈ 1000 hp
    var packet = new TelemetryPacket { Power = 745699.9f };
    var r = new TelemetryReadout(packet);
    Assert.Equal(1000, r.PowerHp, 0);
}
```

- [ ] **Step 2: Run the tests to confirm they fail**

```bash
cd P:/Projects/fh6-telemetry
dotnet test tests/Fh6.Telemetry.Tests --no-build 2>&1 || dotnet test tests/Fh6.Telemetry.Tests
```

Expected: build may succeed but all three new test methods fail with something like `'TelemetryReadout' does not contain a definition for 'SpeedMph'` (compile error counts as failure).

---

## Task 3: Implement TelemetryReadout computed properties

**Files:**
- Modify: `src/Fh6.Telemetry.Core/TelemetryReadout.cs`

Note: `TelemetryReadout` is a `readonly struct`. The new properties are expression-bodied and delegate to existing backing fields set in the constructor (`SpeedMs` for speed; `Acceleration` is not currently stored — see step below).

- [ ] **Step 1: Store Acceleration in the constructor**

The constructor currently does not capture `p.Acceleration`. Add a backing field and property for it. In `TelemetryReadout.cs`:

In the constructor `public TelemetryReadout(in TelemetryPacket p)`, add:
```csharp
Acceleration = p.Acceleration;
Power = p.Power;
Torque = p.Torque;
```

And add the corresponding public properties at the bottom of the property list:
```csharp
public Vec3 Acceleration { get; }
public float Power { get; }
public float Torque { get; }
```

- [ ] **Step 2: Add the computed conversion properties**

Append these computed properties after the raw properties added in Step 1:

```csharp
// Unit conversions
public float SpeedMph  => SpeedMs * 2.2369363f;
public float LatG      => Acceleration.X / 9.80665f;
public float LongG     => Acceleration.Z / 9.80665f;
public float VertG     => Acceleration.Y / 9.80665f;
public float PowerHp   => Power / 745.6999f;
public float TorqueLbFt => Torque * 0.7375621f;
```

- [ ] **Step 3: Run the tests to confirm all pass**

```bash
cd P:/Projects/fh6-telemetry
dotnet test tests/Fh6.Telemetry.Tests
```

Expected output: all tests pass (the original ~7 tests plus the 3 new ones = ~10 total). Zero failures.

- [ ] **Step 4: Commit**

```bash
cd P:/Projects/fh6-telemetry
git add src/Fh6.Telemetry.Core/TelemetryReadout.cs tests/Fh6.Telemetry.Tests/TelemetryReadoutTests.cs
git commit -m "feat(core): add SpeedMph, G-force, PowerHp, TorqueLbFt computed properties to TelemetryReadout"
```

---

## Task 4: Add new ViewModel properties

**Files:**
- Modify: `src/Fh6.Telemetry.Overlay/ViewModels/TelemetryViewModel.cs`

- [ ] **Step 1: Add property declarations**

After the existing `public string Speed { get; set; } = "0";` line, add:

```csharp
public string SpeedMph { get; set; } = "0";
```

After the `public string Boost { get; set; } = "0.0";` line, add the G-force and power/torque properties:

```csharp
public double GLat { get; set; }
public double GLong { get; set; }
public string GText { get; set; } = "0.0g";
public string PowerHp { get; set; } = "0";
public string TorqueLbFt { get; set; } = "0";
```

- [ ] **Step 2: Populate them in `Update`**

In the `Update(in TelemetryReadout r)` method, after `Speed = $"{r.SpeedKmh:F0}";`, add:

```csharp
SpeedMph = $"{r.SpeedMph:F0}";
```

After `Boost = $"{r.Boost:F1}";`, add:

```csharp
GLat        = r.LatG;
GLong       = r.LongG;
GText       = $"{Math.Sqrt(r.LatG * r.LatG + r.LongG * r.LongG):F1}g";
PowerHp     = $"{r.PowerHp:F0}";
TorqueLbFt  = $"{r.TorqueLbFt:F0}";
```

- [ ] **Step 3: Register in BoundNames**

In the `BoundNames` array at the bottom of the file, add the new property names. The current last line is:

```csharp
nameof(Light1), nameof(Light2), nameof(Light3), nameof(Light4), nameof(Light5),
```

Change it to:

```csharp
nameof(Light1), nameof(Light2), nameof(Light3), nameof(Light4), nameof(Light5),
nameof(SpeedMph), nameof(GLat), nameof(GLong), nameof(GText),
nameof(PowerHp), nameof(TorqueLbFt),
```

- [ ] **Step 4: Build to confirm no errors**

```bash
cd P:/Projects/fh6-telemetry
dotnet build src/Fh6.Telemetry.Overlay
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
cd P:/Projects/fh6-telemetry
git add src/Fh6.Telemetry.Overlay/ViewModels/TelemetryViewModel.cs
git commit -m "feat(overlay): add SpeedMph, GLat/GLong, GText, PowerHp, TorqueLbFt to TelemetryViewModel"
```

---

## Task 5: Update SpeedWidget for dual units

**Files:**
- Modify: `src/Fh6.Telemetry.Overlay/Widgets/SpeedWidget.xaml`

The current XAML shows km/h only. Replace with mph large + km/h small:

- [ ] **Step 1: Edit SpeedWidget.xaml**

Replace the entire file content with:

```xml
<UserControl x:Class="Fh6.Telemetry.Overlay.Widgets.SpeedWidget"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Border Background="#A6080A08" CornerRadius="6" Padding="8,4">
        <StackPanel>
            <TextBlock Text="MPH" Foreground="#99FFFFFF" FontSize="9" FontFamily="Consolas"/>
            <TextBlock Text="{Binding SpeedMph}" Foreground="White" FontSize="30" FontWeight="Bold" FontFamily="Consolas"/>
            <TextBlock Text="{Binding Speed}" Foreground="#99FFFFFF" FontSize="11" FontFamily="Consolas"/>
            <TextBlock Text="km/h" Foreground="#66FFFFFF" FontSize="8" FontFamily="Consolas"/>
        </StackPanel>
    </Border>
</UserControl>
```

- [ ] **Step 2: Build to confirm no errors**

```bash
cd P:/Projects/fh6-telemetry
dotnet build src/Fh6.Telemetry.Overlay
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
cd P:/Projects/fh6-telemetry
git add src/Fh6.Telemetry.Overlay/Widgets/SpeedWidget.xaml
git commit -m "feat(overlay): update SpeedWidget to show mph large with km/h secondary"
```

---

## Task 6: Create GForceWidget

**Files:**
- Create: `src/Fh6.Telemetry.Overlay/Widgets/GForceWidget.xaml`
- Create: `src/Fh6.Telemetry.Overlay/Widgets/GForceWidget.xaml.cs`

The widget is ~90×90 dp: a `Border` wrapper containing a `Canvas` (the traction circle field) plus a text label below. The dot position is computed in code: center + (LatG/1.5 * half, -LongG/1.5 * half), clamped to the circle radius.

- [ ] **Step 1: Create GForceWidget.xaml**

```xml
<UserControl x:Class="Fh6.Telemetry.Overlay.Widgets.GForceWidget"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             Width="90" Height="90"
             SizeChanged="OnSizeChanged">
    <Border Background="#A6080A08" CornerRadius="6" Padding="6,4">
        <StackPanel>
            <TextBlock Text="G-FORCE" Foreground="#99FFFFFF" FontSize="9" FontFamily="Consolas" HorizontalAlignment="Center"/>
            <Canvas x:Name="Field" Width="70" Height="70" HorizontalAlignment="Center">
                <!-- 1g ring (maps to 1g at 1/1.5 of canvas radius = ~66% from centre) -->
                <Ellipse x:Name="Ring"
                         Stroke="#55FFFFFF" StrokeThickness="1"
                         Fill="Transparent"/>
                <!-- Crosshair: horizontal line -->
                <Line x:Name="HLine"
                      Stroke="#33FFFFFF" StrokeThickness="1"/>
                <!-- Crosshair: vertical line -->
                <Line x:Name="VLine"
                      Stroke="#33FFFFFF" StrokeThickness="1"/>
                <!-- G-force dot -->
                <Ellipse x:Name="Dot"
                         Width="8" Height="8"
                         Fill="#E05A5A"/>
            </Canvas>
            <TextBlock x:Name="GLabel"
                       Text="{Binding GText}"
                       Foreground="White" FontSize="9" FontWeight="Bold" FontFamily="Consolas"
                       HorizontalAlignment="Center"/>
        </StackPanel>
    </Border>
</UserControl>
```

- [ ] **Step 2: Create GForceWidget.xaml.cs**

```csharp
using System.Windows;
using System.Windows.Controls;

namespace Fh6.Telemetry.Overlay.Widgets;

public partial class GForceWidget : UserControl
{
    public static readonly DependencyProperty LatGProperty =
        DependencyProperty.Register(
            nameof(LatG),
            typeof(double),
            typeof(GForceWidget),
            new PropertyMetadata(0.0, OnGChanged));

    public static readonly DependencyProperty LongGProperty =
        DependencyProperty.Register(
            nameof(LongG),
            typeof(double),
            typeof(GForceWidget),
            new PropertyMetadata(0.0, OnGChanged));

    public double LatG
    {
        get => (double)GetValue(LatGProperty);
        set => SetValue(LatGProperty, value);
    }

    public double LongG
    {
        get => (double)GetValue(LongGProperty);
        set => SetValue(LongGProperty, value);
    }

    public GForceWidget() => InitializeComponent();

    private static void OnGChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((GForceWidget)d).Redraw();

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => Redraw();

    private void Redraw()
    {
        double w = Field.ActualWidth;
        double h = Field.ActualHeight;
        if (w <= 0 || h <= 0) return;

        double cx = w / 2.0;
        double cy = h / 2.0;

        // Ring: the 1g ring sits at 1/1.5 of the half-width
        double ringRadius = cx / 1.5;
        double ringDiam   = ringRadius * 2.0;
        Ring.Width  = ringDiam;
        Ring.Height = ringDiam;
        Canvas.SetLeft(Ring, cx - ringRadius);
        Canvas.SetTop(Ring,  cy - ringRadius);

        // Crosshair lines
        HLine.X1 = 0;  HLine.Y1 = cy; HLine.X2 = w; HLine.Y2 = cy;
        VLine.X1 = cx; VLine.Y1 = 0;  VLine.X2 = cx; VLine.Y2 = h;

        // Dot: ±1.5g maps to ring edge (cx from centre)
        double dotOffX = Math.Clamp(LatG  / 1.5, -1.0, 1.0) * cx;
        double dotOffY = Math.Clamp(LongG / 1.5, -1.0, 1.0) * cy;

        // Clamp to circle (so diagonal G beyond 1.5g still stays inside)
        double magnitude = Math.Sqrt(dotOffX * dotOffX + dotOffY * dotOffY);
        if (magnitude > cx)
        {
            double scale = cx / magnitude;
            dotOffX *= scale;
            dotOffY *= scale;
        }

        double dotHalf = Dot.Width / 2.0;
        Canvas.SetLeft(Dot, cx + dotOffX - dotHalf);
        Canvas.SetTop(Dot,  cy - dotOffY - dotHalf);   // -dotOffY because screen Y is inverted
    }
}
```

- [ ] **Step 3: Build to confirm no errors**

```bash
cd P:/Projects/fh6-telemetry
dotnet build src/Fh6.Telemetry.Overlay
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
cd P:/Projects/fh6-telemetry
git add src/Fh6.Telemetry.Overlay/Widgets/GForceWidget.xaml src/Fh6.Telemetry.Overlay/Widgets/GForceWidget.xaml.cs
git commit -m "feat(overlay): add GForceWidget traction-circle with LatG/LongG DependencyProperties"
```

---

## Task 7: Create PowerTorqueWidget

**Files:**
- Create: `src/Fh6.Telemetry.Overlay/Widgets/PowerTorqueWidget.xaml`
- Create: `src/Fh6.Telemetry.Overlay/Widgets/PowerTorqueWidget.xaml.cs`

Follows the same pattern as `BoostWidget` — a simple label panel.

- [ ] **Step 1: Create PowerTorqueWidget.xaml**

```xml
<UserControl x:Class="Fh6.Telemetry.Overlay.Widgets.PowerTorqueWidget"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Border Background="#A6080A08" CornerRadius="6" Padding="8,4">
        <StackPanel>
            <TextBlock Text="POWER" Foreground="#99FFFFFF" FontSize="9" FontFamily="Consolas"/>
            <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
                <TextBlock Text="{Binding PowerHp}" Foreground="White" FontSize="18" FontWeight="Bold" FontFamily="Consolas"/>
                <TextBlock Text=" hp" Foreground="#99FFFFFF" FontSize="11" FontFamily="Consolas" VerticalAlignment="Bottom" Margin="2,0,0,1"/>
            </StackPanel>
            <TextBlock Text="TORQUE" Foreground="#99FFFFFF" FontSize="9" FontFamily="Consolas" Margin="0,4,0,0"/>
            <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
                <TextBlock Text="{Binding TorqueLbFt}" Foreground="#CCFFFFFF" FontSize="14" FontWeight="Bold" FontFamily="Consolas"/>
                <TextBlock Text=" lb·ft" Foreground="#99FFFFFF" FontSize="10" FontFamily="Consolas" VerticalAlignment="Bottom" Margin="2,0,0,1"/>
            </StackPanel>
        </StackPanel>
    </Border>
</UserControl>
```

- [ ] **Step 2: Create PowerTorqueWidget.xaml.cs**

```csharp
using System.Windows.Controls;

namespace Fh6.Telemetry.Overlay.Widgets;

public partial class PowerTorqueWidget : UserControl
{
    public PowerTorqueWidget() => InitializeComponent();
}
```

- [ ] **Step 3: Build to confirm no errors**

```bash
cd P:/Projects/fh6-telemetry
dotnet build src/Fh6.Telemetry.Overlay
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
cd P:/Projects/fh6-telemetry
git add src/Fh6.Telemetry.Overlay/Widgets/PowerTorqueWidget.xaml src/Fh6.Telemetry.Overlay/Widgets/PowerTorqueWidget.xaml.cs
git commit -m "feat(overlay): add PowerTorqueWidget showing power (hp) and torque (lb·ft)"
```

---

## Task 8: Register new widgets in WidgetId, FreeLayout, and LayoutSeeds

**Files:**
- Modify: `src/Fh6.Telemetry.Overlay/Widgets/WidgetId.cs`
- Modify: `src/Fh6.Telemetry.Overlay/Layouts/FreeLayout.xaml.cs`
- Modify: `src/Fh6.Telemetry.Overlay/Layouts/LayoutSeeds.cs`

### Step 1: Add enum values to WidgetId

- [ ] **Edit `WidgetId.cs`** — append the two new values:

Current file:
```csharp
public enum WidgetId
{
    Gear,
    Speed,
    RpmShift,
    PedalsSteer,
    Boost,
    LapTiming,
}
```

New file:
```csharp
public enum WidgetId
{
    Gear,
    Speed,
    RpmShift,
    PedalsSteer,
    Boost,
    LapTiming,
    GForce,
    PowerTorque,
}
```

### Step 2: Instantiate new widgets in FreeLayout

- [ ] **Edit `FreeLayout.xaml.cs`** — the `_widgets` dictionary initialiser in the constructor currently has 6 entries. Add the two new ones and wire the GForceWidget bindings.

Replace the constructor's `_widgets = new Dictionary<WidgetId, FrameworkElement> { ... };` block with:

```csharp
var gForceWidget = new GForceWidget();

_widgets = new Dictionary<WidgetId, FrameworkElement>
{
    [WidgetId.Gear]        = new GearWidget(),
    [WidgetId.Speed]       = new SpeedWidget(),
    [WidgetId.RpmShift]    = new RpmShiftWidget(),
    [WidgetId.PedalsSteer] = new PedalsSteerWidget(),
    [WidgetId.Boost]       = new BoostWidget(),
    [WidgetId.LapTiming]   = new LapTimingWidget(),
    [WidgetId.GForce]      = gForceWidget,
    [WidgetId.PowerTorque] = new PowerTorqueWidget(),
};
```

Then, directly after the `foreach (var w in _widgets.Values) Surface.Children.Add(w);` loop, add the binding for the GForceWidget DPs. The DataContext (the VM) is set later via `SetViewModel`, so bind using a `Binding` object against the inherited DataContext:

```csharp
// Bind GForceWidget DependencyProperties to the ViewModel via inherited DataContext.
var latBinding  = new System.Windows.Data.Binding(nameof(ViewModels.TelemetryViewModel.GLat))  { Mode = System.Windows.Data.BindingMode.OneWay };
var longBinding = new System.Windows.Data.Binding(nameof(ViewModels.TelemetryViewModel.GLong)) { Mode = System.Windows.Data.BindingMode.OneWay };
System.Windows.Data.BindingOperations.SetBinding(gForceWidget, GForceWidget.LatGProperty,  latBinding);
System.Windows.Data.BindingOperations.SetBinding(gForceWidget, GForceWidget.LongGProperty, longBinding);
```

Make sure the `using Fh6.Telemetry.Overlay.ViewModels;` namespace is present at the top of the file (check the existing using directives; add if not present).

### Step 3: Add seed positions to LayoutSeeds

- [ ] **Edit `LayoutSeeds.cs`** — add `GForce` and `PowerTorque` to all three preset dictionaries.

**BottomStrip** — the current last widget ends around X=488+148=636. Append:
```csharp
[WidgetId.GForce]      = new(640,  6),
[WidgetId.PowerTorque] = new(736,  6),
```

**CornerPanel** — Row 4 (LapTiming) is at Y=236 and is ~148 tall so ends ~384. Append as Row 5 side-by-side:
```csharp
[WidgetId.GForce]      = new(  6, 396),
[WidgetId.PowerTorque] = new(102, 396),
```

**CenterDash** — place them near the left cluster (PedalsSteer is at 700,820, Boost is at 700,900):
```csharp
[WidgetId.GForce]      = new(700, 980, Visible: false),
[WidgetId.PowerTorque] = new(800, 980, Visible: false),
```

- [ ] **Step 4: Build to confirm no errors**

```bash
cd P:/Projects/fh6-telemetry
dotnet build src/Fh6.Telemetry.Overlay
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Run full test suite**

```bash
cd P:/Projects/fh6-telemetry
dotnet test
```

Expected: All tests pass (46 existing + 3 new = 49 total). Zero failures.

- [ ] **Step 6: Commit**

```bash
cd P:/Projects/fh6-telemetry
git add src/Fh6.Telemetry.Overlay/Widgets/WidgetId.cs \
        src/Fh6.Telemetry.Overlay/Layouts/FreeLayout.xaml.cs \
        src/Fh6.Telemetry.Overlay/Layouts/LayoutSeeds.cs
git commit -m "feat(overlay): register GForce and PowerTorque widgets in WidgetId, FreeLayout, and LayoutSeeds"
```

---

## Task 9: Merge to main and clean up

**Files:**
- (git operations only)

- [ ] **Step 1: Switch to main**

```bash
cd P:/Projects/fh6-telemetry
git checkout main
```

- [ ] **Step 2: Merge with --no-ff**

```bash
cd P:/Projects/fh6-telemetry
git merge --no-ff feat/expand-widgets -m "Add mph, G-force, and power/torque widgets"
```

- [ ] **Step 3: Delete the feature branch**

```bash
cd P:/Projects/fh6-telemetry
git branch -d feat/expand-widgets
```

- [ ] **Step 4: Confirm final test run**

```bash
cd P:/Projects/fh6-telemetry
dotnet test
```

Expected: All tests green.

---

## Self-Review

### Spec Coverage

| Spec item | Task |
|-----------|------|
| `SpeedMph`, `LatG`, `LongG`, `VertG`, `PowerHp`, `TorqueLbFt` computed props | Task 3 |
| Tests: SpeedMph≈23.2, LatG/LongG synthetic, PowerHp≈1000 | Task 2 |
| ViewModel SpeedMph, GLat, GLong, GText, PowerHp, TorqueLbFt + BoundNames | Task 4 |
| SpeedWidget dual unit (mph large, km/h small) | Task 5 |
| GForceWidget traction circle with DP LatG/LongG, dot positioning, GText label | Task 6 |
| PowerTorqueWidget POWER+hp, TORQUE+lb·ft | Task 7 |
| WidgetId: GForce, PowerTorque | Task 8 step 1 |
| FreeLayout: instantiate + add to Canvas + bind GForce DPs | Task 8 step 2 |
| LayoutSeeds: all 3 presets | Task 8 step 3 |
| Branch feat/expand-widgets + merge --no-ff + delete branch | Tasks 1, 9 |

All spec requirements are covered.

### Placeholder Scan

No TBD, TODO, "implement later", or "similar to Task N" present. All steps include code.

### Type Consistency

- `TelemetryReadout.SpeedMph` (float) → VM `SpeedMph` (string via `$"{r.SpeedMph:F0}"`) → XAML `{Binding SpeedMph}` ✓
- `TelemetryReadout.LatG` (float) → VM `GLat` (double) → `GForceWidget.LatGProperty` (double) via `Binding("GLat")` ✓
- `TelemetryReadout.LongG` (float) → VM `GLong` (double) → `GForceWidget.LongGProperty` (double) via `Binding("GLong")` ✓
- `TelemetryReadout.PowerHp` (float) → VM `PowerHp` (string) → XAML `{Binding PowerHp}` ✓
- `TelemetryReadout.TorqueLbFt` (float) → VM `TorqueLbFt` (string) → XAML `{Binding TorqueLbFt}` ✓
- `GForceWidget.Redraw()` uses `Field.ActualWidth/Height`, `Ring`, `HLine`, `VLine`, `Dot` — all named in the XAML ✓
- `WidgetId.GForce` and `WidgetId.PowerTorque` referenced in FreeLayout and LayoutSeeds — both added in Task 8 step 1 ✓

### Notes on GForceWidget DP binding

The `LatG`/`LongG` DPs on `GForceWidget` are bound in `FreeLayout.xaml.cs` using `BindingOperations.SetBinding` after the widget is created but before `SetViewModel` is called. WPF resolves the binding's DataContext at the time of first update (when `SetViewModel` sets `DataContext` on the parent `FreeLayout`), so the binding will work correctly — inherited DataContext flows down to children of the Canvas.

**Alternative (simpler):** You can also bind directly in `GForceWidget.xaml` using `{Binding GLat}` and `{Binding GLong}` on the `LatG`/`LongG` dependency properties (using element binding self or just the standard inherited DataContext). However, because `LatG`/`LongG` are DPs (not plain properties), the XAML binding approach `LatG="{Binding GLat}"` will work at the usage site in FreeLayout's XAML — but since `GForceWidget` is instantiated entirely in code, the `BindingOperations.SetBinding` approach in the code-behind is the clean equivalent.
