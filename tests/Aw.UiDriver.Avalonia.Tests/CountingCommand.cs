using System.Windows.Input;

namespace Aw.UiDriver.Avalonia.Tests;

/// <summary>A command that counts how often it ran.</summary>
internal sealed class CountingCommand : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public int Runs { get; private set; }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => Runs++;
}
