using Aw.Context.Core.Engine;
using Aw.Context.Core.Records;
using Aw.Context.Core.Symbols;
using Aw.Kernel;

namespace Aw.Context.Features.BuildMap;

internal sealed partial class RecordPass
{
    // The paths and symbols a record's text cites, resolved against the path lists: an exact match joins the record's file targets, anything else is a count.
    private async ValueTask<Error?> Cite(StoredRecord record, HashSet<(string Repo, string Path)> targets, CancellationToken cancellationToken)
    {
        var chunks = await records.Chunks(record.Key, cancellationToken).ConfigureAwait(false);
        if (chunks.IsError)
        {
            return chunks.Error;
        }

        var text = string.Join('\n', chunks.Value.Select(c => c.Text));
        CitePaths(record.Repo, text, targets);
        await CiteSymbols(record.Repo, text, targets, cancellationToken).ConfigureAwait(false);
        return null;
    }

    private void CitePaths(ContextRepoName? repo, string text, HashSet<(string Repo, string Path)> targets)
    {
        foreach (var path in CitedPaths.Find(text))
        {
            var (outcome, holder, found) = _paths.Resolve(repo, path);
            if (outcome == CitationOutcome.History)
            {
                _removedCited++;
                continue;
            }

            _cited[outcome] = _cited.GetValueOrDefault(outcome) + 1;
            if (holder is not null && found is not null)
            {
                _ = targets.Add((holder, found));
            }
        }
    }

    private async ValueTask CiteSymbols(ContextRepoName? repo, string text, HashSet<(string Repo, string Path)> targets, CancellationToken cancellationToken)
    {
        foreach (var name in CitedPaths.FindSymbols(text))
        {
            var files = await SymbolFiles(repo, name, cancellationToken).ConfigureAwait(false);
            if (files.Count == 0 && !LooksLikeTypeName(name))
            {
                continue;
            }

            var outcome = files.Count switch { 0 => CitationOutcome.Missing, 1 => CitationOutcome.Resolved, _ => CitationOutcome.Ambiguous };
            _cited[outcome] = _cited.GetValueOrDefault(outcome) + 1;
            if (outcome == CitationOutcome.Resolved)
            {
                _ = targets.Add(files[0]);
            }
        }
    }

    // why: field names, env vars and rule ids aren't code symbols of the repo, so an undefined one is no broken citation; a type-shaped name (Upper then a lower-case letter) still is.
    private static bool LooksLikeTypeName(string name) => name is [>= 'A' and <= 'Z', ..] && name.Any(c => c is >= 'a' and <= 'z');

    // The mapped files that define a symbol of this exact name, in the record's repo or (a record naming none) in every mapped repo, as (repo, path); two are enough to call it ambiguous. A code index that is off, doesn't know the name or can't be read answers none: the citation is counted, never guessed.
    private async ValueTask<IReadOnlyList<(string Repo, string Path)>> SymbolFiles(ContextRepoName? repo, string name, CancellationToken cancellationToken)
    {
        var key = (repo?.Value ?? string.Empty, name);
        if (_symbolFiles.TryGetValue(key, out var known))
        {
            return known;
        }

        var found = new List<(string Repo, string Path)>();
        foreach (var scope in input.Repos.Where(r => repo is not { } own || r.Name == own))
        {
            var hits = await symbols.FindSymbols(scope.Name, new SymbolQuery(name, Prefix: false, Max: SymbolHitsAsked), asOf: null, cancellationToken).ConfigureAwait(false);
            if (hits.IsError)
            {
                continue;
            }

            _ = _indexHeads.TryAdd(scope.Name.Value, hits.Value.Head);

            foreach (var path in hits.Value.Rows.Select(h => h.Site.Path).Where(p => _paths.Has(scope.Name.Value, p)).Distinct(StringComparer.Ordinal))
            {
                if (found.Count < 2 && !found.Contains((scope.Name.Value, path)))
                {
                    found.Add((scope.Name.Value, path));
                }
            }
        }

        return _symbolFiles[key] = found;
    }
}
