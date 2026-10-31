using Aw.Context.Core.Audience;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Features.RecordAccess;

/// <summary>
/// The one mask of which stored records a caller may see (spec §4.4), shared by the query's record legs and the pin-suggest read: the audiences
/// the approved profile gives the repos in scope, a human's extras (the profile's Slack channels), and for a session the records of its scoped map.
/// </summary>
internal static class RecordMask
{
    /// <summary>The audiences of <paramref name="repos"/>, plus a human's extras. A policy that can't answer is its error.</summary>
    internal static async ValueTask<Result<HashSet<AudienceKey>>> Allowed(ContextSettings settings, IReadOnlyList<ContextRepo> repos, ContextCallerKind caller, CancellationToken ct)
    {
        var policy = new ProfileAudiencePolicy(settings, [.. repos.Select(static r => r.Name)]);
        var allowed = new HashSet<AudienceKey>();
        foreach (var repo in repos)
        {
            var own = await policy.For(repo.Name, ct).ConfigureAwait(false);
            if (own.IsError)
            {
                return own.Error;
            }

            allowed.UnionWith(own.Value.Allowed);
        }

        if (caller != ContextCallerKind.Session)
        {
            allowed.UnionWith(ProfileAudiencePolicy.HumanExtras(settings));   // a human is not scoped to the session rule
        }

        return allowed;
    }

    /// <summary>Rule (c): the 'record:' nodes of a session's scoped map, the ones the repo scope kept because a link ties them to its graph nodes.</summary>
    internal static HashSet<RecordKey> Linked(GraphSnapshot graph)
    {
        const string prefix = "record:";
        return [.. graph.Nodes.Where(static n => n.Key.Kind == NodeKind.Record).Select(static n => RecordKey.TryFrom(n.Key.Value[prefix.Length..], out var key) ? key : (RecordKey?)null).OfType<RecordKey>()];
    }
}
