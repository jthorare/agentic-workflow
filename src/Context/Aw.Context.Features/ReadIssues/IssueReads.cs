using Aw.Context.Core;
using Aw.Context.Core.Audience;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Features.ReadIssues;

/// <summary>
/// The Linear hub's reads behind the facade (Linear hub spec 2026-11-04 §4.3): stored issues, their comments, the pickers' lists and the strip's changes. They read the store only. The audience rule is the one every record read applies
/// (a session sees only its own repo's audiences, a human every repo of the profile), and the side tables take it from the issue's record. The pickers and the strip are the owner's: a session is refused (SND-CONTEXT-020).
/// </summary>
/// <param name="issues">The stored issues, comments and changes.</param>
/// <param name="lookups">The pickers' lists; a host that wired none has none to read.</param>
internal sealed class IssueReads(ILinearIssueStore issues, ILinearLookupStore? lookups = null)
{
    public async ValueTask<Result<LinearIssuePage>> List(LinearIssueFilter filter, LinearIssueCursor? after, ContextScope scope, CancellationToken cancellationToken)
    {
        var audience = await AudienceOf(scope, cancellationToken).ConfigureAwait(false);
        return audience.IsError ? audience.Error : await issues.List(filter, after, audience.Value, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<Result<LinearIssueRow?>> Get(RecordKey key, ContextScope scope, CancellationToken cancellationToken)
    {
        var audience = await AudienceOf(scope, cancellationToken).ConfigureAwait(false);
        return audience.IsError ? audience.Error : await issues.Get(key, audience.Value, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<Result<LinearIssueDetail?>> Get(LinearIssueKey key, ContextScope scope, CancellationToken cancellationToken)
    {
        var audience = await AudienceOf(scope, cancellationToken).ConfigureAwait(false);
        return audience.IsError ? audience.Error : await issues.Get(key, audience.Value, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<Result<LinearIssueCounts>> Counts(LinearIssueFilter filter, ContextScope scope, CancellationToken cancellationToken)
    {
        var audience = await AudienceOf(scope, cancellationToken).ConfigureAwait(false);
        return audience.IsError ? audience.Error : await issues.Count(filter, audience.Value, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<Result<IReadOnlyList<LinearCommentRow>>> Comments(RecordKey key, ContextScope scope, CancellationToken cancellationToken)
    {
        var audience = await AudienceOf(scope, cancellationToken).ConfigureAwait(false);
        return audience.IsError ? audience.Error : await issues.Comments(key, audience.Value, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<Result<LinearTeamLookups?>> Lookups(string team, ContextScope scope, CancellationToken cancellationToken)
    {
        if (OwnerOnly(scope, "the pickers' lists") is { } refused)
        {
            return refused;
        }

        var audience = await AudienceOf(scope, cancellationToken).ConfigureAwait(false);
        if (audience.IsError)
        {
            return audience.Error;
        }

        // why: a team's lists are readable when its issues are: the feed gives them the audience 'linear:<team key>'
        return AudienceKey.TryFrom("linear:" + team, out var key) && audience.Value.Allows(key, RecordKey.From("linear:team/" + team))
            ? lookups is null ? Results.Success<LinearTeamLookups?>(value: null) : await lookups.Team(team, cancellationToken).ConfigureAwait(false)
            : Results.Success<LinearTeamLookups?>(value: null);
    }

    public async ValueTask<Result<LinearSince>> Since(DateTimeOffset marker, ExternalId viewer, ContextScope scope, CancellationToken cancellationToken)
    {
        if (OwnerOnly(scope, "what changed since you last looked") is { } refused)
        {
            return refused;
        }

        var audience = await AudienceOf(scope, cancellationToken).ConfigureAwait(false);
        return audience.IsError ? audience.Error : await issues.Since(marker, viewer, audience.Value, cancellationToken).ConfigureAwait(false);
    }

    private static Error? OwnerOnly(ContextScope scope, string what) =>
        scope.Caller.Kind == ContextCallerKind.Session ? ContextErrors.SessionsDisabled.With($"{what} are the owner's, not an agent session's") : null;

    // why: a session reads only its own repo's audiences, a human every repo of the profile; the policy gives a repo the profile doesn't name nothing
    private static async ValueTask<Result<RecordAudience>> AudienceOf(ContextScope scope, CancellationToken cancellationToken)
    {
        var settings = SettingsReader.Read(scope.Settings);
        if (settings.IsError)
        {
            return settings.Error;
        }

        var asked = scope.Repos.Where(r => scope.Caller.Kind != ContextCallerKind.Session || (scope.Caller.Repo is { } own && r.Name.Equals(own))).ToList();
        var allowed = await RecordMask.Allowed(settings.Value, asked, scope.Caller.Kind, cancellationToken).ConfigureAwait(false);
        return allowed.IsError ? allowed.Error : new RecordAudience(allowed.Value, new HashSet<RecordKey>());
    }
}
