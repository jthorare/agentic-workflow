using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Values;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;

namespace Aw.Sindri.Features.CodeIndexReads;

/// <summary>A symbol search over the caller's repos (a session's is its own), optionally inside one concept pin.</summary>
internal sealed class FindCodeSymbolsService(CodeIndexScopes scopes, ICodeIndex index, IPinnedCode pins) : IService<FindCodeSymbolsRequest, FindSymbolsAnswer>
{
    public async ValueTask<Result<FindSymbolsAnswer>> Handle(FindCodeSymbolsRequest request, CancellationToken cancellationToken)
    {
        var repo = CodeIndexInputs.Repo(request.Repo);
        var match = CodeIndexInputs.Match(request.Match);
        if (repo.IsError || match.IsError || string.IsNullOrWhiteSpace(request.Query))
        {
            return repo.IsError ? repo.Error : match.IsError ? match.Error : CodeIndex.Core.CodeIndexErrors.QueryInvalid.With("a symbol query is needed (fix: sindri code-index symbols <name>)");
        }

        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return scope.Error;
        }

        var pinned = await Pinned(request.Concept, cancellationToken).ConfigureAwait(false);
        if (pinned.IsError)
        {
            return pinned.Error;
        }

        var query = new SymbolQuery(request.Query, match.Value, repo.Value) { Within = Within(pinned.Value) };
        var answer = await index.FindSymbols(scope.Value, query, cancellationToken).ConfigureAwait(false);
        if (answer.IsError)
        {
            return answer.Error;
        }

        var found = pinned.Value is { } scoped ? answer.Value with { Notes = Notes(scoped, request.Query, answer.Value) } : answer.Value;
        return CodeIndexEgress.For(found, scope.Value.Caller.Kind == CallerKind.Session);
    }

    // No concept is no scope (null); a concept the map doesn't know is the pin lookup's error.
    private async ValueTask<Result<PinnedCode?>> Pinned(string? concept, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(concept))
        {
            return (PinnedCode?)null;
        }

        var files = await pins.Files(concept, cancellationToken).ConfigureAwait(false);
        return files.IsError ? files.Error : files.Value;
    }

    private static RepoFile[]? Within(PinnedCode? pinned)
        => pinned is null ? null : [.. pinned.Files.Where(static f => RepoName.TryFrom(f.Repo, out _)).Select(static f => new RepoFile(RepoName.From(f.Repo), f.Path))];

    // Words only: which pin and map the answer is scoped to, an empty answer said as none, and a map built at another commit than the index.
    private static List<string> Notes(PinnedCode held, string text, FindSymbolsAnswer answer)
    {
        var notes = new List<string> { $"scoped to concept {held.Pin} (map {held.MapTag})" };
        if (answer.Hits.Count == 0)
        {
            notes.Add($"concept {held.Pin}: no symbols named '{text}' in it");
        }

        if (held.MapCommit is { Length: > 0 } built && !string.Equals(built, answer.Stamp.Commit.Value, StringComparison.OrdinalIgnoreCase))
        {
            notes.Add($"concept {held.Pin}: the code index ({Short(answer.Stamp.Commit.Value)}) and map {held.MapTag} ({Short(built)}) are at different commits; files that changed between them count as outside it");
        }

        return notes;
    }

    private static string Short(string commit) => commit.Length > 7 ? commit[..7] : commit;
}
