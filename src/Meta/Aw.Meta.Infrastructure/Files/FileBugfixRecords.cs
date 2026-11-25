using System.Globalization;
using System.Text.Json;
using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>IBugfixRecords over $AW_STATE_DIR/bugfix/&lt;repo&gt;/&lt;run&gt;/state.json (the candidates, plus the red tree from the baseline evidence); a file that does not parse is skipped.</summary>
/// <param name="directory">The bugfix state directory.</param>
internal sealed class FileBugfixRecords(string directory) : IBugfixRecords
{
    // Every state file's rows (all trees), by path, with the length and write time they were read at: a sweep asks once per worktree,
    // and re-parsing every run's state.json for each would read the whole bugfix directory again and again.
    private readonly Dictionary<string, (long Length, DateTime Written, List<BugfixCandidate> Rows)> _read = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    public ValueTask<Result<IReadOnlyList<BugfixCandidate>>> Naming(string worktree, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(worktree);
        var wanted = Normalize(worktree);
        List<BugfixCandidate> found = [];
        try
        {
            if (Directory.Exists(directory))
            {
                foreach (var state in Directory.EnumerateFiles(directory, "state.json", new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 3 }))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    found.AddRange(Cached(state).Where(r => Normalize(r.Worktree) == wanted));
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ValueTask.FromResult(Results.Failure<IReadOnlyList<BugfixCandidate>>(MetaErrors.CommandFailed.With("could not read " + directory + ": " + exception.Message)));
        }

        return ValueTask.FromResult(Results.Success<IReadOnlyList<BugfixCandidate>>(found));
    }

    private List<BugfixCandidate> Cached(string state)
    {
        var info = new FileInfo(state);
        lock (_gate)
        {
            if (_read.TryGetValue(state, out var known) && known.Length == info.Length && known.Written == info.LastWriteTimeUtc)
            {
                return known.Rows;
            }
        }

        var rows = Read(state);
        lock (_gate)
        {
            _read[state] = (info.Length, info.LastWriteTimeUtc, rows);
        }

        return rows;
    }

    private static List<BugfixCandidate> Read(string state)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(state));
            return Rows(document.RootElement, Path.GetFileName(Path.GetDirectoryName(state)) ?? string.Empty);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return [];   // A state file that is unreadable or not JSON names nothing.
        }
    }

    private static List<BugfixCandidate> Rows(JsonElement root, string run)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var finished = Flag(root, "finished");
        var resolvedBy = Text(root, "resolvedBy");
        var issue = Issue(root);
        List<BugfixCandidate> rows = [];
        if (root.TryGetProperty("candidates", out var candidates) && candidates.ValueKind == JsonValueKind.Array)
        {
            foreach (var row in candidates.EnumerateArray())
            {
                if (Row(row, run, finished, resolvedBy) is { } candidate)
                {
                    rows.Add(candidate with { Issue = issue });
                }
            }
        }

        if (Baseline(root, run, finished, resolvedBy) is { } red)
        {
            rows.Add(red with { Issue = issue });
        }

        return rows;
    }

    private static BugfixCandidate? Row(JsonElement row, string run, bool finished, string? resolvedBy) =>
        row.ValueKind == JsonValueKind.Object && Text(row, "worktree") is { } tree && Text(row, "id") is { } id
            ? new BugfixCandidate(run, id, tree, Text(row, "branch") ?? string.Empty, Text(row, "commit") ?? string.Empty, Flag(row, "evaluated"), Flag(row, "resolves"), finished, resolvedBy)
            : null;

    // The run's red tree: `baseline.evidence` names the reproduce run's JSON, whose `checkout` is the tree; the commit is
    // `baseline.commit` (the evidence's own `commit` when that is missing). No branch is recorded for it.
    private static BugfixCandidate? Baseline(JsonElement root, string run, bool finished, string? resolvedBy)
    {
        if (!root.TryGetProperty("baseline", out var baseline) || baseline.ValueKind != JsonValueKind.Object || Text(baseline, "evidence") is not { Length: > 0 } evidence || !File.Exists(evidence))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(evidence));
            var proof = document.RootElement;
            return proof.ValueKind == JsonValueKind.Object && Text(proof, "checkout") is { Length: > 0 } checkout && (Text(baseline, "commit") ?? Text(proof, "commit")) is { Length: > 0 } commit
                ? new BugfixCandidate(run, "red", checkout, string.Empty, commit, Evaluated: true, Resolves: false, finished, resolvedBy) { Baseline = true }
                : null;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;   // An unreadable evidence file names no red tree; the candidates still count.
        }
    }

    // A GitHub issue ticket ({"source":"github","id":"#N"}) gives N; any other ticket (Linear, pasted text) gives null.
    private static int? Issue(JsonElement root) =>
        root.TryGetProperty("ticket", out var ticket) && ticket.ValueKind == JsonValueKind.Object
        && string.Equals(Text(ticket, "source"), "github", StringComparison.Ordinal)
        && Text(ticket, "id") is { Length: > 1 } id && id[0] == '#'
        && int.TryParse(id.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number > 0
            ? number
            : null;

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool Flag(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    // GetFullPath and the Core normalizer only differ on odd paths, and a miss there ends in kept, never removed.
    private static string Normalize(string path) => path.Length > 1 && Path.IsPathRooted(path) ? Path.GetFullPath(path).TrimEnd('/') : path.Trim();
}
