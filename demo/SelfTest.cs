using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using Wpflow.Core;
using Wpflow.Wpf;

namespace demo;

/// <summary>
///     Auto-test visuel (activé par WPFLOW_SELFTEST=1 avec WPFLOW_SCREENSHOT) : pilote la vraie souris Windows pour
///     déplacer un node puis créer une connexion, afin de vérifier le comportement interactif de bout en bout.
/// </summary>
internal static class SelfTest
{
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, nint extra);

    private const uint LeftDown = 0x0002, LeftUp = 0x0004, Wheel = 0x0800;

    public static async Task Run(FlowView flow, Action<string> log)
    {
        await Task.Delay(1000);

        // 1. Déplacement du node "shape" de (-80, +60) pixels écran.
        var shape = Find<NodeHost>(flow).First(h => h.Node?.Id == "shape");
        var before = flow.Nodes.First(n => n.Id == "shape").Position;
        var center = Screen(shape, new Point(shape.ActualWidth / 2, 8));
        await Drag(center, (center.X - 80, center.Y + 60));
        var after = flow.Nodes.First(n => n.Id == "shape").Position;
        log($"drag: {before} -> {after} (zoom {flow.Viewport.Zoom})");

        // 2. Connexion color -> output (handle "zoom") : un 4e edge doit apparaître.
        var edgesBefore = flow.Edges.Count;
        var src = Find<Handle>(Find<NodeHost>(flow).First(h => h.Node?.Id == "color")).First();
        var dst = Find<Handle>(Find<NodeHost>(flow).First(h => h.Node?.Id == "output"))
            .First(h => h.HandleId == "zoom");
        var from = Screen(src, new Point(src.ActualWidth / 2, src.ActualHeight / 2));
        var to = Screen(dst, new Point(dst.ActualWidth / 2 + 4, dst.ActualHeight / 2));
        await Drag(from, (to.X, to.Y));
        log($"connect: edges {edgesBefore} -> {flow.Edges.Count}");

        // 3. Zoom molette au centre de la vue.
        var zoomBefore = flow.Viewport.Zoom;
        var c = Screen(flow, new Point(flow.ActualWidth / 2, flow.ActualHeight / 2));
        SetCursorPos((int)c.X, (int)c.Y);
        mouse_event(Wheel, 0, 0, unchecked((uint)120), 0);
        await Task.Delay(300);
        log($"wheel zoom: {zoomBefore:0.00} -> {flow.Viewport.Zoom:0.00}");

        // 4. Pan par glisser sur le fond.
        var vpBefore = flow.Viewport;
        var empty = Screen(flow, new Point(300, 80));
        await Drag(empty, (empty.X + 50, empty.Y + 30));
        log($"pan: ({vpBefore.X:0},{vpBefore.Y:0}) -> ({flow.Viewport.X:0},{flow.Viewport.Y:0})");

        // 5. Sélection puis suppression avec la touche Suppr.
        var zoomNode = Find<NodeHost>(flow).First(h => h.Node?.Id == "zoom");
        var zc = Screen(zoomNode, new Point(zoomNode.ActualWidth / 2, 8));
        await Drag(zc, (zc.X, zc.Y)); // simple clic
        log($"select: zoom.Selected={flow.Nodes.First(n => n.Id == "zoom").Selected}");

        // 6. Reconnexion : on attrape l'edge "e-shape" près de sa cible et on le dépose sur le handle "color".
        var output = Find<NodeHost>(flow).First(h => h.Node?.Id == "output");
        var shapeHandle = Find<Handle>(output).First(h => h.HandleId == "shape");
        var colorHandle = Find<Handle>(output).First(h => h.HandleId == "color");
        var zoomLevel = flow.Viewport.Zoom;
        var grab = Screen(shapeHandle, new Point(0, shapeHandle.ActualHeight / 2));
        grab = new Point(grab.X - 12 * zoomLevel, grab.Y);
        var drop = Screen(colorHandle, new Point(colorHandle.ActualWidth / 2, colorHandle.ActualHeight / 2));
        await Drag(grab, (drop.X, drop.Y));
        var reconnected = flow.Edges.First(e => e.Id == "e-shape");
        log($"reconnect: e-shape target handle = {reconnected.TargetHandle} (attendu: color)");

        // 7. Undo x3 : reconnexion, connexion, déplacement.
        flow.Undo();
        log($"undo 1: e-shape target handle = {flow.Edges.First(e => e.Id == "e-shape").TargetHandle} (attendu: shape)");
        flow.Undo();
        log($"undo 2: edges = {flow.Edges.Count} (attendu: 3)");
        flow.Undo();
        log($"undo 3: shape = {flow.Nodes.First(n => n.Id == "shape").Position} (attendu: {before})");
        flow.Redo();
        log($"redo: shape = {flow.Nodes.First(n => n.Id == "shape").Position} (attendu: {after})");

        // 8. Redimensionnement : on sélectionne "output" (clic sur son titre) puis on tire son coin bas-droit.
        flow.Viewport = new Viewport(560, 150, 1.1); // le node doit être entièrement visible
        await Task.Delay(300);
        var outHost = Find<NodeHost>(flow).First(h => h.Node?.Id == "output");
        var oc = Screen(outHost, new Point(40, 10));
        await Drag(oc, (oc.X, oc.Y));
        var resizer = Find<NodeResizer>(outHost).First();
        var corner = resizer.GetThumb(ResizeDirection.BottomRight)!;
        var sizeBefore = flow.Nodes.First(n => n.Id == "output");
        var cp = Screen(corner, new Point(corner.ActualWidth / 2, corner.ActualHeight / 2));
        var z = flow.Viewport.Zoom;
        await Drag(cp, (cp.X + 44, cp.Y + 33));
        var sizeAfter = flow.Nodes.First(n => n.Id == "output");
        log($"resize BR: {sizeBefore.Width:0.#}x{sizeBefore.Height:0.#} -> {sizeAfter.Width:0.#}x{sizeAfter.Height:0.#} " +
            $"(attendu +{44 / z:0.#} / +{33 / z:0.#})");

        // 9. Coin haut-gauche : la taille augmente ET la position bouge.
        var tl = resizer.GetThumb(ResizeDirection.TopLeft)!;
        var tp = Screen(tl, new Point(tl.ActualWidth / 2, tl.ActualHeight / 2));
        await Drag(tp, (tp.X - 22, tp.Y - 11));
        var s2 = flow.Nodes.First(n => n.Id == "output");
        log($"resize TL: pos {sizeAfter.Position} -> {s2.Position}, size {s2.Width:0.#}x{s2.Height:0.#}");
        log($"outline visible: selected={s2.Selected}");
    }

    private static Point Screen(UIElement e, Point local) => e.PointToScreen(local);

    private static async Task Drag(Point from, (double X, double Y) to)
    {
        SetCursorPos((int)from.X, (int)from.Y);
        await Task.Delay(120);
        mouse_event(LeftDown, 0, 0, 0, 0);
        await Task.Delay(80);
        const int steps = 12;
        for (var i = 1; i <= steps; i++)
        {
            SetCursorPos((int)(from.X + (to.X - from.X) * i / steps), (int)(from.Y + (to.Y - from.Y) * i / steps));
            await Task.Delay(25);
        }

        await Task.Delay(80);
        mouse_event(LeftUp, 0, 0, 0, 0);
        await Task.Delay(250);
    }

    private static IEnumerable<T> Find<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t) yield return t;
            foreach (var d in Find<T>(child)) yield return d;
        }
    }
}
