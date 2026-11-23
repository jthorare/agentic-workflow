using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Host.Commands;

/// <summary>'sindri setup --apply [--repo &lt;path&gt;]... [--timeout &lt;duration&gt;] [--json]' read off the command line (first-run spec §5.1, decision 10).</summary>
/// <param name="Repos">The repos named with --repo, made absolute; empty means "the known ones".</param>
/// <param name="Timeout">How long to wait for the setup pass; zero means don't wait.</param>
/// <param name="Json">Print the --json document instead of lines.</param>
internal sealed record SetupApplyLine(IReadOnlyList<string> Repos, TimeSpan Timeout, bool Json)
{
    /// <summary>The wait for the setup pass when --timeout isn't given (first-run spec §2.1).</summary>
    internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(120);

    /// <summary>The line, or null when it isn't this command: '--apply' once, any number of '--repo &lt;path&gt;', '--timeout &lt;duration&gt;' and '--json' at most once each, in any order.</summary>
    /// <param name="args">The words after 'setup'.</param>
    /// <returns>The line, or null.</returns>
    internal static SetupApplyLine? Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var words = new Words();
        for (var i = 0; i < args.Length; i++)
        {
            if (!words.Take(args, ref i))
            {
                return null;
            }
        }

        return words.Apply ? new SetupApplyLine(words.Repos, words.Timeout ?? DefaultTimeout, words.Json) : null;
    }

    // '0' is "don't wait"; anything else is a block duration ('90s', '2m').
    private static TimeSpan? Duration(string text)
    {
        if (string.Equals(text, "0", StringComparison.Ordinal))
        {
            return TimeSpan.Zero;
        }

        var read = BlockDuration.Parse(text);
        return read.IsSuccess ? read.Value : null;
    }

    // What the words said so far; each Take reads one flag (and its value), false when the word isn't one or comes twice.
    private sealed class Words
    {
        internal List<string> Repos { get; } = [];

        internal TimeSpan? Timeout { get; private set; }

        internal bool Apply { get; private set; }

        internal bool Json { get; private set; }

        internal bool Take(string[] args, ref int i)
        {
            var value = i + 1 < args.Length;
            switch (args[i])
            {
                case "--repo" when value:
                    Repos.Add(RepoVerbs.Absolute(args[++i]));
                    return true;
                case "--timeout" when value && Timeout is null:
                    Timeout = Duration(args[++i]);
                    return Timeout is not null;
                case "--apply" when !Apply:
                    Apply = true;
                    return true;
                case "--json" when !Json:
                    Json = true;
                    return true;
                default:
                    return false;
            }
        }
    }
}
