using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// The one way the inbox sends a command to an instance (a hint or a refusal answer, a cancel, a hand back): a command is sent once and
/// never retried by the app. A press while a command for the same instance is in flight does nothing; a refusal leaves the control pressable.
/// </summary>
internal sealed class InstanceCommander(ISindriApi api, Func<Uri?> apiUrl)
{
    private readonly HashSet<string> _inFlight = new(StringComparer.Ordinal);

    /// <summary>True when a command for the instance is in flight.</summary>
    /// <param name="instanceId">The instance.</param>
    /// <returns>True while it is.</returns>
    internal bool IsInFlight(string instanceId) => _inFlight.Contains(instanceId);

    /// <summary>Sends the command; the guard is read and set before the first await.</summary>
    /// <param name="body">The command, with its instance.</param>
    /// <returns>The answer, or null when a command for the instance was in flight or no endpoint is known.</returns>
    internal async Task<Result<ApplyCommandAccepted>?> Send(ApplyCommandBody body)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (apiUrl() is not { } url || !_inFlight.Add(body.Instance))
        {
            return null;
        }

        try
        {
            return await api.ApplyCommand(url, body.Instance, body, CancellationToken.None).ConfigureAwait(true);
        }
        finally
        {
            _ = _inFlight.Remove(body.Instance);
        }
    }
}
