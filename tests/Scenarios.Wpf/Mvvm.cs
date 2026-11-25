namespace Scenarios.Wpf;

/// <summary>Une commande minimale, comme celles d'une boîte à outils MVVM.</summary>
internal sealed class Command<T>(Action<T> execute, Func<T, bool>? canExecute = null) : System.Windows.Input.ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add => System.Windows.Input.CommandManager.RequerySuggested += value;
        remove => System.Windows.Input.CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => parameter is T value && (canExecute?.Invoke(value) ?? true);

    public void Execute(object? parameter) => execute((T)parameter!);
}
