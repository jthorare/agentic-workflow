using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.IssueChallenge;

/// <summary>
/// Ask for a challenge for one protected action (engine spec §4.2, POST /workflow/challenges; issue, human at a
/// terminal only). The server parses the action text, resolves its subject and builds the preview.
/// </summary>
/// <param name="Action">The action text, for example 'approve workflow.step 1' or 'revoke workflow.step@9ab2d40c1e77'.</param>
public sealed record IssueChallengeRequest(string Action) : IIssue<IssueChallengeResponse>, IRefusalCli
{
    /// <summary>The row names the approve command; a revoke is refused naming the revoke.</summary>
    string? IRefusalCli.RefusalCli => Action.TrimStart().StartsWith("revoke ", StringComparison.Ordinal) ? "sindri recipe revoke <recipe>" : null;
}
