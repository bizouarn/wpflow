using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace demo;

/// <summary>État partagé par les nodes de la démo (couleur, forme, zoom), comme dans l'exemple de la page d'accueil de React Flow.</summary>
public sealed class DemoState : INotifyPropertyChanged
{
    private string _colorHex = "#ff0071";
    private Color _color = Color.FromRgb(0xff, 0x00, 0x71);
    private bool _isCube = true;
    private double _zoom = 0.5;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string ColorHex
    {
        get => _colorHex;
        set
        {
            if (_colorHex == value) return;
            _colorHex = value;
            try
            {
                Color = (Color)ColorConverter.ConvertFromString(value);
            }
            catch (FormatException)
            {
                // Saisie incomplète : on garde la dernière couleur valide.
            }

            Raise();
        }
    }

    public Color Color
    {
        get => _color;
        private set
        {
            _color = value;
            Raise();
            Raise(nameof(Brush));
        }
    }

    public Brush Brush => new SolidColorBrush(_color);

    public bool IsCube
    {
        get => _isCube;
        set
        {
            if (_isCube == value) return;
            _isCube = value;
            Raise();
            Raise(nameof(IsPyramid));
        }
    }

    public bool IsPyramid
    {
        get => !_isCube;
        set => IsCube = !value;
    }

    public double Zoom
    {
        get => _zoom;
        set
        {
            _zoom = value;
            Raise();
        }
    }

    private void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
