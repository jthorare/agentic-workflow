using Aw.Sindri.Ui.Api.Setup;
using Aw.Sindri.Ui.ViewModels.Approvals;

namespace Aw.Sindri.Ui.ViewModels.Setup;

/// <summary>The setup list (<see cref="SetupViewModel"/>, continued): which action a step offers, and the forms of the repo and Onyx steps.</summary>
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

    // The map has no button until it failed; then Retry is the catalog's refresh (a retry of the build, never a service run). The repo and Onyx steps have a form instead of a button
    // while the catalog lets the app send what they send. Every other step offers what the API serves.
    private string? Offered(SetupStepView step) =>
        string.Equals(step.Id, MapStep, StringComparison.Ordinal)
            ? string.Equals(step.State, "failed", StringComparison.Ordinal) ? MapRefresh : null
            : step.Action is { Length: > 0 } served && !HasForm(step, served) ? served : null;

    // The profile and workflows rows confirm in the row (typed, or held) when the catalog lets the app; otherwise they keep the terminal copy of the action.
    private bool HasForm(SetupStepView step, string action) =>
        Ready(action) && (string.Equals(step.Id, "repo", StringComparison.Ordinal) || string.Equals(step.Id, OnyxStep, StringComparison.Ordinal)
            || (string.Equals(step.Id, ProfileStep, StringComparison.Ordinal) && string.Equals(action, ProfileAction, StringComparison.Ordinal))
            || (string.Equals(step.Id, RecipesStep, StringComparison.Ordinal) && string.Equals(action, ApproveAllViewModel.Action, StringComparison.Ordinal)));

    private bool Ready(string action) => _catalog.For(action) is not null;

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

    // The forms of the repo and Onyx steps: each gets the row's own words for what it started or said.
    private void Attach(SetupStepViewModel row)
    {
        if (string.Equals(row.Id, "repo", StringComparison.Ordinal))
        {
            row.Repo = new SetupRepoFormViewModel(() => _url, _api, _folders, () => Ready("AddRepo"), note => Started(row, note));
            row.Repo.Update(row.Step);
        }
        else if (string.Equals(row.Id, ProfileStep, StringComparison.Ordinal))
        {
            row.Profile = new SetupProfileConfirmViewModel(() => _url, _profileHash, _api);
        }
        else if (string.Equals(row.Id, RecipesStep, StringComparison.Ordinal))
        {
            row.Workflows = new ApproveAllViewModel(_catalog, () => _url, _api, _clock);
        }
        else if (string.Equals(row.Id, OnyxStep, StringComparison.Ordinal))
        {
            row.Onyx = new SetupOnyxViewModel(() => _url, _api, Ready, note => Started(row, note), row.Say);
            row.Onyx.Update(row.Step, row.IsWorking);
        }
    }

    private void Started(SetupStepViewModel row, string note)
    {
        Hold(row, note);
        Fold();
    }

    private bool Knows(string action, bool inApp) => inApp || _catalog.Row(action) is not null || Labels.SetupCli(action).Length > 0;

    // The confirmations in the profile and workflows rows follow their step: asked for while it needs the person, dropped when it does not. An approval under way is left to finish.
    private static void Confirm(SetupStepViewModel row)
    {
        if (row.Profile is { } profile)
        {
            _ = profile.Follow(row.NeedsYou);
        }
        else if (row.Workflows is { } all)
        {
            if (row.NeedsYou)
            {
                if (!all.IsOpen)
                {
                    _ = all.Open(CancellationToken.None);
                }
            }
            else if (all.IsOpen && all.Phase is not (ApproveAllPhase.Applying or ApproveAllPhase.Done))
            {
                all.Close();
            }
        }
    }
}
