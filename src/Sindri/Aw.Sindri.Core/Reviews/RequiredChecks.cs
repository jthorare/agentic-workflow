using System.Text.RegularExpressions;
using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Reviews;

/// <summary>The pure decision of a PR's required checks (PR review page spec §7.3): which apply, and how each stands on the head.</summary>
public static class RequiredChecks
{
    private const string Pending = "pending";
    private static readonly TimeSpan _regexTimeout = TimeSpan.FromSeconds(1);

    /// <summary>Decides every check that applies: a check applies when any changed path matches any of its <c language="text">when</c> globs, and always when it has none.</summary>
    /// <param name="inputs">What to decide from.</param>
    /// <returns>One result per applying check, in settings order.</returns>
    public static IReadOnlyList<RequiredCheckResult> Evaluate(RequiredCheckInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        return [.. inputs.Checks.Where(c => Applies(c, inputs.ChangedPaths)).Select(c => Decide(c, inputs))];
    }

    /// <summary>The number of open checks (anything not a pass or a tick).</summary>
    /// <param name="results">The decided checks.</param>
    /// <returns>The count.</returns>
    public static int OpenCount(IReadOnlyList<RequiredCheckResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        return results.Count(static r => r.IsOpen);
    }

    /// <summary>The CI roll-up of a row: any failure is fail, else any run not completed is running, else any run is pass, no run is none. Only runs on the head count.</summary>
    /// <param name="runs">The check runs.</param>
    /// <param name="headSha">The head commit.</param>
    /// <returns>The roll-up.</returns>
    public static ReviewCi RollUp(IReadOnlyList<PullCheckRun> runs, string headSha)
    {
        ArgumentNullException.ThrowIfNull(runs);
        var onHead = runs.Where(r => SameSha(r.Sha, headSha)).ToList();
        return onHead.Count == 0
            ? ReviewCi.None
            : onHead.Exists(static r => Classify(r.Conclusion) == RequiredCheckStatus.Fail)
                ? ReviewCi.Fail
                : onHead.Exists(static r => Classify(r.Conclusion) == RequiredCheckStatus.Running) ? ReviewCi.Running : ReviewCi.Pass;
    }

    private static bool Applies(PrReviewCheck check, IReadOnlyList<string> paths) =>
        check.When is not { Count: > 0 } globs || paths.Any(path => globs.Any(glob => Matches(glob, path)));

    private static bool Matches(string glob, string path) => ScopeGlobs.Allows([glob], path);

    private static RequiredCheckResult Decide(PrReviewCheck check, RequiredCheckInputs inputs)
    {
        var need = check.Need;
        var (kind, status, detail) = true switch
        {
            _ when need?.Ci is { } name => ("ci", Ci(name, need.App, inputs), CiDetail(name, need.App, inputs)),
            _ when need?.ChangedWith is { } globs => ("changedWith", globs.All(g => inputs.ChangedPaths.Any(p => Matches(g, p))) ? RequiredCheckStatus.Pass : RequiredCheckStatus.Fail, null),
            _ when need?.Covers == true => ("covers", Covers(inputs), null),
            _ when need?.Body is { } pattern => ("body", Body(pattern, inputs.Body), null),
            _ when need?.Manual == true => ("manual", inputs.Ticked.Contains(check.Key) ? RequiredCheckStatus.Ticked : RequiredCheckStatus.NeedsTick, null),
            _ => ("command", RequiredCheckStatus.Fail, "command checks are not run yet"),
        };
        return new RequiredCheckResult(check.Key, check.Title, kind, status, detail);
    }

    private static List<PullCheckRun> Matching(string name, string? app, RequiredCheckInputs inputs) =>
        [.. inputs.Runs.Where(r => string.Equals(r.Name, name, StringComparison.Ordinal) && (app is null || string.Equals(r.App, app, StringComparison.OrdinalIgnoreCase)))];

    // why: a run on an older commit says nothing about the head, so it reads as running until the head's own run arrives
    private static RequiredCheckStatus Ci(string name, string? app, RequiredCheckInputs inputs)
    {
        var onHead = Matching(name, app, inputs).Where(r => SameSha(r.Sha, inputs.HeadSha)).Select(static r => Classify(r.Conclusion)).ToList();
        return onHead.Count == 0 ? RequiredCheckStatus.Running
            : onHead.Contains(RequiredCheckStatus.Fail) ? RequiredCheckStatus.Fail
            : onHead.Contains(RequiredCheckStatus.Running) ? RequiredCheckStatus.Running
            : onHead.Contains(RequiredCheckStatus.Pass) ? RequiredCheckStatus.Pass
            : RequiredCheckStatus.Skipped;
    }

    private static string CiDetail(string name, string? app, RequiredCheckInputs inputs)
    {
        var sha7 = Short(inputs.HeadSha);
        var run = Matching(name, app, inputs).FirstOrDefault(r => SameSha(r.Sha, inputs.HeadSha));
        return run is null ? $"{name}: no run on {sha7} yet" : $"{name}: {run.Conclusion} on {sha7}";
    }

    private static RequiredCheckStatus Classify(string conclusion) => conclusion switch
    {
        "success" => RequiredCheckStatus.Pass,
        "skipped" or "neutral" => RequiredCheckStatus.Skipped,
        Pending or "" => RequiredCheckStatus.Running,
        _ => RequiredCheckStatus.Fail,
    };

    private static RequiredCheckStatus Covers(RequiredCheckInputs inputs) =>
        inputs.CoversRefs is { Count: > 0 } refs && refs.All(inputs.RefResolves) ? RequiredCheckStatus.Pass : RequiredCheckStatus.Fail;

    // why: .NET's multiline '$' stops before '\n' only, so a '\r\n' body is read with '\n' endings
    private static RequiredCheckStatus Body(string pattern, string? body)
    {
        if (body is null)
        {
            return RequiredCheckStatus.Fail;
        }

        try
        {
            return Regex.IsMatch(body.ReplaceLineEndings("\n"), pattern, RegexOptions.Multiline | RegexOptions.CultureInvariant, _regexTimeout) ? RequiredCheckStatus.Claimed : RequiredCheckStatus.Fail;
        }
        catch (Exception e) when (e is RegexMatchTimeoutException or ArgumentException)
        {
            return RequiredCheckStatus.Fail;
        }
    }

    private static bool SameSha(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static string Short(string sha) => sha.Length <= 7 ? sha : sha[..7];
}
