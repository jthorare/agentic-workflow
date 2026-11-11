using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Features.ListResources;
using Aw.Sindri.Features.PurgeResources;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri resources list [owner]' and 'sindri resources purge &lt;owner&gt;' (agent-first PR 14), each with --json: clients of GET /sindri/resources and POST /sindri/resources/purge.
/// The owner is workflow:&lt;slug&gt;, node:&lt;slug&gt;/&lt;id&gt; or toolkit:&lt;name&gt;. A session may run both verbs; a purge is refused while the owner is active or has a run in flight.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verbs write.</param>
/// <param name="ensureRunning">Starts the control plane (true: this verb may launch it) or names why not; null: never start it.</param>
internal sealed class ResourceVerbs(ControlPlaneClient client, Output output, Func<bool, CancellationToken, Task<Error?>>? ensureRunning = null)
{
    internal const string Usage = "usage: sindri resources list [<owner>] [--json] | sindri resources purge <owner> [--json]";

    internal async Task<int> Run(string[] args, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(args);
        var json = args.Contains("--json", StringComparer.Ordinal);
        var rest = args.Skip(1).Where(static a => !string.Equals(a, "--json", StringComparison.Ordinal)).ToArray();
        var verb = rest.FirstOrDefault();
        var operands = rest.Skip(1).ToArray();
        var bad = verb switch
        {
            "list" when operands.Length <= 1 && !operands.Any(IsOption) => null,
            "purge" when operands.Length == 1 && !IsOption(operands[0]) => null,
            _ => SindriErrors.InvalidInput.With(Usage),
        };
        if (bad is not null)
        {
            return await output.Error(bad).ConfigureAwait(false);
        }

        var down = ensureRunning is null ? null : await ensureRunning(true, ct).ConfigureAwait(false);
        if (down is not null)
        {
            return await output.Error(down).ConfigureAwait(false);
        }

        return verb == "list"
            ? await Show(await client.Get<ListResourcesResponse>("/sindri/resources" + (operands.Length == 1 ? "?owner=" + Uri.EscapeDataString(operands[0]) : string.Empty), ct).ConfigureAwait(false), json, Lines).ConfigureAwait(false)
            : await Show(await client.Post<PurgeResourcesRequest, PurgeResourcesResponse>("/sindri/resources/purge", new PurgeResourcesRequest(operands[0]), ct).ConfigureAwait(false), json,
                static a => string.Create(CultureInfo.InvariantCulture, $"purged {a.Removed} resources of {a.Owner}\n")).ConfigureAwait(false);
    }

    private static bool IsOption(string arg) => arg.StartsWith('-');

    private static string Lines(ListResourcesResponse list) =>
        list.Resources.Count == 0
            ? "no resources\n"
            : string.Concat(list.Resources.Select(static r => string.Create(CultureInfo.InvariantCulture, $"{r.Owner}  {r.Kind}  {r.Name}  {r.Size} bytes  {r.Path}\n")));

    private async Task<int> Show<T>(Result<ApiAnswer<T>> answer, bool json, Func<T, string> render)
    {
        if (answer.IsError)
        {
            return await output.Error(answer.Error).ConfigureAwait(false);
        }

        return json ? await output.Raw(answer.Value.Body, 0).ConfigureAwait(false) : await output.Text(render(answer.Value.Value), 0).ConfigureAwait(false);
    }
}
