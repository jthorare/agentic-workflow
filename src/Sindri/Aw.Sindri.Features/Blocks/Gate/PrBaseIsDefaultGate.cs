using System.Text.Json;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks.Gate;

/// <summary>
/// 'gate.pr.base-is-default' (PB spec §10.1, §10.2, BD4): passes only while the PR's base branch is the repo's default branch, which the forge names. The base is 'base.ref' of the
/// 'pr' argument (else fact:pr), then of fact:prState; the repo is the one the PR's link names (the link, or its 'url' or 'link'). Anything it can't read fails: a merge is never allowed on a guess.
/// </summary>
/// <param name="pulls">The forge's pull requests.</param>
internal sealed class PrBaseIsDefaultGate(IPullRequests pulls) : IGate, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor { get; } = PrGates.BaseIsDefault;

    /// <inheritdoc/>
    public async ValueTask<GateResult> Evaluate(GateContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var pr = Parse(context.Arguments.TryGetValue("pr", out var argument) ? argument : Fact(context, "fact:pr"));
        if (RepoOf(pr) is not { } repo)
        {
            return Refused("gate.pr.base-is-default needs the PR's link (host/owner/repo/pull/number) to ask the forge for the default branch");
        }

        if ((RefOf(pr) ?? RefOf(Parse(Fact(context, "fact:prState")))) is not { } baseRef)
        {
            return Refused("gate.pr.base-is-default can't see the PR's base branch (no base.ref in the PR or its state)");
        }

        var found = await pulls.DefaultBranch(repo, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return Refused($"gate.pr.base-is-default can't read the default branch of {repo.Owner}/{repo.Name}: {found.Error.Message}");
        }

        return string.Equals(baseRef, found.Value, StringComparison.Ordinal)
            ? new GateResult(GateVerdict.Pass, Score: null, Reasons: [])
            : Refused($"the PR's base is '{baseRef}', not the default branch '{found.Value}' (fix: merge or retarget the base PR first)");
    }

    private static JsonElement? Fact(GateContext context, string key) => context.Facts.TryGetValue(FactKey.From(key), out var value) ? value : null;

    // A fact reaches a gate as its value, or as the string element of its stored JSON text; a string that is not JSON is a bare link.
    private static JsonElement? Parse(JsonElement? value)
    {
        if (value is not { ValueKind: JsonValueKind.String } text)
        {
            return value;
        }

        try
        {
            return JsonDocument.Parse(text.GetString() ?? string.Empty).RootElement;
        }
        catch (JsonException)
        {
            return value;
        }
    }

    private static PullRepo? RepoOf(JsonElement? pr)
    {
        var link = pr switch
        {
            { ValueKind: JsonValueKind.String } text => text.GetString(),
            { ValueKind: JsonValueKind.Object } obj => Text(obj, "url") ?? Text(obj, "link"),
            _ => null,
        };
        if (!Uri.TryCreate(link?.Trim(), UriKind.Absolute, out var uri))
        {
            return null;
        }

        return uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries) is [var owner, var name, "pull", var number] && number.All(char.IsAsciiDigit)
            ? new PullRepo(uri.IdnHost, owner, name) : null;
    }

    private static string? RefOf(JsonElement? pr) =>
        pr is { ValueKind: JsonValueKind.Object } obj && obj.TryGetProperty("base", out var side) && side.ValueKind == JsonValueKind.Object && Text(side, "ref") is { Length: > 0 } name ? name : null;

    private static string? Text(JsonElement obj, string name) => obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static GateResult Refused(string reason) => new(GateVerdict.Fail, Score: null, [reason]);
}
