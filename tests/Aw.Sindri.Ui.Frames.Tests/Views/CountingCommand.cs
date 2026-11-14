using System.Windows.Input;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>A command that counts its runs.</summary>
internal sealed class CountingCommand : ICommand
{
    public int Runs { get; private set; }

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => Runs++;
}
