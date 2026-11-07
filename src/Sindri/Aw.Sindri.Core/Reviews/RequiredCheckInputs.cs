using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Reviews;

/// <summary>What <see cref="RequiredChecks.Evaluate"/> decides from.</summary>
/// <param name="Checks">The resolved settings' checks.</param>
/// <param name="ChangedPaths">The paths the PR changes.</param>
/// <param name="HeadSha">The head commit; a run on any other commit is old.</param>
/// <param name="Runs">The check runs the forge lists for the PR; a conclusion of "pending" means not completed.</param>
/// <param name="Body">The PR body, or null when it was not read (a body check is then a fail).</param>
/// <param name="Ticked">The ids of the manual checks ticked on this head.</param>
/// <param name="CoversRefs">The refs of the body's Covers: line; null when it has none.</param>
/// <param name="RefResolves">Whether one ref resolves in the library now.</param>
public sealed record RequiredCheckInputs(
    IReadOnlyList<PrReviewCheck> Checks,
    IReadOnlyList<string> ChangedPaths,
    string HeadSha,
    IReadOnlyList<PullCheckRun> Runs,
    string? Body,
    IReadOnlySet<string> Ticked,
    IReadOnlyList<string>? CoversRefs,
    Func<string, bool> RefResolves);
