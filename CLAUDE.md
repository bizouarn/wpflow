# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

`wpflow` ports [xyflow](https://github.com/xyflow/xyflow) (React Flow) to WPF: an interactive node/edge graph editor. Planning documents live in `work-doc/` (`ROADMAP.md` for phases and the target demo, `ARCHITECTURE-DECISIONS.md` for decisions/open questions). Put new roadmap/planning markdown files there and check them before making architectural changes.

The `demo` must keep resembling the homepage demo of https://reactflow.dev/ (dotted background, white rounded cards with real WPF controls, dashed animated bezier edges, minimap, controls). A reference screenshot is in `work-doc/screenshots/demo.png`.

User documentation lives in `doc/` (Learn / API Reference / Examples, in French, mirroring reactflow.dev); keep it in sync when changing the public API. Link check: every relative link in `doc/**/*.md` must resolve.

Solution `wpflow.sln`, Windows only:

- `src/Wpflow.Core` (net8.0, **no WPF dependency**) — logic ported from `@xyflow/system`: immutable model (`Node`, `Edge`, `HandleInfo`), viewport math, edge path generation (SVG path strings), bounds/handle helpers, and `Changes.Apply` (NodeChange/EdgeChange). New logic goes here first, with tests.
- `src/Wpflow.Wpf` (net8.0-windows) — WPF controls over the core: `FlowView`, `NodeHost`, `Handle`, `EdgeLayer`, `FlowBackground`, `MiniMap`, `FlowControls`; default styles in `Themes/Generic.xaml`.
- `demo/` (net10.0-windows, because only the .NET 10 runtime is installed locally) — homepage-style demo.
- `tests/Wpflow.Core.Tests` (xUnit, net10.0) — tests for the core only.

## Commands

```bash
dotnet build wpflow.sln
dotnet run --project demo
dotnet test tests/Wpflow.Core.Tests
dotnet test tests/Wpflow.Core.Tests --filter "FullyQualifiedName~EdgePathsTests"   # one test class
```

Headless visual check of the demo (captures the window to a PNG and exits); `WPFLOW_SELFTEST=1` additionally drives the real mouse (drag, connect, reconnect an edge, wheel zoom, pan, select, undo/redo) and writes a `.log` next to the PNG; `WPFLOW_PERF=N` instead runs the perf spike on N generated nodes (add `WPFLOW_PERF_VIRT=1` for virtualisation) and writes `.perf.log`. `WPFLOW_EXAMPLES=selftest` opens the examples window and runs 28 checks (groups, keyboard, toolbar, palette drag&drop, loose mode, validator, translateExtent, JSON, dark theme, virtualisation), logging `OK`/`ECHEC` lines to the `.log` and saving per-tab PNGs. These tests use real Win32 mouse/keyboard input, so the window must be allowed to take the foreground and nothing else should touch the mouse while they run. Do not overwrite `work-doc/screenshots/demo.png` with a self-test run (it is the reference for the untouched demo):

```bash
WPFLOW_SCREENSHOT="D:/git/wpflow/work-doc/screenshots/out.png" WPFLOW_SELFTEST=1 dotnet run --project demo --no-build
```

## Architecture (WPF layer)

- **State flows through changes, like React Flow.** `FlowView.Nodes` / `Edges` are immutable record lists. UI interactions never mutate them: they build `NodeChange`/`EdgeChange` lists, raise `NodeChangesRequested`/`EdgeChangesRequested`, and — if `AutoApplyChanges` (default true) — apply them via `Changes.Apply`. Set `AutoApplyChanges=false` for a controlled mode.
- **Layers inside `PART_Viewport`** (a `Grid` whose `RenderTransform` is the `Viewport` matrix): `EdgeLayer` (one self-drawing `EdgeVisual` per edge (path, wide transparent hit stroke, arrows, animated dashes all in `OnRender`) plus lazily created `EdgeLabel`s) → `Canvas` of `NodeHost`s → connection-preview path. `FlowBackground` sits outside the transform and uses a tiled `DrawingBrush` whose transform follows the viewport. The control's `Content` is an overlay (hero text, `MiniMap`, `FlowControls`).
- **All mouse interaction is centralised in `FlowView`** (`OnMouseLeftButtonDown/Move/Up`, modes Pan/DragNodes/Connect/Select), found by walking up from `e.OriginalSource` to `NodeHost`/`Handle`/`EdgeVisual`. Child controls (Slider, TextBox…) that mark events handled therefore keep working inside nodes.
- **Node content:** `NodeHost` looks up a `DataTemplate` with key `wpflow-node-{Node.Type}` (DataContext = the `Node`; put view-model state in `Node.Data`); otherwise it builds the React Flow-style default node (`input`/`output`/`default` handle layout).
- **Resizing:** `Node.FixedWidth/FixedHeight` set the explicit size applied to `NodeHost`; `NodeResizer` (place it inside the node template after the card but before the `Handle`s, with stretching content) emits `NodeResizeChange`. Selected custom-template nodes get an outline from the `NodeHost` style.
- **Measurement loop:** `NodeHost` measures its size and its `Handle` children (relative bounds) after layout (`RequestMeasure`, coalesced at `DispatcherPriority.Loaded`) and reports to `FlowView.OnNodeMeasured`, which emits a `NodeDimensionsChange` and stores handle info used for edge endpoints and connection hit-testing. Edges are not drawn until their endpoints are measured.
- **Nested nodes:** `Node.Position` is relative to `ParentId`. `FlowView._nodeIndex` holds nodes in **absolute** coordinates (with `ParentId` cleared) and is what edges, handles, connections, minimap (`AbsoluteNodes`) and the selection box use; `_rawIndex` holds nodes as supplied and is used for drag constraints (`ConstrainPosition`: `ExtentParent`, then `NodeExtent`). Z-index = depth. `NodeHost` skips work only if the node reference *and* its absolute position are unchanged.
- **Virtualisation:** with `OnlyRenderVisibleElements`, `NodeHost`s and `EdgeVisual`s are *created lazily* (`SyncHost`, `EdgeLayer.Refresh` with show/keep rects): shown within 300 px of the view, kept hidden up to 900 px, absent beyond (so memory no longer scales with node count). Selected nodes are always shown. `_handleMap` keeps measured handle info for absent nodes so edges still resolve. Offscreen never-measured nodes are never created, so bounds/fitView/minimap only know measured nodes.
- **Themes:** `ColorMode` merges `Themes/Light.xaml` or `Dark.xaml` into `FlowView.Resources`; code that draws (edges, minimap, default nodes) must read colors via `FlowView.ThemeBrush("Flow.…")`, XAML uses `{DynamicResource Flow.…}`.
- **Input extras:** keyboard navigation and Space+drag/middle-button pan live in `FlowView`; touch pinch uses `ManipulationDelta` (untested on hardware); `DropRequested` serves palette drag&drop; `EdgeLabelTemplate`, `NodeToolbar`, `FlowPanel` complete the component set.
- **Core extras:** `GraphSerializer` (React Flow JSON shape), `GraphLayout`, `NodeTree`, `Connections` (strict/loose), `ViewportMath.ClampToExtent` (translateExtent). `EdgePaths` was compared with the real `@xyflow/system` sources (smoothstep ported from the recent version); re-fetch `packages/system/src/utils/edges/*.ts` from the xyflow repo before changing path logic.
- Name clashes to watch when editing: `Wpflow.Core.Rect` vs `System.Windows.Rect` (files alias `Rect`/`Path` explicitly) and `System.IO.Path` vs `Shapes.Path`.

Code comments in the repo are mostly in French.
