using System.Globalization;
using Aw.Kernel;

namespace Aw.Sindri.Core.Profile;

/// <summary>The 'workflow.pr' and 'workflow.docs' keys (PR babysitter spec W27): choices are checked against their lists, durations and caps against their bounds.</summary>
internal static class PrRules
{
    internal static Error? Check(PrSettings? pr, DocsSettings? docs) => Pr(pr) ?? Docs(docs);

    private static Error? Pr(PrSettings? pr)
    {
        if (pr is null)
        {
            return null;
        }

        return Entries("workflow.pr.bots", pr.Bots)
            ?? Cap(pr.AutofixCap)
            ?? PollInterval(pr.PollInterval)
            ?? Duration("workflow.pr.remindAfter", pr.RemindAfter)
            ?? Duration("workflow.pr.docsMergeWait", pr.DocsMergeWait)
            ?? OneOf("workflow.pr.replies", pr.Replies, "desk", "post")
            ?? OneOf("workflow.pr.mergeMethod", pr.MergeMethod, "squash", "merge", "rebase");
    }

    private static Error? Docs(DocsSettings? docs) =>
        docs is null ? null : OneOf("workflow.docs.sync", docs.Sync, "on", "off") ?? Entries("workflow.docs.globs", docs.Globs);

    private static Error? Entries(string key, IReadOnlyList<string>? values)
    {
        for (var i = 0; i < (values?.Count ?? 0); i++)
        {
            if (string.IsNullOrWhiteSpace(values![i]))   // why: the loop bound is the list's own count
            {
                return Invalid(string.Create(CultureInfo.InvariantCulture, $"{key}[{i}]"), "must not be empty");
            }
        }

        return null;
    }

    private static Error? Cap(int? cap) =>
        cap is < 1 or > 5 ? Invalid("workflow.pr.autofixCap", $"{cap?.ToString(CultureInfo.InvariantCulture)} is outside 1 to 5") : null;

    private static Error? PollInterval(string? value) =>
        value is not null && EffectSettings.Delay(value) is null
            ? Invalid("workflow.pr.pollInterval", $"'{value}' is not a delay: a whole number and s, m or h (for example 60s), from 5s to 24h")
            : null;

    private static Error? Duration(string key, string? value) =>
        value is not null && !RecipeDuration.TryFrom(value, out _)
            ? Invalid(key, $"'{value}' is not a duration: a whole number and m, h or d (for example 30m, 14d)")
            : null;

    private static Error? OneOf(string key, string? value, params string[] allowed) =>
        value is not null && !allowed.Contains(value, StringComparer.Ordinal)
            ? Invalid(key, $"'{value}' is not one of {string.Join(", ", allowed)}")
            : null;

    private static Error Invalid(string keyPath, string problem) => SindriErrors.ProfileInvalid.With($"{keyPath}: {problem}");
}
