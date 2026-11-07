using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Kernel;

namespace Aw.Sindri.Core.Profile;

/// <summary>The 'review.settings.&lt;name&gt;.checks' rules (PR review page spec §7.3): SND-REVIEW-001, and 003 for a 'command:' need.</summary>
internal static class PrReviewCheckRules
{
    private const int MaxRegex = 512;

    private static readonly TimeSpan _regexTimeout = TimeSpan.FromSeconds(1);

    // The 'checks' list of one settings entry (spec §7.3): ids, titles, 'when' globs and the one 'need' key.
    internal static Error? Checks(IReadOnlyList<PrReviewCheck>? checks, string path)
    {
        HashSet<string> ids = new(StringComparer.Ordinal);
        for (var i = 0; i < (checks?.Count ?? 0); i++)
        {
            if (OneCheck(checks![i], PrReviewRules.Index(path, i), ids) is { } bad)   // why: the loop bound is 0 when checks is null
            {
                return bad;
            }
        }

        return null;
    }

    private static Error? OneCheck(PrReviewCheck check, string here, HashSet<string> ids)
    {
        if (string.IsNullOrWhiteSpace(check.Key) || check.Key.Any(char.IsWhiteSpace))
        {
            return PrReviewRules.Invalid($"{here}.id", "must be text with no spaces");
        }

        if (!ids.Add(check.Key))
        {
            return PrReviewRules.Invalid($"{here}.id", $"'{check.Key}' is used twice: check ids are unique within one settings entry");
        }

        if (string.IsNullOrWhiteSpace(check.Title))
        {
            return PrReviewRules.Invalid($"{here}.title", "must be non-empty text");
        }

        if (check.When is { Count: 0 })
        {
            return PrReviewRules.Invalid($"{here}.when", "is empty, which reads as 'never': list the globs, or leave 'when' out for a check that always applies");
        }

        return Globs(check.When, $"{here}.when") ?? Need(check.Need, $"{here}.need");
    }

    private static Error? Need(PrReviewNeed? need, string path)
    {
        if (need is null)
        {
            return PrReviewRules.Invalid(path, "is missing: give exactly one of ci, changedWith, covers, body, manual");
        }

        var keys = new[]
        {
            (need.Ci is not null, "ci"), (need.ChangedWith is not null, "changedWith"), (need.Covers is not null, "covers"),
            (need.Body is not null, "body"), (need.Manual is not null, "manual"), (need.Command is not null, "command"),
        }.Where(static k => k.Item1).Select(static k => k.Item2).ToList();
        if (keys.Count != 1)
        {
            return PrReviewRules.Invalid(path, keys.Count == 0
                ? "has no key: give exactly one of ci, changedWith, covers, body, manual"
                : $"has {string.Join(", ", keys)}: give exactly one of ci, changedWith, covers, body, manual");
        }

        if (need.App is not null && need.Ci is null)
        {
            return PrReviewRules.Invalid($"{path}.app", "belongs with 'ci': name the CI check run it must come from");
        }

        return keys[0] switch
        {
            "command" => SindriErrors.ReviewCommandCheck.With($"{path}.command: Command checks wait for the sandbox. Use `ci:` with the CI job that runs it."),
            "ci" => string.IsNullOrWhiteSpace(need.Ci) ? PrReviewRules.Invalid($"{path}.ci", "must be the name of a CI check run") : null,
            "changedWith" => need.ChangedWith is { Count: 0 } ? PrReviewRules.Invalid($"{path}.changedWith", "must list at least one glob") : Globs(need.ChangedWith, $"{path}.changedWith"),
            "covers" => need.Covers == true ? null : PrReviewRules.Invalid($"{path}.covers", "must be true"),
            "manual" => need.Manual == true ? null : PrReviewRules.Invalid($"{path}.manual", "must be true"),
            _ => RegexProblem(need.Body!, $"{path}.body"),   // why: keys[0] is "body" only when need.Body is set
        };
    }

    private static Error? RegexProblem(string pattern, string path)
    {
        if (pattern.Length is 0 or > MaxRegex)
        {
            return PrReviewRules.Invalid(path, string.Create(CultureInfo.InvariantCulture, $"must be a regex of 1 to {MaxRegex} characters"));
        }

        try
        {
            _ = new Regex(pattern, RegexOptions.Multiline | RegexOptions.CultureInvariant, _regexTimeout);
            return null;
        }
        catch (ArgumentException e)
        {
            return PrReviewRules.Invalid(path, $"is not a .NET regex: {e.Message}");
        }
    }

    internal static Error? Globs(IReadOnlyList<string>? globs, string path)
    {
        for (var i = 0; i < (globs?.Count ?? 0); i++)
        {
            if (ProfileGlobRules.GlobProblem(globs![i]) is { } problem)   // why: the loop bound is 0 when globs is null
            {
                return PrReviewRules.Invalid(PrReviewRules.Index(path, i), problem);
            }
        }

        return null;
    }
}
