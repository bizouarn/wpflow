using static demo.Sim;
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
        MoveTo(c);
        Wheel(120);
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

        // 10. Animation des cubes : l'angle avance et l'image change d'une capture à l'autre.
        flow.Viewport = new Viewport(560, 150, 1.1);
        await Task.Delay(300);
        var field = Find<demo.CubeField>(Find<NodeHost>(flow).First(h => h.Node?.Id == "output")).First();
        var angle0 = field.Angle;
        var frameA = Snapshot(field);
        await Task.Delay(400);
        var frameB = Snapshot(field);
        log($"cubes animés: angle {angle0:0.00} -> {field.Angle:0.00}, images différentes = {!frameA.SequenceEqual(frameB)}");

        // 11. Zoom relié : on tire le vrai slider ; le zoom affiché des cubes suit ; sans edge il retombe à la valeur par défaut.
        var state = (DemoState)flow.Nodes.First(n => n.Id == "zoom").Data!;
        var slider = Find<System.Windows.Controls.Slider>(Find<NodeHost>(flow).First(h => h.Node?.Id == "zoom")).First();
        var thumb = Find<System.Windows.Controls.Primitives.Thumb>(slider).First();
        var thumbFrom = Screen(thumb, new Point(thumb.ActualWidth / 2, thumb.ActualHeight / 2));
        var zoomBefore2 = state.Zoom;
        await Drag(thumbFrom, (thumbFrom.X + 200, thumbFrom.Y));
        await Task.Delay(1200);
        log($"slider: state.Zoom {zoomBefore2:0.00} -> {state.Zoom:0.00}, zoom affiché {field.DisplayedZoom:0.00} (attendu ~{state.Zoom:0.00})");

        var edgesWithZoom = flow.Edges;
        flow.Edges = flow.Edges.Where(e => e.Id != "e-zoom").ToList();
        await Task.Delay(1500);
        log($"edge zoom supprimé: UseZoom={state.UseZoom}, zoom affiché {field.DisplayedZoom:0.00} (attendu {DemoState.DefaultZoom:0.00})");
        flow.Edges = edgesWithZoom;
        await Task.Delay(1500);
        log($"edge zoom rétabli: UseZoom={state.UseZoom}, zoom affiché {field.DisplayedZoom:0.00} (attendu ~{state.Zoom:0.00})");

        // Couleur et forme suivent aussi leurs edges.
        flow.Edges = flow.Edges.Where(e => e.Id != "e-color" && e.Id != "e-shape").ToList();
        await Task.Delay(200);
        log($"edges couleur/forme supprimés: UseColor={state.UseColor}, UseShape={state.UseShape}, couleur effective {state.EffectiveColor}");
        flow.Edges = edgesWithZoom;

        // 12. Sélecteur de couleur : on ouvre la pastille, on clique un preset, les cubes changent de couleur.
        var colorHost = Find<NodeHost>(flow).First(h => h.Node?.Id == "color");
        var swatch = Find<System.Windows.Controls.Primitives.ToggleButton>(colorHost).First();
        var sp = Screen(swatch, new Point(swatch.ActualWidth / 2, swatch.ActualHeight / 2));
        await Drag(sp, (sp.X, sp.Y));
        await Task.Delay(500);
        var popup = Find<System.Windows.Controls.Primitives.Popup>(colorHost).First();
        log($"sélecteur: popup ouvert = {popup.IsOpen}");
        var presetButtons = Find<System.Windows.Controls.Button>(popup.Child).ToList();
        var blue = presetButtons[5];
        var bp = Screen(blue, new Point(blue.ActualWidth / 2, blue.ActualHeight / 2));
        await Drag(bp, (bp.X, bp.Y));
        await Task.Delay(300);
        var colorState = (DemoState)flow.Nodes.First(n => n.Id == "color").Data!;
        log($"sélecteur: preset bleu -> ColorHex={colorState.ColorHex}, couleur effective des cubes {colorState.EffectiveColor}");
        var frameBlue = Snapshot(field);
        colorState.ColorHex = "#ff0071";
        await Task.Delay(300);
        log($"sélecteur: les cubes ont été redessinés = {!frameBlue.SequenceEqual(Snapshot(field))}");
    }
}
