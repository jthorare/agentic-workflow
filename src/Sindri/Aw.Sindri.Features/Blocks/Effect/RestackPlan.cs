using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>What 'effect.pr.restack' works from (PB spec §4.3, §4.6): the snapshot 'fact:prState' ('url', 'base{ref, sha, onto}', 'head{ref, sha}') and the workspace 'fact:workspace' ('path').</summary>
/// <param name="Link">The pull request's link.</param>
/// <param name="Repo">The repository the link names.</param>
/// <param name="Workspace">The phase worktree.</param>
/// <param name="HeadRef">The head branch.</param>
/// <param name="HeadSha">The head the snapshot saw: the push's lease.</param>
/// <param name="BaseRef">The base branch now.</param>
/// <param name="BaseSha">The base's commit at the last rebase, or null.</param>
/// <param name="Onto">The base branch to move onto: the snapshot's 'base.onto', else 'base.defaultRef', else the current base.</param>
internal sealed record RestackPlan(string Link, PullRepo Repo, string Workspace, string HeadRef, string HeadSha, string BaseRef, string? BaseSha, string Onto)
{
    /// <summary>Reads the plan from the instance's stored facts.</summary>
    /// <param name="context">The dispatch.</param>
    /// <param name="facts">Reads a stored fact back.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The plan, or why the facts don't make one.</returns>
    public static async ValueTask<Result<RestackPlan>> Read(EffectContext context, IFactContents facts, CancellationToken cancellationToken)
    {
        var state = await Fact(context, facts, "fact:prState", cancellationToken).ConfigureAwait(false);
        var workspace = await Fact(context, facts, "fact:workspace", cancellationToken).ConfigureAwait(false);
        if (state.IsError || workspace.IsError)
        {
            return state.IsError ? state.Error : workspace.Error;
        }

        var s = state.Value;
        var link = Text(s, "url");
        var path = workspace.Value.ValueKind == JsonValueKind.String ? workspace.Value.GetString() : Text(workspace.Value, "path");
        var headRef = Text(s, "head", "ref");
        var headSha = Text(s, "head", "sha");
        var baseRef = Text(s, "base", "ref");
        var onto = Text(s, "base", "onto") ?? Text(s, "base", "defaultRef") ?? baseRef;
        return Repo(link) is { } repo && path is { Length: > 0 } && headRef is { Length: > 0 } && headSha is { Length: > 0 } && baseRef is { Length: > 0 } && onto is { Length: > 0 }
            ? new RestackPlan(link!, repo, path, headRef, headSha, baseRef, Text(s, "base", "sha"), onto)   // why: Repo found a link
            : WorkflowErrors.NoRuntime.With($"effect.pr.restack of {context.Instance} needs 'fact:prState' with the pull request's url, base ref and head ref and sha, and 'fact:workspace' with its path");
    }

    private static async ValueTask<Result<JsonElement>> Fact(EffectContext context, IFactContents facts, string key, CancellationToken cancellationToken)
    {
        if (!context.Facts.TryGetValue(FactKey.From(key), out var element)
            || element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty("ref", out var reference) || reference.ValueKind != JsonValueKind.String
            || !element.TryGetProperty("sha256", out var sha) || sha.ValueKind != JsonValueKind.String)
        {
            return WorkflowErrors.NoRuntime.With($"{context.Instance} has no stored {key} for effect.pr.restack");
        }

        var content = await facts.Read(new FactValue(reference.GetString()!, sha.GetString()!, HeadSha: null), cancellationToken).ConfigureAwait(false);   // why: both checked to be strings just above
        if (content.IsError)
        {
            return content.Error;
        }

        try
        {
            using var json = JsonDocument.Parse(content.Value);
            return json.RootElement.Clone();
        }
        catch (JsonException)   // a fact that isn't JSON names nothing; the same error as a missing one
        {
            return WorkflowErrors.NoRuntime.With($"{key} of {context.Instance} isn't JSON");
        }
    }

    private static string? Text(JsonElement element, params string[] path)
    {
        foreach (var name in path)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out element))
            {
                return null;
            }
        }

        return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
    }

    /// <summary>The repository from '.../owner/name/pull/N'.</summary>
    private static PullRepo? Repo(string? link)
    {
        var parts = Uri.TryCreate(link, UriKind.Absolute, out var uri) ? uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries) : [];
        return parts.Length >= 4 && parts[^2] == "pull" && uri is not null ? new PullRepo(uri.Host, parts[^4], parts[^3]) : null;
    }
}
