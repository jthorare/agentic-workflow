using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Features.CreateMemory;
using Aw.Sindri.Features.ForgetMemoryFact;
using Aw.Sindri.Features.ListMemories;
using Aw.Sindri.Features.SearchMemory;
using Aw.Sindri.Features.WriteMemoryFact;
using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri memory list|create|search|write|forget' (agent-first PR 13): a client of the /sindri/memories routes. A session may run every verb.
/// create &lt;name&gt; [--description T], search &lt;name&gt; &lt;query&gt; [--level loose|balanced|strict] [--limit N], write &lt;name&gt; &lt;text&gt; [--tag T]... [--expires-at D],
/// forget &lt;name&gt; (--fact KEY | --tag T | --expired); every verb takes --json.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verbs write.</param>
/// <param name="ensureRunning">Starts the control plane (true: this verb may launch it) or names why not; null: never start it.</param>
internal sealed class MemoryVerbs(ControlPlaneClient client, Output output, Func<bool, CancellationToken, Task<Error?>>? ensureRunning = null)
{
    private readonly ControlPlaneClient _client = client;
    private readonly Output _output = output;
    private readonly Func<bool, CancellationToken, Task<Error?>>? _ensureRunning = ensureRunning;

    internal async Task<int> Run(string[] args, CancellationToken ct)
    {
        var parsed = MemoryVerbArguments.Parse(args);
        if (parsed.IsError)
        {
            return await _output.Error(parsed.Error).ConfigureAwait(false);
        }

        var down = _ensureRunning is null ? null : await _ensureRunning(true, ct).ConfigureAwait(false);
        if (down is not null)
        {
            return await _output.Error(down).ConfigureAwait(false);
        }

        return parsed.Value.Verb switch
        {
            "list" => await Show(await _client.Get<ListMemoriesResponse>("/sindri/memories", ct).ConfigureAwait(false), parsed.Value.Json, Lines).ConfigureAwait(false),
            "create" => await Show(await _client.Post<CreateMemoryRequest, CreateMemoryResponse>("/sindri/memories", new CreateMemoryRequest(parsed.Value.Name, parsed.Value.Description), ct).ConfigureAwait(false),
                parsed.Value.Json, static a => $"made {a.Store.Name}\n").ConfigureAwait(false),
            "search" => await Show(await _client.Get<SearchMemoryResponse>(
                $"/sindri/memories/{Uri.EscapeDataString(parsed.Value.Name)}/search" + UrlQuery.Of([("query", parsed.Value.Text), ("level", parsed.Value.Level), ("limit", parsed.Value.Limit)]), ct).ConfigureAwait(false), parsed.Value.Json, Lines).ConfigureAwait(false),
            "write" => await Show(await _client.Post<WriteMemoryFactBody, WriteMemoryFactResponse>(
                $"/sindri/memories/{Uri.EscapeDataString(parsed.Value.Name)}/facts", new WriteMemoryFactBody(parsed.Value.Text, parsed.Value.Tags.Count == 0 ? null : parsed.Value.Tags, parsed.Value.ExpiresAt, Provenance: null), ct).ConfigureAwait(false),
                parsed.Value.Json, static a => $"{a.Action} {a.Key}\n").ConfigureAwait(false),
            _ => await Show(await _client.Delete<ForgetMemoryFactResponse>(
                $"/sindri/memories/{Uri.EscapeDataString(parsed.Value.Name)}/facts" + UrlQuery.Of([("fact", parsed.Value.Fact), ("tag", parsed.Value.Tag), ("expired", parsed.Value.Expired ? "true" : null)]), ct).ConfigureAwait(false),
                parsed.Value.Json, static a => string.Create(CultureInfo.InvariantCulture, $"forgot {a.Deleted}\n")).ConfigureAwait(false),
        };
    }

    private static string Lines(ListMemoriesResponse list) =>
        list.Memories.Count == 0 ? "no memory stores\n" : string.Concat(list.Memories.Select(static m => string.Create(CultureInfo.InvariantCulture, $"{m.Name}  {m.Facts} facts  {m.Description}\n")));

    private static string Lines(SearchMemoryResponse found) =>
        $"mode: {found.Mode}\n" + (found.Note is { } note ? note + "\n" : string.Empty)
        + string.Concat(found.Facts.Select(static f => string.Create(CultureInfo.InvariantCulture, $"{f.Score:0.00}  {f.Key}  {f.Text}\n")));

    private async Task<int> Show<T>(Result<ApiAnswer<T>> answer, bool json, Func<T, string> render)
    {
        if (answer.IsError)
        {
            return await _output.Error(answer.Error).ConfigureAwait(false);
        }

        return json ? await _output.Raw(answer.Value.Body, 0).ConfigureAwait(false) : await _output.Text(render(answer.Value.Value), 0).ConfigureAwait(false);
    }
}
