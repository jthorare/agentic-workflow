using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Features.ActivateWorkflow;
using Aw.Sindri.Features.ApplyWorkflowOps;
using Aw.Sindri.Features.CreateWorkflow;
using Aw.Sindri.Features.DeleteWorkflow;
using Aw.Sindri.Features.PauseWorkflow;
using Aw.Sindri.Features.ReplaceWorkflow;
using Aw.Sindri.Features.ValidateWorkflow;
using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri wf create|edit|activate|pause|delete' and 'wf validate --slug' (each with --json): clients of the workflow write routes. Like 'wf run' they start the control plane first.
/// Exit 0 ok, 1 not running, 2 refused or error (a stale base revision is a refusal that names the current revision).
/// </summary>
/// <param name="desk">The API client, the start rule and the terminal.</param>
/// <param name="output">Where the verbs write.</param>
internal sealed class WfWriteVerbs(RecipeDesk desk, Output output)
{
    internal const string CreateUsage = "usage: sindri wf create <slug> (--template <name> | --file <spec.json>) [--json]";
    internal const string EditUsage = "usage: sindri wf edit <slug> --base <revision> (--ops <json> | --ops-file <file> | --file <spec.json>) [--json]";
    internal const string ActivateUsage = "usage: sindri wf activate <slug> [--revision <n>] [--json]";
    internal const string PauseUsage = "usage: sindri wf pause <slug> [--json]";
    internal const string DeleteUsage = "usage: sindri wf delete <slug> [--json]";
    internal const string ValidateUsage = "usage: sindri wf validate --slug <slug> [--revision <n>] [--json]  (or: sindri wf validate <file> [--json])";

    internal async Task<int> Create(string[] args, CancellationToken ct)
    {
        var flags = Flags.Read(args, CreateUsage, "--template", "--file");
        if (flags.Bad is { } bad)
        {
            return await output.Error(bad).ConfigureAwait(false);
        }

        var spec = await ReadJson(flags.Value("--file"), CreateUsage, ct).ConfigureAwait(false);
        if (spec.IsError)
        {
            return await output.Error(spec.Error).ConfigureAwait(false);
        }

        if (flags.Slug is not { } slug)
        {
            return await output.Error(SindriErrors.InvalidInput.With("wf create needs a workflow slug (" + CreateUsage + ")")).ConfigureAwait(false);
        }

        var body = new CreateWorkflowBody(slug, flags.Value("--template"), spec.Value);
        return await Send<CreateWorkflowBody, CreateWorkflowResponse>(HttpMethod.Post, "/sindri/workflows", body, flags.Json, static r => $"{r.Slug.Value} created at revision {r.Revision} (off)\n", ct).ConfigureAwait(false);
    }

    internal async Task<int> Edit(string[] args, CancellationToken ct)
    {
        var flags = Flags.Read(args, EditUsage, "--base", "--ops", "--ops-file", "--file");
        if (flags.Bad is { } bad)
        {
            return await output.Error(bad).ConfigureAwait(false);
        }

        if (flags.Slug is not { } slug || !int.TryParse(flags.Value("--base"), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var baseRevision) || baseRevision < 1)
        {
            return await output.Error(SindriErrors.InvalidInput.With("wf edit needs a slug and --base <revision>; 'sindri wf show <slug>' prints the newest revision (" + EditUsage + ")")).ConfigureAwait(false);
        }

        var given = new[] { "--ops", "--ops-file", "--file" }.Count(f => flags.Value(f) is not null);
        if (given != 1)
        {
            return await output.Error(SindriErrors.InvalidInput.With("give exactly one of --ops, --ops-file and --file (" + EditUsage + ")")).ConfigureAwait(false);
        }

        var route = "/sindri/workflows/" + Uri.EscapeDataString(slug);
        if (flags.Value("--file") is { } file)
        {
            var spec = await ReadJson(file, EditUsage, ct).ConfigureAwait(false);
            return spec.IsError
                ? await output.Error(spec.Error).ConfigureAwait(false)
                : await Send<ReplaceWorkflowBody, ReplaceWorkflowResponse>(HttpMethod.Put, route, new ReplaceWorkflowBody(baseRevision, spec.Value), flags.Json, static r => $"{r.Slug.Value} is now at revision {r.Revision}\n", ct).ConfigureAwait(false);
        }

        var ops = await ReadJson(flags.Value("--ops-file"), EditUsage, ct, inline: flags.Value("--ops")).ConfigureAwait(false);
        return ops.IsError
            ? await output.Error(ops.Error).ConfigureAwait(false)
            : await Send<ApplyWorkflowOpsBody, ApplyWorkflowOpsResponse>(HttpMethod.Post, route + "/ops", new ApplyWorkflowOpsBody(baseRevision, ops.Value), flags.Json, static r => $"{r.Slug.Value} is now at revision {r.Revision}\n", ct).ConfigureAwait(false);
    }

    internal async Task<int> Validate(string[] args, CancellationToken ct)
    {
        var flags = Flags.Read(args, ValidateUsage, "--slug", "--revision");
        if (flags.Bad is { } bad)
        {
            return await output.Error(bad).ConfigureAwait(false);
        }

        if (flags.Value("--slug") is not { } slug || flags.Slug is not null)
        {
            return await output.Error(SindriErrors.InvalidInput.With(ValidateUsage)).ConfigureAwait(false);
        }

        int? revision = null;
        if (flags.Value("--revision") is { } text)
        {
            if (!int.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var parsed) || parsed < 1)
            {
                return await output.Error(SindriErrors.InvalidInput.With("--revision is a number of 1 or more (" + ValidateUsage + ")")).ConfigureAwait(false);
            }

            revision = parsed;
        }

        return await Send<ValidateWorkflowBody, ValidateWorkflowResponse>(HttpMethod.Post, "/sindri/workflows/validate", new ValidateWorkflowBody(slug, revision, Spec: null), flags.Json, WfWriteText.Validation, ct).ConfigureAwait(false);
    }

    internal Task<int> Activate(string[] args, CancellationToken ct) => OneSlug(args, ActivateUsage, "--revision", ct, async (slug, flags) =>
    {
        int? revision = null;
        if (flags.Value("--revision") is { } text)
        {
            if (!int.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var parsed) || parsed < 1)
            {
                return await output.Error(SindriErrors.InvalidInput.With("--revision is a number of 1 or more (" + ActivateUsage + ")")).ConfigureAwait(false);
            }

            revision = parsed;
        }

        return await Send<ActivateWorkflowBody, ActivateWorkflowResponse>(HttpMethod.Post, "/sindri/workflows/" + Uri.EscapeDataString(slug) + "/activate", new ActivateWorkflowBody(revision), flags.Json, static r => $"{r.Slug.Value} is on at revision {r.Revision}\n", ct).ConfigureAwait(false);
    });

    internal Task<int> Pause(string[] args, CancellationToken ct) => OneSlug(args, PauseUsage, flag: null, ct, async (slug, flags) =>
        await Send<ActivateWorkflowBody, PauseWorkflowResponse>(HttpMethod.Post, "/sindri/workflows/" + Uri.EscapeDataString(slug) + "/pause", body: null, flags.Json, static r => $"{r.Slug.Value} is off\n", ct).ConfigureAwait(false));

    internal Task<int> Delete(string[] args, CancellationToken ct) => OneSlug(args, DeleteUsage, flag: null, ct, async (slug, flags) =>
        await Send<ActivateWorkflowBody, DeleteWorkflowResponse>(HttpMethod.Delete, "/sindri/workflows/" + Uri.EscapeDataString(slug), body: null, flags.Json, static r => $"{r.Slug.Value} is archived\n", ct).ConfigureAwait(false));

    private async Task<int> OneSlug(string[] args, string usage, string? flag, CancellationToken ct, Func<string, Flags, Task<int>> run)
    {
        ArgumentNullException.ThrowIfNull(args);
        var flags = flag is null ? Flags.Read(args, usage) : Flags.Read(args, usage, flag);
        if (flags.Bad is { } bad)
        {
            return await output.Error(bad).ConfigureAwait(false);
        }

        ct.ThrowIfCancellationRequested();
        return flags.Slug is { } slug ? await run(slug, flags).ConfigureAwait(false) : await output.Error(SindriErrors.InvalidInput.With("this verb needs a workflow slug (" + usage + ")")).ConfigureAwait(false);
    }

    private async Task<int> Send<TBody, T>(HttpMethod method, string route, TBody? body, bool json, Func<T, string> text, CancellationToken ct)
        where TBody : class
    {
        if (await desk.EnsureRunning(ct).ConfigureAwait(false) is { } down)
        {
            return await output.Error(down).ConfigureAwait(false);
        }

        var answer = method == HttpMethod.Put ? await desk.Client.Put<TBody, T>(route, body!, ct).ConfigureAwait(false)   // why: only the PUT caller (wf edit --file) passes a body
            : method == HttpMethod.Delete ? await desk.Client.Delete<T>(route, ct).ConfigureAwait(false)
            : await desk.Client.Post<TBody, T>(route, body, ct).ConfigureAwait(false);
        return answer.IsError
            ? await output.Error(answer.Error).ConfigureAwait(false)
            : json ? await output.Raw(answer.Value.Body, 0).ConfigureAwait(false) : await output.Text(text(answer.Value.Value), 0).ConfigureAwait(false);
    }

    /// <summary>A JSON document from a file (or, for <paramref name="inline"/>, from the argument itself).</summary>
    private static async Task<Result<JsonNode?>> ReadJson(string? file, string usage, CancellationToken ct, string? inline = null)
    {
        string? text = inline;
        if (text is null && file is not null)
        {
            try
            {
                text = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return SindriErrors.InvalidInput.With($"can't read '{file}': {ex.Message} ({usage})");
            }
        }

        if (text is null)
        {
            return (JsonNode?)null;
        }

        try
        {
            return JsonNode.Parse(text);
        }
        catch (System.Text.Json.JsonException ex)
        {
            return SindriErrors.InvalidInput.With($"the JSON can't be read: {ex.Message} ({usage})");
        }
    }

    /// <summary>The slug, the value flags and --json of one verb.</summary>
    private sealed record Flags(string? Slug, IReadOnlyDictionary<string, string> Values, bool Json, Error? Bad)
    {
        internal string? Value(string name) => Values.TryGetValue(name, out var v) ? v : null;

        internal static Flags Read(string[] args, string usage, params string[] valueFlags)
        {
            string? slug = null;
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            var json = false;
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i] == "--json")
                {
                    json = true;
                }
                else if (valueFlags.Contains(args[i], StringComparer.Ordinal))
                {
                    if (i + 1 >= args.Length)
                    {
                        return new Flags(slug, values, json, SindriErrors.InvalidInput.With($"'{args[i]}' needs a value ({usage})"));
                    }

                    values[args[i]] = args[++i];
                }
                else if (args[i].StartsWith('-'))
                {
                    return new Flags(slug, values, json, SindriErrors.InvalidInput.With($"'{args[i]}' isn't an option ({usage})"));
                }
                else if (slug is not null)
                {
                    return new Flags(slug, values, json, SindriErrors.InvalidInput.With($"'{args[i]}' is one argument too many ({usage})"));
                }
                else
                {
                    slug = args[i];
                }
            }

            return new Flags(slug, values, json, Bad: null);
        }
    }
}
