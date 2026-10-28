using System.Text.RegularExpressions;
using Aw.Connectors.Core;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Connectors.Features.PullGitHub;

internal sealed partial class PullGitHubValidator : IValidator<PullGitHubRequest>
{
    private const int MaxLimit = 10_000;
    private const int MaxCommentCap = 1000;

    public Result<Unit> Validate(PullGitHubRequest value)
    {
        if (!RepoName.IsMatch(value.Repo))
        {
            return ConnectorsErrors.Invalid.With("repo must be 'owner/name'.");
        }

        if (value.Limit is < 1 or > MaxLimit)
        {
            return ConnectorsErrors.Invalid.With($"limit must be from 1 to {MaxLimit}.");
        }

        return value.CommentCap is < 0 or > MaxCommentCap
            ? ConnectorsErrors.Invalid.With($"commentCap must be from 0 to {MaxCommentCap}.")
            : Unit.Value;
    }

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]*/[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RepoName { get; }
}
