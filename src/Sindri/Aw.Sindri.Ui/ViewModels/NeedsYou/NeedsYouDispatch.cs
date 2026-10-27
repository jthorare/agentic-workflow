namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>The result of running a Home row's action.</summary>
/// <param name="Outcome">What became of it.</param>
/// <param name="Message">The words the row shows: the refusal as served, or the sentence for an accepted or unhandled action.</param>
internal sealed record NeedsYouDispatch(DispatchOutcome Outcome, string Message);
