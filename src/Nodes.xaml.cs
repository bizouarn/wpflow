using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace wpflow;

/// <summary>
///     Logique d'interaction pour Nodes.xaml
/// </summary>
public partial class Nodes
{
    private bool _isDragging;
    private Canvas _parentCanvas;
    private Point _startPoint;

    public Nodes()
    {
        InitializeComponent();

        // Attache des événements avec des lambdas pour éviter les méthodes inutiles
        Loaded += (_, _) => InitializeParentCanvas();
        MouseDown += OnMouseDown;
        MouseUp += OnMouseUp;
        MouseMove += OnMouseMove;
    }

    private void InitializeParentCanvas()
    {
        _parentCanvas = Parent as Canvas;
        if (_parentCanvas == null) throw new InvalidOperationException("Nodes must be a direct child of a Canvas.");
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _isDragging = true;
            _startPoint = e.GetPosition(this); // Utiliser `this` pour calculer le point d'ancrage relatif.
            CaptureMouse(); // Capture la souris pour assurer un suivi même si elle sort de la fenêtre.
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || _parentCanvas == null) return;

        var currentPosition = e.GetPosition(_parentCanvas);

        // Calcul des nouvelles positions en ajoutant la position actuelle du nœud
        var newLeft = currentPosition.X - _startPoint.X;
        var newTop = currentPosition.Y - _startPoint.Y;

        // Contraindre les limites dans le canvas (optionnel)
        newLeft = Math.Max(0, Math.Min(newLeft, _parentCanvas.ActualWidth - ActualWidth));
        newTop = Math.Max(0, Math.Min(newTop, _parentCanvas.ActualHeight - ActualHeight));

        Canvas.SetLeft(this, newLeft);
        Canvas.SetTop(this, newTop);
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            ReleaseMouseCapture(); // Libère la capture de la souris.
        }
    }
}