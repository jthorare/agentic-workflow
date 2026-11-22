using Aw.Kernel;
using Aw.Transcripts.Core.Turns;
using Aw.Transcripts.Infrastructure.Files;

namespace Aw.Transcripts.Infrastructure.Reading;

/// <summary>
/// One scan's reads, with resume and fork copies taken out. Copies sit beside their original (same directory, same
/// provider), and a file can hold one of the target's turns only if it was still written after the target's first
/// line (a line at time t is written at t or later), so only those files are read for a target, once per scan. Of a
/// file read only for dedupe the scan keeps its turn keys, not its text.
/// </summary>
/// <param name="reader">Reads one file.</param>
/// <param name="all">Every file under the roots.</param>
internal sealed class SessionDedupe(TranscriptFileReader reader, IReadOnlyList<TranscriptFile> all)
{
    private readonly Dictionary<string, Result<LoadedFile>> _loaded = new(StringComparer.Ordinal);
    private readonly ILookup<(string, Core.TranscriptProvider), TranscriptFile> _beside = all.ToLookup(f => (f.Directory, f.Provider));

    /// <summary>The target's own turns: every turn no earlier file beside it holds.</summary>
    /// <param name="target">The file.</param>
    /// <param name="withTurns">Whether to return the turns, or only count the human ones (a list).</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    public async ValueTask<Result<DedupedFile>> Turns(TranscriptFile target, bool withTurns, CancellationToken cancellationToken)
    {
        var own = await Load(target, withTurns, cancellationToken).ConfigureAwait(false);
        if (own.IsError)
        {
            return own.Error;
        }

        var file = own.Value;
        List<IReadOnlyList<string>> earlier = [];
        foreach (var other in _beside[(target.Directory, target.Provider)])
        {
            if (string.Equals(other.Path, target.Path, StringComparison.Ordinal) || file.Span.First is not { } first || other.ModifiedAt < first)
            {
                continue;
            }

            // An unreadable neighbour can't be compared; the target keeps the turns it might have copied.
            if (await Load(other, keepTurns: false, cancellationToken).ConfigureAwait(false) is { IsSuccess: true } loaded && TurnDedupe.CanHoldOriginals(loaded.Value.Span, other.ModifiedAt, file.Span))
            {
                earlier.Add(loaded.Value.Keys);
            }
        }

        var keep = TurnDedupe.Own(file.Keys, earlier);
        var humans = file.Roles.Where((role, i) => keep[i] && role == TurnRole.Human).Count();
        return new DedupedFile(file.Parsed, file.Turns is { } turns ? [.. turns.Where((_, i) => keep[i])] : [], humans);
    }

    // A file read only for its keys drops its turns' text; asking for the turns later reads it again.
    private async ValueTask<Result<LoadedFile>> Load(TranscriptFile file, bool keepTurns, CancellationToken cancellationToken)
    {
        if (_loaded.TryGetValue(file.Path, out var known) && (!keepTurns || known is not { IsSuccess: true, Value.Turns: null }))
        {
            return known;
        }

        var read = await reader.Read(file, cancellationToken).ConfigureAwait(false);
        var loaded = read.Map(parsed =>
        {
            var turns = TurnAssembler.Assemble(file.Ref, parsed.Entries);
            parsed.Entries.Clear();
            return new LoadedFile(parsed, new FileSpan(parsed.First, parsed.Last, file.Path), keepTurns ? turns : null, [.. turns.Select(TurnKey.Of)], [.. turns.Select(t => t.Role)]);
        });
        _loaded[file.Path] = loaded;
        return loaded;
    }

    private sealed record LoadedFile(ParsedFile Parsed, FileSpan Span, IReadOnlyList<TranscriptTurn>? Turns, IReadOnlyList<string> Keys, IReadOnlyList<TurnRole> Roles);
}
