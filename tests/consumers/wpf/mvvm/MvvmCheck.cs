using Wpflow.Core;
using Wpflow.Wpf;

namespace Consumer;

public interface IMovable
{
    double X { get; set; }
    double Y { get; set; }
}

internal static class MvvmCheck
{
    public static void Run(FlowView view, Action<string, bool> check)
    {
        var vm = (MainViewModel)view.DataContext;
        vm.Nodes[0].X = 42;
        check("ViewModel -> vue", view.Nodes[0].Position.X == 42);
        view.UpdateNode("a", n => n with { Position = new XYPosition(7, 9) });
        check("vue -> ViewModel", vm.Nodes[0].X == 7 && vm.Nodes[0].Y == 9);
        var command = view.ConnectCommand;
        check("ConnectCommand lié", command != null && ReferenceEquals(command, vm.ConnectCommand));
        command?.Execute(new Connection("b", "a", null, null));
        check("un lien ajouté par le ViewModel arrive dans la vue", view.Edges.Count == 2);
    }
}
