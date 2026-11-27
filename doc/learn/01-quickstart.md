# 1. Quick start

*React Flow equivalent: Quick Start.*

## Installation

Add a reference to the project (or the NuGet package `Wpflow.Wpf`, which depends on `Wpflow.Core`):

```xml
<ProjectReference Include="..\src\Wpflow.Wpf\Wpflow.Wpf.csproj" />
```

Requirements: Windows, with .NET 8 or later, or .NET Framework 4.7.2 or later.

| Package | Targets |
|---|---|
| `Wpflow.Core` | `net8.0`, `net472`, `netstandard2.0` (.NET Core 2.0 and later) |
| `Wpflow.Wpf` | `net8.0-windows`, `net472` |
| `Wpflow.Blazor` | `net8.0` |

WPF only exists on .NET Core from .NET Core 3.0 onward: on .NET Core 2.x, only the `Wpflow.Core` core can be used.

Specifics of the older targets:

- **.NET Framework 4.7.2**: an SDK-style project needs `<LangVersion>latest</LangVersion>` to use the core's records with current C# syntax (`with`, `init`). Selection announcements for screen readers are not emitted (the UI Automation API does not exist in this framework).
- **.NET Core 2.x**: the `System.Text.Json` 8 dependency blocks the build (« System.Runtime.CompilerServices.Unsafe doesn't support netcoreapp2.0 »), because Microsoft does not support it before .NET Core 3.1. To get past this, add `<SuppressTfmSupportBuildWarnings>true</SuppressTfmSupportBuildWarnings>` to the application project; this setting applies to all of its dependencies. .NET Core 2.x is no longer maintained.

## Your first flow

**XAML**

```xml
<Window x:Class="MyApp.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:flow="clr-namespace:Wpflow.Wpf;assembly=Wpflow.Wpf"
        Title="My flow" Width="900" Height="600">

    <flow:FlowView x:Name="Flow" FitViewOnInit="True">
        <!-- The content of FlowView is an overlay displayed above the graph. -->
        <Grid>
            <flow:MiniMap />
            <flow:FlowControls />
        </Grid>
    </flow:FlowView>
</Window>
```

> `FlowView` accepts **only one** child (it is a `ContentControl`). For several overlays, wrap them in a `Grid`, as above.

**C#**

```csharp
using Wpflow.Core;

public MainWindow()
{
    InitializeComponent();

    Flow.Nodes =
    [
        new Node("1", new XYPosition(0, 0),    "input",   "Start"),
        new Node("2", new XYPosition(250, 80), "default", "Step"),
        new Node("3", new XYPosition(500, 0),  "output",  "End"),
    ];

    Flow.Edges =
    [
        new Edge("e1-2", "1", "2", Animated: true),
        new Edge("e2-3", "2", "3", Type: "smoothstep", Label: "ok", MarkerEnd: EdgeMarker.ArrowClosed),
    ];
}
```

Run the application: you can move the nodes, connect the small circles (handles), zoom with the mouse wheel, pan the view by dragging the background, delete with the Delete key and undo with Ctrl+Z. No other code is needed.

## Key takeaways

- A **node** has an `Id`, a `Position` (« flow » coordinates), a `Type` and `Data`.
- An **edge** connects two node ids (`Source` → `Target`).
- `Nodes` and `Edges` are **immutable lists**: to change them, assign a new list (see [Building a flow](03-building-a-flow.md)).
- The built-in types are `default`, `input`, `output` and `group`.

Next step: [Terms and concepts](02-concepts.md).
