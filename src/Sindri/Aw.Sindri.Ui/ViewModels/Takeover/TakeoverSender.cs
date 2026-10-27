using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Takeover;

/// <summary>
/// Sends "ApplyCommand:takeover" for a surface that has no Attach control (the run inspectors): once per press, never retried, and says
/// what came of it: the viewer's outcome, or the refusal with its code. A server that served no viewer is asked for the launch.
/// </summary>
internal sealed class TakeoverSender(ISindriApi api, Func<Uri?> apiUrl)
{
    private bool _pressed;

    /// <summary>One press: shows "Sending…", sends, then shows the answer. A press while one is out does nothing.</summary>
    /// <param name="instance">The instance to take over.</param>
    /// <param name="launchId">The launch of its agent Step, read when the answer served no viewer; null for none.</param>
    /// <param name="show">Receives each state of the press.</param>
    /// <returns>A task that completes when the answer was shown.</returns>
    internal async Task Press(string instance, string? launchId, Action<TakeoverView> show)
    {
        ArgumentNullException.ThrowIfNull(show);
        if (_pressed)
        {
            return;
        }

        _pressed = true;
        try
        {
            show(new TakeoverView(Labels.AttachSending, string.Empty));
            show(await Send(instance, launchId).ConfigureAwait(true));
        }
        finally
        {
            _pressed = false;
        }
    }

    private async Task<TakeoverView> Send(string instance, string? launchId)
    {
        if (apiUrl() is not { } endpoint)
        {
            return new TakeoverView(Labels.TakeoverNotSent, string.Empty);
        }

        var result = await api.ApplyCommand(endpoint, instance, new ApplyCommandBody(instance, CommandKind.Takeover), CancellationToken.None).ConfigureAwait(true);

        if (result.IsError)
        {
            return new TakeoverView(Labels.TakeoverRefused(result.Error.Code, result.Error.Message), string.Empty);
        }

        var (viewer, attach) = (result.Value.Viewer, result.Value.Attach);
        if (viewer is null && launchId is not null && apiUrl() is { } url && await api.Launch(url, launchId, CancellationToken.None).ConfigureAwait(true) is { IsError: false } read)
        {
            (viewer, attach) = (read.Value.Viewer, attach ?? read.Value.Attach);
        }

        var (line, command) = ViewerWords.TakeoverOutcome(viewer, attach);
        return new TakeoverView(line, command);
    }
}
