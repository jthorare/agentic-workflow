using System.Text.Json;
using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>IBugfixRecords over $AW_STATE_DIR/bugfix/&lt;repo&gt;/&lt;run&gt;/state.json; a file that does not parse is skipped.</summary>
/// <param name="directory">The bugfix state directory.</param>
internal sealed class FileBugfixRecords(string directory) : IBugfixRecords
{
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
                    found.AddRange(Read(state, wanted));
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ValueTask.FromResult(Results.Failure<IReadOnlyList<BugfixCandidate>>(MetaErrors.CommandFailed.With("could not read " + directory + ": " + exception.Message)));
        }

        return ValueTask.FromResult(Results.Success<IReadOnlyList<BugfixCandidate>>(found));
    }

    private static List<BugfixCandidate> Read(string state, string wanted)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(state));
            return Rows(document.RootElement, Path.GetFileName(Path.GetDirectoryName(state)) ?? string.Empty, wanted);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return [];   // A state file that is unreadable or not JSON names nothing.
        }
    }

    private static List<BugfixCandidate> Rows(JsonElement root, string run, string wanted)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var finished = Flag(root, "finished");
        var resolvedBy = Text(root, "resolvedBy");
        List<BugfixCandidate> rows = [];
        foreach (var row in candidates.EnumerateArray())
        {
            if (Row(row, run, wanted, finished, resolvedBy) is { } candidate)
            {
                rows.Add(candidate);
            }
        }

        return rows;
    }

    private static BugfixCandidate? Row(JsonElement row, string run, string wanted, bool finished, string? resolvedBy) =>
        row.ValueKind == JsonValueKind.Object && Text(row, "worktree") is { } tree && Normalize(tree) == wanted && Text(row, "id") is { } id
            ? new BugfixCandidate(run, id, tree, Text(row, "branch") ?? string.Empty, Text(row, "commit") ?? string.Empty, Flag(row, "evaluated"), Flag(row, "resolves"), finished, resolvedBy)
            : null;

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool Flag(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    // GetFullPath and the Core normalizer only differ on odd paths, and a miss there ends in kept, never removed.
    private static string Normalize(string path) => path.Length > 1 && Path.IsPathRooted(path) ? Path.GetFullPath(path).TrimEnd('/') : path.Trim();
}
