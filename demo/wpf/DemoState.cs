using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;

namespace demo;

/// <summary>État partagé par les nodes de la démo (couleur, forme, zoom).</summary>
public sealed class DemoState : INotifyPropertyChanged
{
    public const string InitialColorHex = "#57dfa9";

    private string _colorHex = InitialColorHex;
    private Color _color = Color.FromRgb(0x57, 0xdf, 0xa9);
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
        Color.FromRgb(0x57, 0xdf, 0xa9), Color.FromRgb(0x5f, 0xbd, 0xf8), Color.FromRgb(0x81, 0x8c, 0xf8),
        Color.FromRgb(0xc0, 0x84, 0xfc), Color.FromRgb(0xfb, 0xbf, 0x24), Color.FromRgb(0xfb, 0x92, 0x3c),
        Color.FromRgb(0xa3, 0xe6, 0x35), Color.FromRgb(0xe2, 0xe8, 0xf0)
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

    // Le node « rendu » ne lit une valeur que si le node correspondant
    // lui est relié : sans edge, il retombe sur la valeur par défaut.
    public static readonly Color DefaultColor = Color.FromRgb(0x5b, 0x74, 0x86);
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
public sealed class ParamCommand(Action<object?> execute, Func<object?, bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => execute(parameter);
}
