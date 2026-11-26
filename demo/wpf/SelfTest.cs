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
        void Check(string name, bool ok, string detail = "") => log(SelfTestReport.Line(name, ok, detail));
        // Écart toléré : le curseur Windows se pose sur des pixels entiers.
        static bool Near(double actual, double expected, double tolerance = 2) => Math.Abs(actual - expected) <= tolerance;
        await Task.Delay(1000);

        // 1. Déplacement du node "shape" de (-40, +20) pixels écran (loin des bords, pour ne pas déclencher l'auto-pan).
        var shape = Find<NodeHost>(flow).First(h => h.Node?.Id == "shape");
        var before = flow.Nodes.First(n => n.Id == "shape").Position;
        var center = Screen(shape, new Point(30, 10)); // le bandeau du titre ; la douille est au milieu du bord haut
        await Drag(center, (center.X - 40, center.Y + 20));
        var after = flow.Nodes.First(n => n.Id == "shape").Position;
        Check("drag: le node suit la souris", Near(after.X - before.X, -40 / flow.Viewport.Zoom) && Near(after.Y - before.Y, 20 / flow.Viewport.Zoom),
            $"{before} -> {after} (zoom {flow.Viewport.Zoom})");

        // 2. Connexion color -> output (handle "zoom") : un 4e edge doit apparaître.
        var edgesBefore = flow.Edges.Count;
        var src = Find<Handle>(Find<NodeHost>(flow).First(h => h.Node?.Id == "color")).First();
        var dst = Find<Handle>(Find<NodeHost>(flow).First(h => h.Node?.Id == "output"))
            .First(h => h.HandleId == "zoom");
        var from = Screen(src, new Point(src.ActualWidth / 2, src.ActualHeight / 2));
        var to = Screen(dst, new Point(dst.ActualWidth / 2 + 4, dst.ActualHeight / 2));
        await Drag(from, (to.X, to.Y));
        Check("connexion: un edge de plus", flow.Edges.Count == edgesBefore + 1, $"edges {edgesBefore} -> {flow.Edges.Count}");

        // 3. Zoom molette au centre de la vue.
        var zoomBefore = flow.Viewport.Zoom;
        var c = Screen(flow, new Point(flow.ActualWidth / 2, flow.ActualHeight / 2));
        MoveTo(c);
        Wheel(120);
        await Task.Delay(300);
        Check("molette: zoom avant", flow.Viewport.Zoom > zoomBefore, $"{zoomBefore:0.00} -> {flow.Viewport.Zoom:0.00}");

        // 4. Pan par glisser sur le fond.
        var vpBefore = flow.Viewport;
        var empty = Screen(flow, new Point(100, 300));
        await Drag(empty, (empty.X + 50, empty.Y + 30));
        Check("pan: la vue suit la souris sans zoomer",
            Near(flow.Viewport.X - vpBefore.X, 50) && Near(flow.Viewport.Y - vpBefore.Y, 30) && flow.Viewport.Zoom == vpBefore.Zoom,
            $"({vpBefore.X:0},{vpBefore.Y:0}) -> ({flow.Viewport.X:0},{flow.Viewport.Y:0})");

        // 5. Sélection puis suppression avec la touche Suppr.
        var zoomNode = Find<NodeHost>(flow).First(h => h.Node?.Id == "zoom");
        var zc = Screen(zoomNode, new Point(30, 10));
        await Drag(zc, (zc.X, zc.Y)); // simple clic
        Check("clic: le node est sélectionné", flow.Nodes.First(n => n.Id == "zoom").Selected);

        // 6. Reconnexion : on attrape l'edge "e-shape" près de sa cible et on le dépose sur le handle "color".
        var output = Find<NodeHost>(flow).First(h => h.Node?.Id == "output");
        var shapeHandle = Find<Handle>(output).First(h => h.HandleId == "shape");
        var colorHandle = Find<Handle>(output).First(h => h.HandleId == "color");
        var zoomLevel = flow.Viewport.Zoom;
        var grab = Screen(shapeHandle, new Point(shapeHandle.ActualWidth / 2, shapeHandle.ActualHeight));
        grab = new Point(grab.X, grab.Y + 12 * zoomLevel); // le câble arrive par le bas
        var drop = Screen(colorHandle, new Point(colorHandle.ActualWidth / 2, colorHandle.ActualHeight / 2));
        await Drag(grab, (drop.X, drop.Y));
        var reconnected = flow.Edges.First(e => e.Id == "e-shape");
        Check("reconnexion: l'extrémité change de handle", reconnected.TargetHandle == "color", $"handle = {reconnected.TargetHandle}");

        // 7. Undo x3 : reconnexion, connexion, déplacement.
        flow.Undo();
        var undoneHandle = flow.Edges.First(e => e.Id == "e-shape").TargetHandle;
        Check("annuler 1: la reconnexion", undoneHandle == "shape", $"handle = {undoneHandle}");
        flow.Undo();
        Check("annuler 2: la connexion", flow.Edges.Count == edgesBefore, $"edges = {flow.Edges.Count}");
        flow.Undo();
        var undonePosition = flow.Nodes.First(n => n.Id == "shape").Position;
        Check("annuler 3: le déplacement", undonePosition == before, $"{undonePosition} (attendu {before})");
        flow.Redo();
        var redonePosition = flow.Nodes.First(n => n.Id == "shape").Position;
        Check("rétablir: le déplacement", redonePosition == after, $"{redonePosition} (attendu {after})");

        // 8. Redimensionnement : on sélectionne "output" (clic sur son titre) puis on tire son coin bas-droit.
        flow.Viewport = HomeViewModel.InitialViewport; // le node doit être entièrement visible
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
        Check("redimensionnement: coin bas-droit",
            Near(sizeAfter.Width!.Value - sizeBefore.Width!.Value, 44 / z) && Near(sizeAfter.Height!.Value - sizeBefore.Height!.Value, 33 / z),
            $"{sizeBefore.Width:0.#}x{sizeBefore.Height:0.#} -> {sizeAfter.Width:0.#}x{sizeAfter.Height:0.#} (attendu +{44 / z:0.#} / +{33 / z:0.#})");

        // 9. Coin haut-gauche : la taille augmente ET la position bouge.
        var tl = resizer.GetThumb(ResizeDirection.TopLeft)!;
        var tp = Screen(tl, new Point(tl.ActualWidth / 2, tl.ActualHeight / 2));
        await Drag(tp, (tp.X - 22, tp.Y - 11));
        var s2 = flow.Nodes.First(n => n.Id == "output");
        Check("redimensionnement: le coin haut-gauche agrandit et déplace",
            s2.Position.X < sizeAfter.Position.X && s2.Position.Y < sizeAfter.Position.Y && s2.Width > sizeAfter.Width && s2.Height > sizeAfter.Height,
            $"pos {sizeAfter.Position} -> {s2.Position}, taille {s2.Width:0.#}x{s2.Height:0.#}");
        Check("redimensionnement: le node reste sélectionné", s2.Selected);

        // 10. Animation des cubes : l'angle avance et l'image change d'une capture à l'autre.
        flow.Viewport = HomeViewModel.InitialViewport;
        await Task.Delay(300);
        var field = Find<demo.CubeField>(Find<NodeHost>(flow).First(h => h.Node?.Id == "output")).First();
        var angle0 = field.Angle;
        var frameA = Snapshot(field);
        await Task.Delay(400);
        var frameB = Snapshot(field);
        Check("cubes: animés", field.Angle != angle0 && !frameA.SequenceEqual(frameB), $"angle {angle0:0.00} -> {field.Angle:0.00}");

        // 11. Zoom relié : on tire le vrai slider ; le zoom affiché des cubes suit ; sans edge il retombe à la valeur par défaut.
        var state = (DemoState)flow.Nodes.First(n => n.Id == "zoom").Data!;
        var slider = Find<System.Windows.Controls.Slider>(Find<NodeHost>(flow).First(h => h.Node?.Id == "zoom")).First();
        var thumb = Find<System.Windows.Controls.Primitives.Thumb>(slider).First();
        var thumbFrom = Screen(thumb, new Point(thumb.ActualWidth / 2, thumb.ActualHeight / 2));
        var zoomBefore2 = state.Zoom;
        await Drag(thumbFrom, (thumbFrom.X + 200, thumbFrom.Y));
        await Task.Delay(1200);
        Check("curseur: glisser change le zoom des cubes", state.Zoom != zoomBefore2 && Near(field.DisplayedZoom, state.Zoom, 0.05),
            $"state.Zoom {zoomBefore2:0.00} -> {state.Zoom:0.00}, affiché {field.DisplayedZoom:0.00}");

        var vm = (HomeViewModel)flow.DataContext;
        var allLinks = vm.Links.ToList();
        void RestoreLinks()
        {
            foreach (var link in allLinks.Where(l => !vm.Links.Contains(l))) vm.Links.Add(link);
        }
        vm.Links.Remove(vm.Links.First(l => l.Id == "e-zoom"));
        await Task.Delay(1500);
        Check("edge zoom supprimé: retour à la valeur par défaut", !state.UseZoom && Near(field.DisplayedZoom, DemoState.DefaultZoom, 0.05),
            $"UseZoom={state.UseZoom}, affiché {field.DisplayedZoom:0.00} (attendu {DemoState.DefaultZoom:0.00})");
        RestoreLinks();
        await Task.Delay(1500);
        Check("edge zoom rétabli: la valeur du curseur revient", state.UseZoom && Near(field.DisplayedZoom, state.Zoom, 0.05),
            $"UseZoom={state.UseZoom}, affiché {field.DisplayedZoom:0.00} (attendu {state.Zoom:0.00})");

        // Couleur et forme suivent aussi leurs edges.
        foreach (var link in vm.Links.Where(l => l.Id is "e-color" or "e-shape").ToList()) vm.Links.Remove(link);
        await Task.Delay(200);
        Check("edges couleur et forme supprimés: les cubes ne les suivent plus", !state.UseColor && !state.UseShape,
            $"UseColor={state.UseColor}, UseShape={state.UseShape}, couleur effective {state.EffectiveColor}");
        RestoreLinks();

        // 12. Sélecteur de couleur : on ouvre la pastille, on clique un preset, les cubes changent de couleur.
        var colorHost = Find<NodeHost>(flow).First(h => h.Node?.Id == "color");
        var swatch = Find<System.Windows.Controls.Primitives.ToggleButton>(colorHost).First();
        var sp = Screen(swatch, new Point(swatch.ActualWidth / 2, swatch.ActualHeight / 2));
        await Drag(sp, (sp.X, sp.Y));
        await Task.Delay(500);
        var popup = Find<System.Windows.Controls.Primitives.Popup>(colorHost).First();
        Check("sélecteur: la pastille ouvre le popup", popup.IsOpen);
        var presetButtons = Find<System.Windows.Controls.Button>(popup.Child).ToList();
        var blue = presetButtons[5];
        var bp = Screen(blue, new Point(blue.ActualWidth / 2, blue.ActualHeight / 2));
        var colorState = (DemoState)flow.Nodes.First(n => n.Id == "color").Data!;
        var colorBefore = colorState.ColorHex;
        await Drag(bp, (bp.X, bp.Y));
        await Task.Delay(300);
        Check("sélecteur: un preset change la couleur des cubes", colorState.ColorHex != colorBefore,
            $"ColorHex {colorBefore} -> {colorState.ColorHex}, couleur effective {colorState.EffectiveColor}");
        var frameBlue = Snapshot(field);
        colorState.ColorHex = DemoState.InitialColorHex;
        await Task.Delay(300);
        Check("sélecteur: les cubes sont redessinés", !frameBlue.SequenceEqual(Snapshot(field)));

        // 13. Forme : clic sur le radio "pyramid" -> les solides passent en pyramides.
        var shapeHost = Find<NodeHost>(flow).First(h => h.Node?.Id == "shape");
        var radios = Find<System.Windows.Controls.RadioButton>(shapeHost).ToList();
        var shapeState = (DemoState)flow.Nodes.First(n => n.Id == "shape").Data!;
        var cubeFrame = Snapshot(field);
        await Click(Center(radios[1]));
        await Task.Delay(400);
        Check("forme: le radio « pyramide » change la forme", !shapeState.IsCube && !shapeState.EffectiveIsCube,
            $"IsCube={shapeState.IsCube}, radios cochés={string.Join(",", radios.Select(r => r.IsChecked))}");
        var pyramidFrame = Snapshot(field);
        Check("forme: le rendu change", !cubeFrame.SequenceEqual(pyramidFrame),
            $"pixels colorés cube={Colored(cubeFrame)} pyramide={Colored(pyramidFrame)}");
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        var target = new System.Windows.Media.Imaging.RenderTargetBitmap((int)field.ActualWidth, (int)field.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        target.Render(field);
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(target));
        using (var fs = System.IO.File.Create(System.IO.Path.ChangeExtension(Environment.GetEnvironmentVariable("WPFLOW_SCREENSHOT")!, ".pyramid.png")))
            encoder.Save(fs);
        await Click(Center(radios[0]));
        await Task.Delay(300);
        Check("forme: le radio « cube » la rétablit", shapeState.IsCube);
    }

    private static int Colored(byte[] bgra)
    {
        var count = 0;
        for (var i = 0; i < bgra.Length; i += 4)
            if (bgra[i + 3] > 0) count++;
        return count;
    }
}
