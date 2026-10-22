using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Components;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>
/// One command the engine lists for the state. The app sends none of them yet (the send is U8b): each shows the CLI the
/// engine served with a Copy, and a protected one is the lock button with its popover. A CLI line that holds a control
/// or bidi character is not shown.
/// </summary>
internal sealed class ExpandedCommandViewModel : ObservableObject
{
    internal ExpandedCommandViewModel(StatusCommand command, AnswerKind? answer, string address, IClipboard clipboard, TimeProvider clock)
    {
        Verb = command.Verb;
        // Each answer variant is its own entry (answer-accept, answer-reject, ...), named by its own action id rather than the status line's kind.
        Label = command.Verb == StatusVerb.Answer && Labels.HasLabel(command.ActionId) ? Labels.Action(command.ActionId) : Labels.CommandLabel(command.Verb, command.Verb == StatusVerb.Answer ? answer : null);
        var isProtected = string.Equals(command.Class, "protected", StringComparison.Ordinal);
        PendingChip = isProtected ? string.Empty : Labels.AvailableWithU8b;
        Name = Labels.CommandName(Label, address, PendingChip);
        Cli = ControlViewModel.Safe(command.Cli) ? command.Cli : string.Empty;
        Confirm = command.Confirm;
        Protected = isProtected ? new ProtectedActionViewModel(new ProtectedAction(Label, command.Cli, address), clipboard, clock) : null;
        CopyName = $"{Labels.Copy}: {(Cli.Length > 0 ? Cli : Labels.NoCommandServed)}";
        CliCopy = new CopyAction(clipboard, clock, Name);
    }

    /// <summary>The verb.</summary>
    public StatusVerb Verb { get; }

    /// <summary>The words.</summary>
    public string Label { get; }

    /// <summary>The accessible name: the words and the instance.</summary>
    public string Name { get; }

    /// <summary>True for Cancel, which asks first.</summary>
    public bool IsCancel => Verb == StatusVerb.Cancel;

    /// <summary>True for Hand back on a command the app may send (not a protected one), which has its own button.</summary>
    public bool IsHandBack => Verb == StatusVerb.Handback && Protected is null;

    /// <summary>True when the words stand as text: a protected command has the lock button, Cancel and Hand back their own buttons.</summary>
    public bool ShowsLabel => Protected is null && !IsCancel && !IsHandBack;

    /// <summary>The served CLI line, or empty when it can't be shown.</summary>
    public string Cli { get; }

    /// <summary>The served confirm sentence, or null.</summary>
    public string? Confirm { get; }

    /// <summary>The chip that stands for the send, which the app does not make yet; empty for a protected command.</summary>
    public string PendingChip { get; }

    /// <summary>The lock button for a protected command; null for the others.</summary>
    public ProtectedActionViewModel? Protected { get; }

    /// <summary>True when the command shows its own CLI and Copy (a protected one does it in its popover).</summary>
    public bool ShowsCli => Protected is null && Cli.Length > 0;

    /// <summary>The Copy control: its words and the live region that says "Copied".</summary>
    public CopyAction CliCopy { get; }

    /// <summary>The Copy button's accessible name: it carries the CLI.</summary>
    public string CopyName { get; }

    /// <summary>Copies the CLI.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the clipboard answered.</returns>
    public Task Copy(CancellationToken cancellationToken) => CliCopy.Run(Cli, cancellationToken);

    /// <summary>What the command shows, so a re-read of the same keeps the control.</summary>
    internal string Signature => string.Join('|', Verb, Label, Cli, Confirm, Protected is null);
}
