using System.Text.RegularExpressions;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.IssueChallenge;

/// <summary>
/// Issues a challenge for one protected action (engine spec §4.2, S1-D5): parses the action text, builds the challenge for the
/// subject the server itself reads, and returns it. The chain lets only a human at a terminal reach it. The one form left after the
/// recipe engine went is 'approve profile &lt;hash6&gt;'.
/// </summary>
/// <param name="challenges">The server's challenges.</param>
/// <param name="profiles">The live profile, for 'approve profile'.</param>
internal sealed partial class IssueChallengeService(IChallenges challenges, IProfileSource profiles)
    : IService<IssueChallengeRequest, IssueChallengeResponse>
{
    private const string Forms = "'approve profile <hash6>'";

    public ValueTask<Result<IssueChallengeResponse>> Handle(IssueChallengeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Issue(request.Action, ChallengeChannel.Terminal, cancellationToken);
    }

    /// <summary>Issues the challenge for <paramref name="action"/> on <paramref name="channel"/>: the terminal's route and the app's (setup-once spec §4) alike.</summary>
    /// <param name="action">The action text.</param>
    /// <param name="channel">Where the challenge is answered.</param>
    /// <param name="cancellationToken">Cancels the issue.</param>
    /// <returns>The challenge.</returns>
    internal async ValueTask<Result<IssueChallengeResponse>> Issue(string action, ChallengeChannel channel, CancellationToken cancellationToken)
    {
        var text = action.Trim();
        return ProfileForm.Match(text) is { Success: true } profile
            ? await Profile(profile.Groups["hash"].Value, channel, cancellationToken).ConfigureAwait(false)
            : SindriErrors.InvalidInput.With($"'{text}' isn't an action a challenge is issued for; use {Forms}");
    }

    private async ValueTask<Result<IssueChallengeResponse>> Profile(string hash6, ChallengeChannel channel, CancellationToken cancellationToken)
    {
        var live = await profiles.Load(cancellationToken).ConfigureAwait(false);
        if (live.IsError)
        {
            return live.Error;
        }

        var hash = live.Value.Hash;
        return string.Equals(hash.Abbreviated, hash6, StringComparison.Ordinal)
            ? Issued(challenges.Issue(ChallengeAction.ApproveProfile(hash), ChallengeSubject.ForProfile(hash), ChallengeMode.Typed, channel))
            : SindriErrors.ProfileHashMismatch.With($"{hash6} isn't the live profile's hash; the live hash is {hash.Value} (fix: sindri profile approve {hash.Value})");
    }

    private static IssueChallengeResponse Issued(IssuedChallenge issued)
    {
        var response = new IssueChallengeResponse(issued.Id, issued.Action.Text, issued.ExpiresAt);
        return issued.Channel == ChallengeChannel.App ? response with { Confirm = "typed" } : response;
    }

    [GeneratedRegex(@"^approve profile (?<hash>[0-9a-f]{6})\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ProfileForm { get; }
}
