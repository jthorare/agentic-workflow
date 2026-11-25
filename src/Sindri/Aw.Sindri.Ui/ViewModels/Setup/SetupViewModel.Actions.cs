using Aw.Sindri.Ui.Api.Setup;

namespace Aw.Sindri.Ui.ViewModels.Setup;

/// <summary>The setup list (<see cref="SetupViewModel"/>, continued): which action a step offers, and the forms of the repo step.</summary>
internal sealed partial class SetupViewModel
{
    // A step's action: an app button when the app can do it, else the command to copy; none for an action nothing here knows. A kept action stays while nothing about it changed, so a
    // Copy in flight is not reset.
    private SetupActionViewModel? ActionOf(SetupStepView step, SetupActionViewModel? kept)
    {
        if (Offered(step) is not { } action)
        {
            return null;
        }

        var inApp = _opensInApp(action) || OpensHere(action);
        if (kept is not null && kept.Matches(action, step, inApp))
        {
            return kept;
        }

        return Knows(action, inApp) ? new SetupActionViewModel(action, step, inApp, _catalog, _clock, Open) : null;
    }

    // The map has no button until it failed; then Retry is the catalog's refresh (a retry of the build, never a service run). The repo step has a form instead of a button
    // while the catalog lets the app send what it sends. Every other step offers what the API serves.
    private string? Offered(SetupStepView step) =>
        string.Equals(step.Id, MapStep, StringComparison.Ordinal)
            ? string.Equals(step.State, "failed", StringComparison.Ordinal) ? MapRefresh : null
            : step.Action is { Length: > 0 } served && !HasForm(step, served) ? served : null;

    // The repo form is in the row when the catalog lets the app send what it sends; otherwise the step keeps the terminal copy of the action.
    private bool HasForm(SetupStepView step, string action) =>
        Ready(action) && string.Equals(step.Id, "repo", StringComparison.Ordinal);

    private bool Ready(string action) => _catalog.IsReady(action);

    // The refresh and the agents' registration are the app's own button when the catalog lists them as ready.
    private bool OpensHere(string action) => (string.Equals(action, MapRefresh, StringComparison.Ordinal) || string.Equals(action, RegisterAction, StringComparison.Ordinal)) && Ready(action);

    private void Open(string action)
    {
        if (string.Equals(action, MapRefresh, StringComparison.Ordinal))
        {
            _ = RefreshMap(CancellationToken.None);
        }
        else if (string.Equals(action, RegisterAction, StringComparison.Ordinal) && OpensHere(action))
        {
            _ = RegisterMcp(CancellationToken.None);
        }
        else
        {
            ActionRequested?.Invoke(this, new SetupActionEventArgs(action));
        }
    }

    // The forms of the repo step: each gets the row's own words for what it started or said.
    private void Attach(SetupStepViewModel row)
    {
        if (string.Equals(row.Id, "repo", StringComparison.Ordinal))
        {
            row.Repo = new SetupRepoFormViewModel(() => _url, _api, _folders, () => Ready("AddRepo"), note => Started(row, note));
            row.Repo.Update(row.Step);
            row.OpenIfFailedWithBody();
        }
    }

    private void Started(SetupStepViewModel row, string note)
    {
        Hold(row, note);
        Fold();
    }

    private bool Knows(string action, bool inApp) => inApp || _catalog.Row(action) is not null || Labels.SetupCli(action).Length > 0;
}
