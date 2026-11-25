using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Scenarios.Wpf;

/// <summary>Le même schéma en MVVM : le ViewModel sert de <c>Node.Data</c>, avec les mêmes noms que <see cref="Table" />.</summary>
public sealed class TableViewModel : INotifyPropertyChanged
{
    private double _x, _y;
    private int _rows;
    private bool _collapsed, _selected;

    public TableViewModel(string id, double x, double y, IEnumerable<Column> columns)
    {
        Id = id;
        _x = x;
        _y = y;
        Columns = new ObservableCollection<Column>(columns);
    }

    public string Id { get; }
    public string Name => Id;
    public string Type => Schemas.TableType;
    public bool Locked => false;
    public ObservableCollection<Column> Columns { get; }
    public double X { get => _x; set => Set(ref _x, value); }
    public double Y { get => _y; set => Set(ref _y, value); }
    public int Rows { get => _rows; set => Set(ref _rows, value); }
    public bool Collapsed { get => _collapsed; set => Set(ref _collapsed, value); }
    public bool IsSelected { get => _selected; set => Set(ref _selected, value); }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public sealed class RelationViewModel(string id, string source, string sourceColumn, string target, string targetColumn)
{
    public string Id { get; } = id;
    public string Source { get; } = source;
    public string SourceColumn { get; } = sourceColumn;
    public string Target { get; } = target;
    public string TargetColumn { get; } = targetColumn;
    public string Type => Schemas.RelationType;
}
