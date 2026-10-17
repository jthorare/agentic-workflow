using System.Globalization;
using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Features.GetItem;
using Aw.Sindri.Features.GetLaunch;
using Aw.Sindri.Features.OpenItem;
using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri open &lt;item&gt; [--agent &lt;id&gt;] [--attach | --resume] [--version N] [--no-wait] [--json] [--no-start]' (UI spec E-11, API P10a): asks for a
/// launch and then follows it, because a launch is the launcher service's work: it reads the item's version when none is given (as 'item close' does),
/// posts the open, and polls the launch until it started or failed (the launcher answers within 30 s). A started launch prints how the viewer fared and,
/// when no window opened, the command that attaches. Exit 0 started (or accepted with --no-wait), 1 not running or no outcome yet, 2 refused, failed or invalid.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verb writes.</param>
/// <param name="ensureRunning">Starts the control plane (true: this verb may launch it) or names why not.</param>
/// <param name="clock">The time the polling waits on.</param>
internal sealed class OpenCommand(ControlPlaneClient client, Output output, Func<bool, CancellationToken, Task<Error?>> ensureRunning, TimeProvider clock)
{
    private static readonly TimeSpan _poll = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan _patience = LaunchRules.Deadline + TimeSpan.FromSeconds(5);

    internal async Task<int> Run(string[] args, CancellationToken ct)
    {
        var parsed = OpenArguments.Parse(args);
        if (parsed.IsError)
        {
            return await output.Error(parsed.Error).ConfigureAwait(false);
        }

        var open = parsed.Value;
        if (await ensureRunning(!open.NoStart, ct).ConfigureAwait(false) is { } down)
        {
            return await output.Error(down).ConfigureAwait(false);
        }

        var route = "/sindri/items/" + Uri.EscapeDataString(open.Item);
        var version = open.Version;
        if (version is null)
        {
            var read = await client.GetView<ItemView>(route, ct).ConfigureAwait(false);   // the API always wants a version: read the one the human would have seen
            if (read.IsError)
            {
                return await output.Error(read.Error).ConfigureAwait(false);
            }

            version = read.Value.Value.Version;
        }

        var asked = await client.Post<OpenItemBody, LaunchRequested>(route + "/open", new OpenItemBody(open.Mode, version.Value, open.Agent), ct).ConfigureAwait(false);
        if (asked.IsError)
        {
            return await output.Error(asked.Error).ConfigureAwait(false);
        }

        var requested = asked.Value.Value;
        if (open.NoWait)
        {
            return open.Json ? await output.Raw(asked.Value.Body, 0).ConfigureAwait(false) : await output.Text(Accepted(open.Item, requested), 0).ConfigureAwait(false);
        }

        return await Follow(open, requested, asked.Value.Body, ct).ConfigureAwait(false);
    }

    private async Task<int> Follow(OpenArguments open, LaunchRequested requested, ReadOnlyMemory<byte> acceptedBody, CancellationToken ct)
    {
        var started = clock.GetTimestamp();
        while (true)
        {
            var read = await client.GetView<LaunchView>("/sindri/launches/" + requested.Launch.Value, ct).ConfigureAwait(false);
            if (read.IsError)
            {
                return await output.Error(read.Error).ConfigureAwait(false);
            }

            var view = read.Value.Value;
            if (view.State != LaunchState.Requested)
            {
                return open.Json ? await output.Raw(read.Value.Body, view.State == LaunchState.Failed ? 2 : 0).ConfigureAwait(false) : await Report(open, requested, view).ConfigureAwait(false);
            }

            if (clock.GetElapsedTime(started) >= _patience)
            {
                return open.Json
                    ? await output.Raw(acceptedBody, 1).ConfigureAwait(false)
                    : await output.Text(Accepted(open.Item, requested) + $"  no outcome yet (fix: run `sindri doctor`; follow it with `sindri ledger --item {open.Item}`)\n", 1).ConfigureAwait(false);
            }

            await Task.Delay(_poll, clock, ct).ConfigureAwait(false);
        }
    }

    private async Task<int> Report(OpenArguments open, LaunchRequested requested, LaunchView view)
    {
        if (view.State == LaunchState.Failed)
        {
            var code = view.Code ?? SindriErrors.LaunchInterrupted.Code;
            var kind = SindriErrors.All.FirstOrDefault(d => string.Equals(d.Code, code, StringComparison.Ordinal))?.Kind ?? ErrorKind.Unavailable;
            return await output.Error(new Error(code, kind, view.Detail ?? "the launch failed")).ConfigureAwait(false);
        }

        var text = new StringBuilder();
        _ = text.Append(CultureInfo.InvariantCulture, $"{open.Item} {Verb(view.Mode)} in {view.Target.Value}");
        if (view.Viewer is { } viewer)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $" ({viewer.Kind}: {Words(viewer.Outcome)})");
        }

        _ = text.AppendLine();
        AttachLine(text, view);
        foreach (var warning in requested.Warnings)
        {
            _ = text.Append("  warning: ").AppendLine(warning);
        }

        return await output.Text(text.ToString(), 0).ConfigureAwait(false);
    }

    /// <summary>No window opened: the person attaches from any terminal.</summary>
    private static void AttachLine(StringBuilder text, LaunchView view)
    {
        if (view.Attach is { Count: > 0 } attach && view.Viewer?.Outcome != ViewerOutcomeKind.Opened)
        {
            _ = text.Append("  attach: ").AppendJoin(' ', attach).AppendLine();
        }
    }

    private static string Accepted(string item, LaunchRequested requested)
    {
        var text = new StringBuilder();
        _ = text.Append(CultureInfo.InvariantCulture, $"{item} -> launch {requested.Launch.Value} ({requested.Target.Value}, {requested.ChosenBy}; sequence {requested.Accepted.Sequence})\n");
        foreach (var warning in requested.Warnings)
        {
            _ = text.Append("  warning: ").AppendLine(warning);
        }

        return text.ToString();
    }

    private static string Verb(LaunchMode mode) => mode switch
    {
        LaunchMode.Attach => "attached",
        LaunchMode.Resume => "resumed",
        LaunchMode.Open => "opened",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "not a launch mode"),
    };

    private static string Words(ViewerOutcomeKind outcome) => outcome switch
    {
        ViewerOutcomeKind.Opened => "opened",
        ViewerOutcomeKind.Unconfirmed => "not confirmed yet",
        ViewerOutcomeKind.Skipped => "no window",
        ViewerOutcomeKind.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "not a viewer outcome"),
    };
}
