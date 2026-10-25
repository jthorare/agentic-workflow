namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>What the catalog says of Decide, and what a disabled option owes the human: why it takes no input (UI spec §7).</summary>
/// <param name="Readiness">Whether the app can run Decide now.</param>
/// <param name="Cli">The served command.</param>
/// <param name="Read">False while the catalog has not been read, so Decide's absence from it means nothing yet.</param>
internal readonly record struct DecideState(ActionReadiness Readiness, string Cli, bool Read = true)
{
    /// <summary>The tooltip and help text of a disabled option: the served command, or why there is none; empty when Decide runs.</summary>
    internal string Reason => Line;

    /// <summary>The line shown once under the decision while Decide is unavailable: the catalog is unread (say so) or lists no Decide (the served command is all there is to say); empty when Decide runs.</summary>
    internal string Line => Readiness switch
    {
        ActionReadiness.Pending => !Read ? Labels.CheckingCatalog : Cli.Length > 0 ? Cli : Labels.DecideUnlisted,
        ActionReadiness.Ready => "",
        _ => "",
    };
}
