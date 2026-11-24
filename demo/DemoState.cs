using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
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
            Raise(nameof(Hue));
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

    /// <summary>Couleurs proposées par le sélecteur.</summary>
    public IReadOnlyList<Color> Presets { get; } =
    [
        Color.FromRgb(0xff, 0x00, 0x71), Color.FromRgb(0xff, 0x7a, 0x00), Color.FromRgb(0xff, 0xc8, 0x00),
        Color.FromRgb(0x2e, 0xcc, 0x71), Color.FromRgb(0x00, 0xb8, 0xd9), Color.FromRgb(0x3b, 0x82, 0xf6),
        Color.FromRgb(0x8b, 0x5c, 0xf6), Color.FromRgb(0x11, 0x18, 0x27)
    ];

    private ICommand? _setColor;

    public ICommand SetColorCommand => _setColor ??= new ParamCommand(p =>
    {
        if (p is Color c) ColorHex = $"#{c.R:x2}{c.G:x2}{c.B:x2}";
    });

    /// <summary>Teinte (0–359) de la couleur courante ; l'écrire choisit une couleur pleinement saturée.</summary>
    public double Hue
    {
        get
        {
            double r = _color.R / 255.0, g = _color.G / 255.0, b = _color.B / 255.0;
            var max = Math.Max(r, Math.Max(g, b));
            var d = max - Math.Min(r, Math.Min(g, b));
            if (d < 1e-9) return 0;
            var h = max == r ? ((g - b) / d % 6) : max == g ? (b - r) / d + 2 : (r - g) / d + 4;
            return (h * 60 + 360) % 360;
        }
        set
        {
            if (Math.Abs(value - Hue) < 0.5) return;
            var h = value / 60.0;
            var x = 1 - Math.Abs(h % 2 - 1);
            var (r, g, b) = (int)h switch
            {
                0 => (1.0, x, 0.0), 1 => (x, 1.0, 0.0), 2 => (0.0, 1.0, x),
                3 => (0.0, x, 1.0), 4 => (x, 0.0, 1.0), _ => (1.0, 0.0, x)
            };
            ColorHex = $"#{(int)Math.Round(r * 255):x2}{(int)Math.Round(g * 255):x2}{(int)Math.Round(b * 255):x2}";
        }
    }

    // Comme dans la démo de React Flow, le node « output » ne lit une valeur que si le node correspondant
    // lui est relié : sans edge, il retombe sur la valeur par défaut.
    public static readonly Color DefaultColor = Color.FromRgb(0x9c, 0xa3, 0xaf);
    public const double DefaultZoom = 0.5;

    private bool _useColor = true, _useShape = true, _useZoom = true;

    public bool UseColor
    {
        get => _useColor;
        set => Set(ref _useColor, value);
    }

    public bool UseShape
    {
        get => _useShape;
        set => Set(ref _useShape, value);
    }

    public bool UseZoom
    {
        get => _useZoom;
        set => Set(ref _useZoom, value);
    }

    public Color EffectiveColor => _useColor ? _color : DefaultColor;
    public bool EffectiveIsCube => !_useShape || _isCube;
    public double EffectiveZoom => _useZoom ? _zoom : DefaultZoom;

    private void Set(ref bool field, bool value, [CallerMemberName] string? name = null)
    {
        if (field == value) return;
        field = value;
        Raise(name);
    }

    private void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Commande à paramètre (le paramètre est celui de <c>CommandParameter</c>).</summary>
public sealed class ParamCommand(Action<object?> execute) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => execute(parameter);
}
