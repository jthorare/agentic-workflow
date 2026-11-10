using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Toolkits;
using Aw.Sindri.Features.BuildToolkit;
using Aw.Sindri.Features.CreateToolkit;
using Aw.Sindri.Features.ListToolkitCalls;
using Aw.Sindri.Features.ListToolkits;
using Aw.Sindri.Features.PauseToolkit;
using Aw.Sindri.Features.ResumeToolkit;
using Aw.Sindri.Features.StartToolkit;
using Aw.Sindri.Features.StopToolkit;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri toolkit list|create|build|start|stop|pause|resume|calls' (agent-first PR 16c), each with --json: clients of the toolkit routes. 'create' and 'build' start a job and follow it like 'repo add'
/// (--detach prints the job id and returns); the other verbs answer at once. A session may run every verb.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verbs write.</param>
/// <param name="follower">Follows the create and build jobs.</param>
/// <param name="ensureRunning">Starts the control plane (true: this verb may launch it) or names why not; null: never start it.</param>
internal sealed class ToolkitVerbs(ControlPlaneClient client, Output output, JobFollower follower, Func<bool, CancellationToken, Task<Error?>>? ensureRunning = null)
{
    internal const string Usage = "usage: sindri toolkit list [--json] | create <name> --tool <tool> [--needs <list>] [--detach] [--json] | build <name> [--detach] [--json] | start|stop|pause|resume <name> [--json] | calls <name> [--limit <n>] [--json]";

    internal async Task<int> Run(string[] args, CancellationToken interrupt, CancellationToken again)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (ToolkitVerbLine.Parse([.. args.Skip(1)]) is not { } line)
        {
            return await output.Error(SindriErrors.InvalidInput.With(Usage)).ConfigureAwait(false);
        }

        var down = ensureRunning is null ? null : await ensureRunning(true, interrupt).ConfigureAwait(false);
        if (down is not null)
        {
            return await output.Error(down).ConfigureAwait(false);
        }

        var path = line.Name is null ? string.Empty : Uri.EscapeDataString(line.Name);
        return line.Verb switch
        {
            "list" => await Show(await client.Get<ListToolkitsResponse>("/sindri/toolkits", interrupt).ConfigureAwait(false), line.Json, Lines).ConfigureAwait(false),
            "calls" => await Show(await client.Get<ListToolkitCallsResponse>($"/sindri/toolkits/{path}/calls" + (line.Limit is { } n ? "?limit=" + n.ToString(CultureInfo.InvariantCulture) : string.Empty), interrupt).ConfigureAwait(false), line.Json, CallLines).ConfigureAwait(false),
            "create" => await Finish(await client.Post<CreateToolkitRequest, JobStartedResponse>("/sindri/toolkits", new CreateToolkitRequest(line.Name ?? string.Empty, line.Tool ?? string.Empty, line.Needs), interrupt).ConfigureAwait(false), line, interrupt, again).ConfigureAwait(false),
            "build" => await Finish(await client.Post<BuildToolkitRequest, JobStartedResponse>("/sindri/toolkits/build", new BuildToolkitRequest(line.Name ?? string.Empty), interrupt).ConfigureAwait(false), line, interrupt, again).ConfigureAwait(false),
            _ => await Change(line, path, interrupt).ConfigureAwait(false),
        };
    }

    private async Task<int> Change(ToolkitVerbLine line, string path, CancellationToken ct)
    {
        var route = $"/sindri/toolkits/{path}/{line.Verb}";
        return line.Verb switch
        {
            "start" => await Show(await client.Post<object?, StartToolkitResponse>(route, body: null, ct).ConfigureAwait(false), line.Json, static r => State(r.Name, r.State)).ConfigureAwait(false),
            "stop" => await Show(await client.Post<object?, StopToolkitResponse>(route, body: null, ct).ConfigureAwait(false), line.Json, static r => State(r.Name, r.State)).ConfigureAwait(false),
            "pause" => await Show(await client.Post<object?, PauseToolkitResponse>(route, body: null, ct).ConfigureAwait(false), line.Json, static r => State(r.Name, r.State)).ConfigureAwait(false),
            _ => await Show(await client.Post<object?, ResumeToolkitResponse>(route, body: null, ct).ConfigureAwait(false), line.Json, static r => State(r.Name, r.State)).ConfigureAwait(false),
        };
    }

    private async Task<int> Finish(Result<ApiAnswer<JobStartedResponse>> started, ToolkitVerbLine line, CancellationToken interrupt, CancellationToken again)
    {
        if (started.IsError)
        {
            return await output.Error(started.Error).ConfigureAwait(false);
        }

        if (!line.Detach)
        {
            return await follower.Follow(started.Value.Value, line.Json, interrupt, again).ConfigureAwait(false);
        }

        return line.Json
            ? await output.Raw(started.Value.Body, 0).ConfigureAwait(false)
            : await output.Text($"job {started.Value.Value.JobId.Value} started\n", 0).ConfigureAwait(false);
    }

    private static string State(string name, ToolkitState state) => $"{name} {state.ToString().ToLowerInvariant()}\n";

    private static string Lines(ListToolkitsResponse list) =>
        list.Toolkits.Count == 0
            ? "no toolkits\n"
            : string.Concat(list.Toolkits.Select(static t => $"{t.Name}  {t.State.ToString().ToLowerInvariant()}" + (t.Manifest is { Tools.Count: > 0 } m ? "  " + string.Join(", ", m.Tools.Select(static x => x.Name)) : string.Empty) + "\n"));

    private static string CallLines(ListToolkitCallsResponse list) =>
        list.Calls.Count == 0
            ? "no calls\n"
            : string.Concat(list.Calls.Select(static c => string.Create(CultureInfo.InvariantCulture, $"{c.At.UtcDateTime:yyyy-MM-dd HH:mm:ss}Z  {c.Tool}  {c.Caller}  {c.DurationMs} ms") + (c.Error is { Length: > 0 } e ? "  " + e : string.Empty) + "\n"));

    private async Task<int> Show<T>(Result<ApiAnswer<T>> answer, bool json, Func<T, string> render)
    {
        if (answer.IsError)
        {
            return await output.Error(answer.Error).ConfigureAwait(false);
        }

        return json ? await output.Raw(answer.Value.Body, 0).ConfigureAwait(false) : await output.Text(render(answer.Value.Value), 0).ConfigureAwait(false);
    }
}
