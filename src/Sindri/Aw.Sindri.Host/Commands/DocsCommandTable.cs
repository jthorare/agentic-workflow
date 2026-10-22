using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Features.Docs;
using Aw.Sindri.Host.Commands.Scoping;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// The 'docs' verb group (docs-sync spec API17, class local): 'docs check --facts &lt;dir&gt; [--json]' runs the rules 'step.docs.check' runs, in this process over a directory of fact files
/// ('workspace.json' holds the docs worktree's path, 'docsScope.json' the scope), with no server. Exit 0 pass, 1 findings, 2 usage or a tool error.
/// </summary>
internal static class DocsCommandTable
{
    private const string CheckUsage = "usage: sindri docs check --facts <dir> [--json]";

    internal static Task<int> Run(string[] args, IServiceProvider services, Output output, CancellationToken ct) => args switch
    {
        ["check", .. var rest] => Check(rest, services, output, ct),
        _ => output.Error(SindriErrors.UnknownVerb.With($"'{string.Join(' ', ["docs", .. args])}' is not a sindri command ({CheckUsage})")),
    };

    private static async Task<int> Check(IReadOnlyList<string> args, IServiceProvider services, Output output, CancellationToken ct)
    {
        var parsed = ScopingArguments.Parse(args, "--facts");
        if (parsed is null || parsed.Positional.Count != 0 || parsed.Value("--facts") is not { } directory)
        {
            return await output.Error(SindriErrors.InvalidInput.With(CheckUsage)).ConfigureAwait(false);
        }

        var request = await Request(directory, ct).ConfigureAwait(false);
        if (request.IsError)
        {
            return await output.Error(request.Error).ConfigureAwait(false);
        }

        var result = await services.GetRequiredService<IService<DocsCheckRequest, DocsCheckResponse>>().Handle(request.Value, ct).ConfigureAwait(false);
        if (result.IsError)
        {
            return await output.Error(result.Error).ConfigureAwait(false);
        }

        var code = result.Value.Passed ? 0 : 1;
        return parsed.Json ? await output.Json(result.Value, code).ConfigureAwait(false) : await output.Text(Text(result.Value), code).ConfigureAwait(false);
    }

    private static async Task<Result<DocsCheckRequest>> Request(string directory, CancellationToken ct)
    {
        var facts = await ScopingFactDirectory.Read(directory, ct).ConfigureAwait(false);
        if (facts.IsError)
        {
            return facts.Error;
        }

        if (!facts.Value.TryGetValue("workspace", out var workspaceText) || !facts.Value.TryGetValue("docsScope", out var scopeText))
        {
            return SindriErrors.InvalidInput.With($"{directory} needs workspace.json (the docs worktree's path) and docsScope.json (base, paths, candidates) (fix: pass the directory the docs Step's facts are in, for example --facts \"$AW_SINDRI_RUN_DIR\")");
        }

        return Parse(workspaceText, scopeText) is { } request
            ? request
            : SindriErrors.InvalidInput.With("workspace.json must hold one JSON string, the docs worktree's path, and docsScope.json an object with a \"base\" commit (fix: write the facts as the Step reads them)");
    }

    private static DocsCheckRequest? Parse(string workspaceText, string scopeText)
    {
        try
        {
            return JsonNode.Parse(workspaceText) is JsonValue { } workspace && workspace.TryGetValue<string>(out var path) && path.Length > 0
                && JsonNode.Parse(scopeText) is JsonObject scope && scope["base"] is JsonValue
                ? new DocsCheckRequest(path, scope)
                : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>The check as text: one line per finding with its fix, then the verdict.</summary>
    /// <param name="response">The check.</param>
    /// <returns>The text.</returns>
    internal static string Text(DocsCheckResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        var text = new StringBuilder();
        foreach (var finding in response.Findings)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"{finding.Rule} {finding.Item ?? "-"}: {finding.Message} (fix: {finding.Fix})\n");
        }

        return text.Append(response.Passed ? "docs check: pass\n" : string.Create(CultureInfo.InvariantCulture, $"docs check: {response.Findings.Count} findings\n")).ToString();
    }
}
