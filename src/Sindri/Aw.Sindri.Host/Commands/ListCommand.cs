using System.Text;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Features.ListInstances;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri workflow list [--needs-me | --item &lt;id&gt; | --state &lt;s&gt;] [--recipe &lt;id&gt;] [--urgency incident|triage|work] [--subject &lt;fact&gt;] [--root true|false] [--query &lt;visibility query&gt;] [--json]'
/// (engine spec §6.3; engine P4 unit (e)): one header line per instance, built from the served 'InstanceList' rows; '--json' prints that list. A client of serve's 'GET /workflow/instances' (review run 2); it never starts serve. '--needs-me' shows <b>Decisions</b> and then
/// <b>Tasks</b>, each under its heading; the typed filters AND together and the list is in the API order (<see cref="InstanceOrder"/>:
/// unfinished before finished, not parked or escalated before parked or escalated, incident, triage, then work, the subject label, the address); empty, it prints the sentence the server served for the filter ('nothing needs you', 'No workflows are open.' or 'No workflows match.'), and 'nothing needs you' when no row falls under either heading.
/// Exit 0, empty included; 1 when Sindri isn't running; 2 on any other error.
/// </summary>
internal static class ListCommand
{
    private const string Usage = "usage: sindri workflow list [--needs-me | --item <id> | --state <s>] [--recipe <id>] [--urgency incident|triage|work] [--subject <fact>] [--root true|false] [--query <visibility query>] [--json]";

    internal static async Task<int> Run(string[] args, RecipeDesk desk, IServiceProvider services, Output output, CancellationToken ct)
    {
        var json = args.Contains("--json", StringComparer.Ordinal);
        var rest = args.Where(static a => a != "--json").ToArray();
        if (Misspelled(rest) is { } problem)
        {
            return await output.Error(SindriErrors.InvalidInput.With(problem)).ConfigureAwait(false);
        }

        var parsed = Parse(rest);
        if (parsed is null)
        {
            return await output.Error(SindriErrors.InvalidInput.With(Usage)).ConfigureAwait(false);
        }

        var request = parsed.Item is null && parsed.State is null ? parsed with { Open = "true" } : parsed;   // the list shows running instances unless an item or a state is asked for

        var listed = await desk.Client.Get<InstanceList>(Route(request), ct).ConfigureAwait(false);
        if (listed.IsError)
        {
            return await output.Error(InstanceReads.NotRunning(listed.Error)).ConfigureAwait(false);
        }

        return json
            ? await output.Json(listed.Value.Value, 0).ConfigureAwait(false)
            : await output.Text(Text(listed.Value.Value, request, services.GetRequiredService<TimeProvider>().GetUtcNow()), 0).ConfigureAwait(false);
    }

    /// <summary>'GET /workflow/instances' with each filter given as its query parameter.</summary>
    private static string Route(ListInstancesRequest request)
    {
        (string Name, string? Value)[] filters =
        [
            ("needsMe", request.NeedsMe),
            ("item", request.Item?.Value),
            ("state", request.State),
            ("recipe", request.Recipe),
            ("urgency", request.Urgency),
            ("subject", request.Subject),
            ("root", request.Root),
            ("open", request.Open),
            ("query", request.Query),
        ];
        var parts = filters.Where(static f => f.Value is not null).Select(static f => $"{f.Name}={Uri.EscapeDataString(f.Value!)}");   // why: Where kept only the non-null values
        var query = string.Join('&', parts);
        return query.Length == 0 ? InstanceReads.Instances : $"{InstanceReads.Instances}?{query}";
    }

    /// <summary>The flags, each at most once, '--needs-me' alone of the filters it excludes; null when they don't parse.</summary>
    private static ListInstancesRequest? Parse(string[] args)
    {
        var request = new ListInstancesRequest();
        for (var i = 0; i < args.Length; i++)
        {
            var (applied, used) = Apply(request, args[i], i + 1 < args.Length ? args[i + 1] : null);
            if (applied is null)
            {
                return null;
            }

            request = applied;
            i += used ? 1 : 0;
        }

        return request.NeedsMe is not null && (request.Item is not null || request.State is not null) ? null : request;
    }

    /// <summary>One flag applied: the request after it (null when it doesn't parse) and whether it took the next argument.</summary>
    private static (ListInstancesRequest? Request, bool UsedValue) Apply(ListInstancesRequest request, string flag, string? value) => flag switch
    {
        "--needs-me" when request.NeedsMe is null => (request with { NeedsMe = "true" }, false),
        "--item" when request.Item is null && value is not null && ItemKey.TryFrom(value, out var key) => (request with { Item = key }, true),
        "--state" when request.State is null && value is not null => (request with { State = value }, true),
        "--recipe" when request.Recipe is null && value is not null => (request with { Recipe = value }, true),
        "--urgency" when request.Urgency is null && value is not null && Urgencies.IsKnown(value) => (request with { Urgency = value }, true),
        "--subject" when request.Subject is null && value is not null => (request with { Subject = value }, true),
        "--root" when request.Root is null && Boolean(value) is not null => (request with { Root = value }, true),
        "--query" when request.Query is null && value is not null => (request with { Query = value }, true),
        _ => (null, false),
    };

    /// <summary>The value of '--urgency' or '--root' that isn't one of its words, with the fix; null when they are fine.</summary>
    private static string? Misspelled(string[] args)
    {
        for (var i = 0; i + 1 < args.Length; i++)
        {
            if (args[i] == "--urgency" && !Urgencies.IsKnown(args[i + 1]))
            {
                return $"'{args[i + 1]}' isn't an urgency (fix: --urgency incident, triage or work)";
            }

            if (args[i] == "--root" && Boolean(args[i + 1]) is null)
            {
                return $"'{args[i + 1]}' isn't true or false (fix: --root true for top-level instances, --root false for children)";
            }
        }

        return null;
    }

    private static bool? Boolean(string? value) => value switch
    {
        "true" => true,
        "false" => false,
        _ => null,
    };

    private static string Text(InstanceList listed, ListInstancesRequest request, DateTimeOffset now)
    {
        if (listed.Instances.Count == 0)
        {
            return (listed.Empty ?? InstanceList.NoMatchText) + "\n";   // the served sentence matches the filter
        }

        var text = new StringBuilder();
        if (request.NeedsMe is not null)
        {
            Section(text, "Decisions", [.. listed.Instances.Where(static r => Chip(r, StateChip.Parked) || Chip(r, StateChip.Escalated) || Chip(r, StateChip.WithYou))], now);
            Section(text, "Tasks", [.. listed.Instances.Where(static r => Chip(r, StateChip.Decision))], now);
            return text.Length == 0 ? NeedsYouList.Nothing + "\n" : text.ToString();   // rows that are in neither section (queued, running) need nobody
        }

        foreach (var row in listed.Instances)
        {
            _ = text.Append(Line(row, now)).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>One row as a header line: the address, the recipe at its hash, the state with its 'now' sentence, and how long it has been there.</summary>
    private static string Line(InstanceSummary row, DateTimeOffset now) => $"{row.Address.Value}  {row.Recipe.Id.Value}@{StatusText.Hash12(row.Recipe.Hash.Value)}  {row.State}: {row.Now}  since {StatusText.Clock(row.Since, now)} ({StatusText.Duration(now - row.Since)})";

    private static bool Chip(InstanceSummary row, string word) => string.Equals(row.Chip.Word, word, StringComparison.Ordinal);

    private static void Section(StringBuilder text, string heading, IReadOnlyList<InstanceSummary> rows, DateTimeOffset now)
    {
        if (rows.Count == 0)
        {
            return;
        }

        _ = text.Append(heading).Append('\n');
        foreach (var row in rows.OrderBy(static r => r.Address.Value, StringComparer.Ordinal))   // spec §6.3: each section by address
        {
            _ = text.Append("  ").Append(Line(row, now)).Append('\n');
        }
    }
}
