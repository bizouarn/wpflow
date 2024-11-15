using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace wpflow;

/// <summary>
///     Logique d'interaction pour Edge.xaml
/// </summary>
public partial class Edge
{
    private Point _startPoint;
    private Point _endPoint;
    private Line _line;
    private Canvas _parentCanvas;

    public Edge()
    {
        _startPoint = new Point();
        _endPoint = new Point(5,5);
        _line = new Line
        {
            Stroke = Brushes.Black,
            StrokeThickness = 2,
            X1 = _startPoint.X,
            X2 = _endPoint.X,
            Y1 = _endPoint.Y,
            Y2 = _endPoint.Y
        };

        Loaded += (_, _) => InitializeParentCanvas();
    }

    private void InitializeParentCanvas()
    {
        _parentCanvas = Parent as Canvas;
        if (_parentCanvas == null) throw new InvalidOperationException("Nodes must be a direct child of a Canvas.");
        _parentCanvas.Children.Add(_line);
    }
}