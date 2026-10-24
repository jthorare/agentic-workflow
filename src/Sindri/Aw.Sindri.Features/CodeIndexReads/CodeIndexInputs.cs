using System.Globalization;
using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Values;
using Aw.Kernel;

namespace Aw.Sindri.Features.CodeIndexReads;

/// <summary>Turns the reads' query text into the facade's typed arguments; a bad one is SND-CODEIDX-012.</summary>
internal static class CodeIndexInputs
{
    internal static Result<RepoName?> Repo(string? repo)
    {
        if (repo is null)
        {
            return Results.Success<RepoName?>(value: null);
        }

        return RepoName.TryFrom(repo, out var name) ? Results.Success<RepoName?>(name) : Invalid($"repo '{repo}' is not a repo name");
    }

    internal static Result<SymbolRef> Symbol(string text, string? repo)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Invalid("a symbol ref is needed");
        }

        var full = repo is not null && !text.StartsWith(repo + ":", StringComparison.Ordinal) ? repo + ":" + text : text;
        return SymbolRef.TryFrom(full, out var symbol) ? symbol : Invalid($"'{text}' is not a symbol ref ('<repo>:<path>#<name>', or '<path>#<name>' with --repo)");
    }

    internal static Result<CallDepth> Depth(int depth) => depth is >= 1 and <= 8 ? (CallDepth)depth : Invalid($"depth {depth.ToString(CultureInfo.InvariantCulture)} is out of range (1 to 8)");

    internal static Result<SymbolMatch> Match(string? match) => match switch
    {
        null or "exact" => SymbolMatch.Exact,
        "prefix" => SymbolMatch.Prefix,
        _ => Invalid($"match '{match}' is not 'exact' or 'prefix'"),
    };

    private static Error Invalid(string detail) => CodeIndexErrors.QueryInvalid.With(detail);
}
