using System.Text.Json.Nodes;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Expressions;
using Aw.Sindri.Core.Graph.Schedule;
using Aw.Sindri.Core.Graph.Validation;
using Aw.Sindri.Core.Memory;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri wf validate &lt;file&gt; [--json]': reads a workflow graph file and runs the validator in this process, never
/// starting or contacting serve. Exit 0 when nothing refuses, 1 on findings, 2 on a usage or tool error.
/// </summary>
internal static class WfValidateCommand
{
    private const string Usage = "usage: sindri wf validate <file> [--json]";

    /// <summary>Runs the verb.</summary>
    /// <param name="args">The arguments after 'wf validate'.</param>
    /// <param name="services">The container, for the block catalog and the names the validator asks about.</param>
    /// <param name="output">Where the answer goes.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The exit code.</returns>
    internal static async Task<int> Run(string[] args, IServiceProvider services, Output output, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(args);
        var (file, json) = args switch
        {
            [var plain] when !plain.StartsWith('-') => (plain, false),
            [var first, "--json"] when !first.StartsWith('-') => (first, true),
            ["--json", var last] when !last.StartsWith('-') => (last, true),
            _ => (null, false),
        };
        if (file is null)
        {
            return await output.Error(SindriErrors.InvalidInput.With($"wf validate needs one graph file ({Usage})")).ConfigureAwait(false);
        }

        string text;
        try
        {
            text = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return await output.Error(SindriErrors.InvalidInput.With($"can't read '{file}': {ex.Message} ({Usage})")).ConfigureAwait(false);
        }

        var spec = GraphJson.Parse(text);
        if (spec.IsError)
        {
            var refused = new GraphFinding(spec.Error.Code, GraphSeverity.Error, [], Field: null, spec.Error.Message, "fix the document so it matches the graph spec");
            return await Print([refused], hash: null, json, output).ConfigureAwait(false);
        }

        var world = services.GetService<IGraphWorld>() ?? await ProfileGraphWorld.Load(services.GetService<IApprovedProfile>(), services.GetService<IConnectionStore>(), services.GetService<IMemoryStore>(), ct).ConfigureAwait(false);
        var context = new GraphValidationContext(services.GetRequiredService<IGraphBlockCatalog>().Types, world, (cron, zone) => CronExpression.TryParse(cron, zone, out _, out var problem) ? null : problem, ParseCondition) { CodeCheck = services.GetService<ICodeCheck>() is { } check ? body => check.Check(body, ct) : null };
        var result = new GraphValidator(context).Validate(spec.Value, GraphMode.Activate);
        return await Print(result.Findings, result.Passed ? GraphJson.Hash(spec.Value) : null, json, output).ConfigureAwait(false);
    }

    private static ConditionParse ParseCondition(string text) =>
        ConditionExpression.TryParse(text, out var expression, out var error) ? new ConditionParse(Error: null, expression!.References) : new ConditionParse(error, []);   // why: TryParse sets the expression when it returns true

    private static async Task<int> Print(IReadOnlyList<GraphFinding> findings, string? hash, bool json, Output output)
    {
        var passed = findings.All(static f => f.Severity != GraphSeverity.Error);
        if (json)
        {
            var document = new JsonObject
            {
                ["passed"] = passed,
                ["hash"] = hash,
                ["findings"] = new JsonArray([.. findings.Select(Describe)]),
            };
            return await output.Text(document.ToJsonString() + "\n", passed ? 0 : 1).ConfigureAwait(false);
        }

        var lines = findings.Select(static f => (f.Severity == GraphSeverity.Warning ? "warning: " : string.Empty) + f).ToList();
        if (hash is not null)
        {
            lines.Add("hash " + hash);
        }

        return await output.Text(string.Join('\n', lines) + "\n", passed ? 0 : 1).ConfigureAwait(false);
    }

    private static JsonNode Describe(GraphFinding f) => new JsonObject
    {
        ["code"] = f.Code,
        ["severity"] = f.Severity == GraphSeverity.Error ? "error" : "warning",
        ["nodes"] = new JsonArray([.. f.Nodes.Select(static n => (JsonNode)JsonValue.Create(n))]),
        ["field"] = f.Field,
        ["message"] = f.Message,
        ["fix"] = f.Fix,
    };
}
