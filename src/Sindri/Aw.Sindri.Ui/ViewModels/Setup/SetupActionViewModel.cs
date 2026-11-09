using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Setup;
using Aw.Sindri.Ui.ViewModels.Components;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Setup;

/// <summary>
/// The one action a step offers: a button the app presses itself when it can do the action, otherwise the terminal command to copy. A protected action (an approval) is the
/// existing locked terminal-copy button, never a command the app runs for the person. The command is the catalog's own when it lists the action, else the spec's form.
/// </summary>
internal sealed class SetupActionViewModel
{
    private static readonly HashSet<string> _protected = new(StringComparer.Ordinal);   // none left: recipe approval (v2 PR 4b) and profile approval (main) are gone

    private readonly Action<string>? _open;

    internal SetupActionViewModel(string action, SetupStepView step, bool inApp, CatalogViewModel catalog, TimeProvider clock, Action<string>? open)
    {
        (Action, IsInApp, _open) = (action, inApp, open);
        Done = string.Equals(step.State, "done", StringComparison.Ordinal);
        Label = Labels.SetupActionLabel(action, Done);
        Name = NameOf(step);
        if (inApp)
        {
            return;
        }

        var cli = catalog.Row(action) is { Cli: { Length: > 0 } served } ? served : Labels.SetupCli(action);
        if (cli.Length == 0)
        {
            return;
        }

        if (_protected.Contains(action))
        {
            Protected = new ProtectedActionViewModel(new ProtectedAction(Label, cli, Labels.SetupTitle(step.Id, step.Title)), catalog.Clipboard, clock);
        }
        else
        {
            Cli = new ControlViewModel(new CommandRowView(action, "desk", [], Terminal: false, Method: null, Route: null, cli, "cli-only:setup", Destructive: false, ReadsViews: false, Shortcut: null), send: null, catalog.Clipboard, clock);
        }
    }

    /// <summary>The catalog action id.</summary>
    public string Action { get; }

    /// <summary>The button's words ("Approve all workflows"; "Add another repo" once the step is done).</summary>
    public string Label { get; }

    /// <summary>The button's accessible name: its words first, then the step.</summary>
    public string Name { get; }

    /// <summary>True when the app does it itself (a button); false when the person runs the command.</summary>
    public bool IsInApp { get; }

    /// <summary>The terminal command with Copy, when the app does not do it itself and it is not protected; else null.</summary>
    public ControlViewModel? Cli { get; }

    /// <summary>The locked terminal-copy button of a protected action the app does not do itself; else null.</summary>
    public ProtectedActionViewModel? Protected { get; }

    /// <summary>True when the command shows (the view's switch).</summary>
    public bool HasCli => Cli is not null;

    /// <summary>True when the protected terminal-copy button shows (the view's switch).</summary>
    public bool HasProtected => Protected is not null;

    /// <summary>The line over the command.</summary>
    public string CliHint { get; } = Labels.SetupCliHint;

    /// <summary>True when the step it belongs to is done.</summary>
    internal bool Done { get; }

    /// <summary>Whether this is still the action for <paramref name="step"/>: the same action, drawn the same way, with the same words.</summary>
    /// <param name="action">The action the step offers now.</param>
    /// <param name="step">The step as served now.</param>
    /// <param name="inApp">Whether the app does the action itself now.</param>
    /// <returns>True when nothing about it changed.</returns>
    internal bool Matches(string action, SetupStepView step, bool inApp) =>
        string.Equals(Action, action, StringComparison.Ordinal) && IsInApp == inApp && Done == string.Equals(step.State, "done", StringComparison.Ordinal) && string.Equals(Name, NameOf(step), StringComparison.Ordinal);

    /// <summary>Presses the button: asks the owner of the action to open it.</summary>
    internal void Open()
    {
        if (IsInApp)
        {
            _open?.Invoke(Action);
        }
    }

    private string NameOf(SetupStepView step) => Label + ", " + Labels.SetupTitle(step.Id, step.Title);
}
