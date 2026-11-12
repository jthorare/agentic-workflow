using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Host.Commands;

/// <summary>Reads the arguments of 'reviews', 'review show' and 'review fix' (PR review page spec §6, §8.2) into one <see cref="Parsed"/>, or the usage error.</summary>
internal static partial class ReviewVerbArguments
{
    internal const string Usage = "reviews [--json] | review show <owner/repo#N> [--json] | review fix <owner/repo#N> <text> [--head <sha>] [--json] [--no-start]";

    private const int MaxText = 2000;

    internal static Result<Parsed> Parse(string[] args)
    {
        var (verb, rest) = args switch
        {
            ["reviews", .. var r] => (Verb.List, r),
            ["review", "show", .. var r] => (Verb.Show, r),
            ["review", "fix", .. var r] => (Verb.Fix, r),
            _ => (Verb.None, args),
        };
        if (verb == Verb.None)
        {
            return SindriErrors.UnknownVerb.With($"'{string.Join(' ', args)}' is not a review command: {Usage}");
        }

        var parsed = new Parsed(verb);
        var words = new List<string>();
        for (var i = 0; i < rest.Length; i++)
        {
            var taken = Flag(parsed, rest, i, words);
            if (taken.IsError)
            {
                return taken.Error;
            }

            i += taken.Value;
        }

        return Shape(parsed, words) is { } wrong ? wrong : parsed;
    }

    /// <summary>Reads "owner/repo#N", checked against GitHub's name rules.</summary>
    /// <param name="word">The PR as typed.</param>
    /// <returns>The PR, or null when the word isn't one.</returns>
    internal static ReviewPullRef? Pull(string word)
    {
        var at = word.LastIndexOf('#');
        return at > 0 && int.TryParse(word[(at + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? ReviewPullRef.TryParse(word[..at], number) : null;
    }

    private static Result<int> Flag(Parsed parsed, string[] rest, int i, List<string> words)
    {
        var flag = rest[i];
        switch (flag)
        {
            case "--json":
                parsed.Json = true;
                return 0;
            case "--no-start":
                parsed.NoStart = true;
                return 0;
            case "--head" when parsed.Verb == Verb.Fix:
                if (i + 1 >= rest.Length)
                {
                    return SindriErrors.InvalidInput.With($"--head needs a value: {Usage}");
                }

                if (!HeadShape.IsMatch(rest[i + 1]))
                {
                    return SindriErrors.InvalidInput.With($"--head needs the 40 hex digits of the head commit, not '{rest[i + 1]}'");
                }

                parsed.Head = rest[i + 1];
                return 1;
            case var other when other.StartsWith("--", StringComparison.Ordinal):
                return SindriErrors.InvalidInput.With($"'{other}' isn't a flag of this command: {Usage}");
            default:
                words.Add(flag);
                return 0;
        }
    }

    private static Error? Shape(Parsed parsed, List<string> words)
    {
        if (parsed.Verb == Verb.List)
        {
            return words.Count > 0 ? SindriErrors.InvalidInput.With($"'{words[0]}' isn't an argument of reviews: {Usage}") : null;
        }

        var expected = parsed.Verb == Verb.Show ? 1 : 2;
        if (words.Count != expected)
        {
            return SindriErrors.InvalidInput.With($"review {parsed.Verb.ToString().ToLowerInvariant()} needs {(parsed.Verb == Verb.Show ? "a PR" : "a PR and the text to fix")}: {Usage}");
        }

        if (Pull(words[0]) is not { } pull)
        {
            return SindriErrors.InvalidInput.With($"'{words[0]}' isn't a PR; write it as owner/repo#N (`sindri reviews` lists them)");
        }

        parsed.Pull = pull;
        if (parsed.Verb == Verb.Fix)
        {
            parsed.Text = words[1];
            if (string.IsNullOrWhiteSpace(parsed.Text) || parsed.Text.Length > MaxText)
            {
                return SindriErrors.InvalidInput.With($"the text of a fix is 1 to {MaxText} characters");
            }
        }

        return null;
    }

    [GeneratedRegex("^[0-9a-fA-F]{40}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HeadShape { get; }

    internal enum Verb
    {
        None,
        List,
        Show,
        Fix,
    }

    /// <summary>The arguments of one review verb; <see cref="Pull"/> is the PR of 'show' and 'fix'.</summary>
    /// <param name="Verb">The verb.</param>
    internal sealed record Parsed(Verb Verb)
    {
        public bool Json { get; set; }

        public bool NoStart { get; set; }

        public ReviewPullRef Pull { get; set; } = new(string.Empty, string.Empty, 0);

        public string Text { get; set; } = string.Empty;

        public string? Head { get; set; }
    }
}
