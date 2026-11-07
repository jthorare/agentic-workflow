using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Skills;

/// <summary>
/// The 'pr' input of a skill block (review, address-review): a PR number or a github.com pull request link. The host fetches the PR head into the run's own refs before the launch, so the session reads the PR locally with no token and no network
/// (skill flows spec §3.1, owner pick 2026-11-05).
/// </summary>
public static partial class PrReference
{
    /// <summary>The input key that names the PR.</summary>
    public const string InputKey = "pr";

    /// <summary>The environment variable that names the fetched branch for the session.</summary>
    public const string BranchVariable = "AW_SINDRI_PR_REF";

    /// <summary>The PR number in <paramref name="input"/>: '42', '#42' or 'https://github.com/owner/name/pull/42' (with or without a trailing path, query or fragment).</summary>
    /// <param name="input">The input's value.</param>
    /// <returns>The number, or SND-SKILL-006.</returns>
    public static Result<int> Number(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var text = input.Trim();
        var match = Shape.Match(text);
        return match.Success && int.TryParse(match.Groups["n"].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number > 0
            ? number
            : SindriErrors.SkillPrInputInvalid.With($"'{input}' isn't a PR number or a github.com/<owner>/<repo>/pull/<n> link (fix: give 42 or the PR's link)");
    }

    /// <summary>The branch the PR head is fetched to: the run's own namespace plus '/pr' ('sindri/&lt;instance&gt;/&lt;run&gt;/pr'), which the sandbox lets the run write and read.</summary>
    /// <param name="instance">The workflow instance the launch belongs to.</param>
    /// <param name="runNumber">The Step run's number.</param>
    /// <returns>The branch name, without 'refs/heads/'.</returns>
    public static string BranchOf(WorkflowInstanceId instance, int runNumber) => string.Create(CultureInfo.InvariantCulture, $"sindri/{instance.Value}/{runNumber}/pr");

    /// <summary>The fetch refspec: the PR's head on origin to <paramref name="branch"/>, forced, so a second run of the same namespace re-reads a force-pushed PR.</summary>
    /// <param name="number">The PR number.</param>
    /// <param name="branch">The branch from <see cref="BranchOf"/>.</param>
    /// <returns>The refspec.</returns>
    public static string Refspec(int number, string branch) => string.Create(CultureInfo.InvariantCulture, $"+refs/pull/{number}/head:refs/heads/{branch}");

    [GeneratedRegex(@"^(?:#?(?<n>\d{1,9})|https://github\.com/[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+/pull/(?<n>\d{1,9})(?:[/?#].*)?)\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
