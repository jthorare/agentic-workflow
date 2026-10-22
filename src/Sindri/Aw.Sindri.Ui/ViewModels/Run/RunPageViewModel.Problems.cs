using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Run;

/// <summary>The run page's way to a row and to a problem (<see cref="RunPageViewModel"/>, continued): an address resolved to a run, a failed read worded.</summary>
internal sealed partial class RunPageViewModel
{
    // An address from a deep link names a row of the list: the one whose address it is, else the one it matches.
    private async Task<string?> Resolve(Uri url, string address, int generation, CancellationToken cancellationToken)
    {
        var found = await api.Instances(url, new InstanceQuery(Address: address), cancellationToken).ConfigureAwait(true);
        if (generation != _generation)
        {
            return null;
        }

        if (found.IsError)
        {
            Failed(found.Error);
            return null;
        }

        var rows = found.Value.Instances;
        if ((rows.FirstOrDefault(r => string.Equals(r.Address, address, StringComparison.Ordinal)) ?? (rows.Count == 1 ? rows[0] : null)) is not { } row)
        {
            Problem(RunPageState.NotFound, Labels.InstanceNotFound(address));
            return null;
        }

        return row.Id;
    }

    // A first read ends in a problem; a later one keeps what is shown.
    private void Failed(Error error)
    {
        if (Phase == RunPageState.Shown)
        {
            return;
        }

        if (error.Kind == ErrorKind.NotFound)
        {
            Problem(RunPageState.NotFound, error.Message);
        }
        else if (string.Equals(error.Code, SindriUiErrors.BadAnswer.Code, StringComparison.Ordinal))
        {
            Problem(RunPageState.Problem, Labels.StripUnreadable);
        }
        else
        {
            Problem(RunPageState.Problem, Labels.InstanceRefused(error));
        }
    }

    private void Problem(RunPageState phase, string text)
    {
        ProblemText = text;
        Phase = phase;
    }
}
