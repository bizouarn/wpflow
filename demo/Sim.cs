using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace demo;

/// <summary>
///     Outils partagés par les auto-tests : saisie Windows réelle (souris, clavier, molette) et parcours de l'arbre visuel.
///     Les tests pilotent le vrai curseur : la fenêtre doit pouvoir passer au premier plan.
/// </summary>
internal static class Sim
{
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, nint extra);
    [DllImport("user32.dll")] private static extern void keybd_event(byte vk, byte scan, uint flags, nint extra);

    private const uint LeftDown = 0x0002, LeftUp = 0x0004, WheelFlag = 0x0800;
    private const uint KeyUp = 2;

    public const byte VkTab = 0x09, VkControl = 0x11, VkEscape = 0x1B, VkSpace = 0x20, VkLeft = 0x25, VkRight = 0x27,
        VkDelete = 0x2E, VkZ = 0x5A;

    public static Point Screen(UIElement e, Point local) => e.PointToScreen(local);

    public static Point Center(FrameworkElement e) => e.PointToScreen(new Point(e.ActualWidth / 2, e.ActualHeight / 2));

    /// <summary>Glisse avec le bouton gauche de <paramref name="from" /> à <paramref name="to" /> ; un clic est un glisser sur place.</summary>
    public static async Task Drag(Point from, (double X, double Y) to, int steps = 12, int delay = 25)
    {
        SetCursorPos((int)from.X, (int)from.Y);
        await Task.Delay(120);
        mouse_event(LeftDown, 0, 0, 0, 0);
        await Task.Delay(80);
        for (var i = 1; i <= steps; i++)
        {
            SetCursorPos((int)(from.X + (to.X - from.X) * i / steps), (int)(from.Y + (to.Y - from.Y) * i / steps));
            await Task.Delay(delay);
        }

        await Task.Delay(80);
        mouse_event(LeftUp, 0, 0, 0, 0);
        await Task.Delay(250);
    }

    /// <summary>Comme <see cref="Drag" />, mais exécute <paramref name="whileHeld" /> avant de relâcher le bouton (pour observer l'état pendant le geste).</summary>
    public static async Task DragHolding(Point from, (double X, double Y) to, Func<Task> whileHeld)
    {
        SetCursorPos((int)from.X, (int)from.Y);
        await Task.Delay(120);
        mouse_event(LeftDown, 0, 0, 0, 0);
        await Task.Delay(80);
        for (var i = 1; i <= 12; i++)
        {
            SetCursorPos((int)(from.X + (to.X - from.X) * i / 12), (int)(from.Y + (to.Y - from.Y) * i / 12));
            await Task.Delay(25);
        }

        await Task.Delay(120);
        await whileHeld();
        mouse_event(LeftUp, 0, 0, 0, 0);
        await Task.Delay(250);
    }

    public static Task Click(Point p) => Drag(p, (p.X, p.Y), steps: 1);

    public static void MoveTo(Point p) => SetCursorPos((int)p.X, (int)p.Y);

    public static void Wheel(int delta) => mouse_event(WheelFlag, 0, 0, unchecked((uint)delta), 0);

    public static void Key(byte vk, bool ctrl = false)
    {
        if (ctrl) keybd_event(VkControl, 0, 0, 0);
        keybd_event(vk, 0, 0, 0);
        keybd_event(vk, 0, KeyUp, 0);
        if (ctrl) keybd_event(VkControl, 0, KeyUp, 0);
    }

    public static void KeyDown(byte vk) => keybd_event(vk, 0, 0, 0);
    public static void KeyRelease(byte vk) => keybd_event(vk, 0, KeyUp, 0);

    public static IEnumerable<T> Find<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t) yield return t;
            foreach (var d in Find<T>(child)) yield return d;
        }
    }

    /// <summary>Pixels d'un élément rendu (pour comparer deux images).</summary>
    public static byte[] Snapshot(FrameworkElement e)
    {
        var bmp = new RenderTargetBitmap(Math.Max(1, (int)e.ActualWidth), Math.Max(1, (int)e.ActualHeight), 96, 96,
            PixelFormats.Pbgra32);
        bmp.Render(e);
        var pixels = new byte[bmp.PixelWidth * bmp.PixelHeight * 4];
        bmp.CopyPixels(pixels, bmp.PixelWidth * 4, 0);
        return pixels;
    }
}
