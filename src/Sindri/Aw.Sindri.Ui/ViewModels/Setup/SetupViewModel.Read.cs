using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Setup;
using Aw.Sindri.Ui.ViewModels.Approvals;

namespace Aw.Sindri.Ui.ViewModels.Setup;

/// <summary>Reading the list (<see cref="SetupViewModel"/>, continued): on connect and with every event, newest answer wins, and what is shown when a read fails.</summary>
internal sealed partial class SetupViewModel
{
    /// <summary>Re-reads the list (on connect and on every event).</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the list is shown.</returns>
    internal Task Refresh(Uri url, CancellationToken cancellationToken)
    {
        _url = url;
        return Read(url, cancellationToken);
    }

    /// <summary>Reads again after a failed first read.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task Retry(CancellationToken cancellationToken) => _url is { } url ? Read(url, cancellationToken) : Task.CompletedTask;

    private async Task Read(Uri url, CancellationToken cancellationToken)
    {
        var read = ++_reads;
        var answer = await _api.Setup(url, cancellationToken).ConfigureAwait(true);
        if (read != _reads)
        {
            return;   // a newer read was asked for since: its answer is the one to show
        }

        if (answer.IsSuccess)
        {
            var recipes = await Workflows(url, cancellationToken).ConfigureAwait(true);
            if (read != _reads)
            {
                return;
            }

            Show(answer.Value, recipes, read);
        }
        else
        {
            Fail(answer.Error);
        }
    }

    // A route this Sindri doesn't have marks only the card (it draws none); a shape this build can't read is a sentence; nothing answering is "Sindri isn't running" when there are
    // no steps to show, else the last good steps stay, marked stale. Any other failure is a sentence with a retry, or stale steps.
    private void Fail(Error error)
    {
        IsReading = false;
        if (error.Kind == ErrorKind.NotFound)
        {
            IsNotServed = true;
            _rows.Clear();   // a Sindri without the route shows only that, never the rows of an earlier read
            Steps.Clear();
        }
        else if (string.Equals(error.Code, SindriUiErrors.BadAnswer.Code, StringComparison.Ordinal))
        {
            IsUnreadable = true;
        }
        else if (Steps.Count > 0)
        {
            IsStale = true;
        }
        else if (string.Equals(error.Code, SindriUiErrors.Unreachable.Code, StringComparison.Ordinal))
        {
            IsNotRunning = true;
        }
        else
        {
            (IsFailed, FailedText) = (true, Labels.SetupFailed(error));
        }

        Fold();
    }

    // The first time the list shows and no step opens by itself, the first one still to do opens: a first run starts at its first step.
    private void OpenTheFirstStep()
    {
        if (!_hasRead && Steps.All(s => !s.IsOpen) && Steps.FirstOrDefault(s => !s.IsFinished) is { } first)
        {
            first.IsOpen = true;
        }
    }

    // The workflows under the workflows step: a read that fails shows none (the step is then as the server says), never an error of its own.
    private async Task<SetupRecipeListViewModel> Workflows(Uri url, CancellationToken cancellationToken)
    {
        var list = await _api.Recipes(url, cancellationToken).ConfigureAwait(true);
        return list.IsSuccess ? new SetupRecipeListViewModel(list.Value.Recipes) : SetupRecipeListViewModel.Empty;
    }

    // The server counts a workflow approved while its approval is older than what ships; the app does not: the row needs the person, with the one approve-all action.
    private static SetupStepView Outdated(SetupStepView step, SetupRecipeListViewModel recipes) =>
        string.Equals(step.Id, RecipesStep, StringComparison.Ordinal) && recipes.HasOutdated && string.Equals(step.State, "done", StringComparison.Ordinal)
            ? step with { State = "needs-you", Action = ApproveAllViewModel.Action }
            : step;

    private void Show(SetupView view, SetupRecipeListViewModel recipes, int read)
    {
        var said = new List<string>();
        var served = new List<SetupStepViewModel>(view.Steps.Count);
        foreach (var served0 in view.Steps)
        {
            var own = string.Equals(served0.Id, RecipesStep, StringComparison.Ordinal) ? recipes : SetupRecipeListViewModel.Empty;
            served.Add(Place(Outdated(served0, own), own, served.Count + 1, read, said));
        }

        Forget(view);
        Patch.To(Steps, served);
        ConfirmAll(served);
        (IsReading, IsNotServed, IsUnreadable, IsStale, IsFailed, IsNotRunning, FailedText) = (false, false, false, false, false, false, string.Empty);
        OpenTheFirstStep();
        _hasRead = true;
        Fold();
        if (said.Count > 0)
        {
            Live.Say(string.Join(". ", said));
        }
    }

    // The row of a step: the one kept from an earlier read, updated in place, or a new one; the workflows are given before it words itself.
    private SetupStepViewModel Place(SetupStepView step, SetupRecipeListViewModel own, int number, int read, List<string> said)
    {
        if (_rows.TryGetValue(step.Id, out var kept))
        {
            kept.ShowRecipes(own);
            if (kept.Update(step, keepWorking: read <= _holdUntil) && _hasRead)
            {
                said.Add(Labels.SetupChanged(kept.Title, kept.ChipWord));
            }

            return kept;
        }

        var row = new SetupStepViewModel(step, number, ActionOf);
        row.ShowRecipes(own);
        _ = row.Update(step);   // words itself with the workflows now that it has them
        Attach(row);
        _rows[step.Id] = row;
        return row;
    }

    private void Forget(SetupView view)
    {
        var present = new HashSet<string>(view.Steps.Select(s => s.Id), StringComparer.Ordinal);
        foreach (var gone in _rows.Keys.Where(k => !present.Contains(k)).ToList())
        {
            _ = _rows.Remove(gone);
        }
    }

    private static void ConfirmAll(List<SetupStepViewModel> rows)
    {
        foreach (var row in rows)
        {
            Confirm(row);
        }
    }
}
