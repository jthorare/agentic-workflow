using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Host.Commands;

/// <summary>'skill run &lt;id&gt; [--skills &lt;dir&gt;] [--input k=v]… [--input-file k=&lt;path&gt;]… [--repo R] [--follow] [--json]' as values.</summary>
/// <param name="Block">The block id.</param>
/// <param name="Inputs">The input values by key; a file's content for '--input-file'.</param>
/// <param name="Skills">The skills folder, absolute, or null.</param>
/// <param name="Repo">The repo name, or null for the cwd's.</param>
/// <param name="Follow">Whether to wait for the run and print how it ended.</param>
/// <param name="Json">Whether to print the raw answer.</param>
internal sealed record SkillRunArguments(string Block, IReadOnlyDictionary<string, string> Inputs, string? Skills, string? Repo, bool Follow, bool Json)
{
    internal const string Usage = "skill run <id> [--skills <dir>] [--input <key>=<value>]... [--input-file <key>=<path>]... [--repo <name>] [--follow] [--json]";

    /// <summary>Reads the command line; '--input-file' paths resolve against <paramref name="currentDirectory"/> and the file's content is the value (at most 1 MiB).</summary>
    /// <param name="args">The words after 'skill run'.</param>
    /// <param name="currentDirectory">The working directory.</param>
    /// <param name="ct">Cancels a file read.</param>
    /// <returns>The arguments, or exit-2 usage naming what is wrong.</returns>
    internal static async Task<Result<SkillRunArguments>> Parse(string[] args, string currentDirectory, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args is not [{ Length: > 0 } block, .. var rest] || block.StartsWith("--", StringComparison.Ordinal))
        {
            return SindriErrors.InvalidInput.With($"the first argument is the block id ({Usage})");
        }

        var parsed = new Builder();
        for (var i = 0; i < rest.Length; i++)
        {
            var failure = await parsed.Take(rest, i, currentDirectory, ct).ConfigureAwait(false);
            if (failure.IsError)
            {
                return failure.Error;
            }

            i += failure.Value;
        }

        return new SkillRunArguments(block, parsed.Inputs, parsed.Skills, parsed.Repo, parsed.Follow, parsed.Json);
    }

    /// <summary>The values read so far; <see cref="Take"/> reads one option at a position.</summary>
    private sealed class Builder
    {
        private static readonly HashSet<string> _valued = new(["--skills", "--repo", "--input", "--input-file"], StringComparer.Ordinal);

        internal Dictionary<string, string> Inputs { get; } = new(StringComparer.Ordinal);

        internal string? Skills { get; private set; }

        internal string? Repo { get; private set; }

        internal bool Follow { get; private set; }

        internal bool Json { get; private set; }

        /// <summary>Reads the option at <paramref name="at"/>; the result is how many value words it took.</summary>
        internal async Task<Result<int>> Take(string[] words, int at, string currentDirectory, CancellationToken ct)
        {
            var flag = words[at];
            if (string.Equals(flag, "--follow", StringComparison.Ordinal) || string.Equals(flag, "--json", StringComparison.Ordinal))
            {
                (Follow, Json) = (Follow || flag == "--follow", Json || flag == "--json");
                return 0;
            }

            if (!_valued.Contains(flag))
            {
                return SindriErrors.InvalidInput.With($"'{flag}' isn't an option of this command ({Usage})");
            }

            if (at + 1 >= words.Length)
            {
                return SindriErrors.InvalidInput.With($"'{flag}' needs a value ({Usage})");
            }

            var value = words[at + 1];
            var taken = await Valued(flag, value, currentDirectory, ct).ConfigureAwait(false);
            return taken is { } failed ? failed : 1;
        }

        /// <summary>Reads one option that takes a value; null when it took it.</summary>
        private async Task<Error?> Valued(string flag, string value, string currentDirectory, CancellationToken ct)
        {
            switch (flag)
            {
                case "--skills":
                    Skills = Path.GetFullPath(value, currentDirectory);
                    return null;
                case "--repo":
                    Repo = value;
                    return null;
                case "--input":
                    if (Pair(value) is not { } pair)
                    {
                        return SindriErrors.InvalidInput.With($"'{value}' isn't <key>=<value> ({Usage})");
                    }

                    Inputs[pair.Key] = pair.Value;
                    return null;
                default:
                    var read = await File(value, currentDirectory, ct).ConfigureAwait(false);
                    if (read.IsError)
                    {
                        return read.Error;
                    }

                    Inputs[read.Value.Key] = read.Value.Value;
                    return null;
            }
        }
    }

    private static (string Key, string Value)? Pair(string text) =>
        text.Split('=', 2) is [{ Length: > 0 } key, var value] ? (key, value) : null;

    private static async Task<Result<(string Key, string Value)>> File(string text, string currentDirectory, CancellationToken ct)
    {
        if (Pair(text) is not { } pair)
        {
            return SindriErrors.InvalidInput.With($"'{text}' isn't <key>=<path> ({Usage})");
        }

        var path = Path.GetFullPath(pair.Value, currentDirectory);
        try
        {
            var info = new FileInfo(path);
            if (info.Exists && info.Length > IFactStore.MaxBytes)
            {
                return SindriErrors.InvalidInput.With($"{path} is larger than 1 MiB (fix: pass a smaller file)");
            }

            return (pair.Key, await System.IO.File.ReadAllTextAsync(path, ct).ConfigureAwait(false));
        }
        catch (IOException e)
        {
            return SindriErrors.InvalidInput.With($"can't read {path}: {e.Message}");
        }
        catch (UnauthorizedAccessException e)
        {
            return SindriErrors.InvalidInput.With($"can't read {path}: {e.Message}");
        }
    }
}
