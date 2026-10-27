namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// The labelled lines of a failure row's sheet (U3' T6): the service, when it failed, why (the served reason sentence), and each action
/// the row offers with the command that does it (an action the app cannot run yet shows its served command
/// to copy). Built once for a row and its age, not on every read.
/// </summary>
internal static class FailureDetails
{
    /// <summary>The lines, in order: service, last run, why, then the actions.</summary>
    /// <param name="entry">The failure row.</param>
    /// <param name="age">How long ago it failed, as the row shows it.</param>
    /// <returns>The lines.</returns>
    internal static IReadOnlyList<SheetDetail> Build(NeedsYouEntry entry, string age)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return
        [
            new(Labels.SheetService, entry.Subject?.Id ?? entry.ShownId),
            new(Labels.SheetFailed, age),
            new(Labels.SheetWhy, entry.Reason),
            .. entry.Actions.Select(a => new SheetDetail(a.Label, Value(a), a.FillInHint, a.Cli)),
        ];
    }

    private static string Value(NeedsYouAction a) => a.IsReady ? a.Cli : Labels.NotInTheAppYet(a.Cli);
}
