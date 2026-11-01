using Aw.Context.Core;
using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The code files a pin holds in the current map (concept pins spec §3.5): the code nodes the map's own grouping puts in the pin, path pins and 'features:' pins alike.
/// A file the map can't place in one repo counts for each mapped repo, so the filter may only admit too much, never lose a file.
/// </summary>
internal sealed class PinnedCodeReader(ContextScopes scopes, MapReader maps) : IPinnedCode
{
    private const string CodePrefix = "code:";

    public async ValueTask<Result<PinnedCode>> Files(string concept, CancellationToken cancellationToken)
    {
        var context = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (context.IsError)
        {
            return context.Error;
        }

        var settings = SettingsReader.Read(context.Value.Settings);
        if (settings.IsError)
        {
            return settings.Error;
        }

        var map = await maps.Read(context.Value, draft: false, cancellationToken).ConfigureAwait(false);
        if (map.IsError)
        {
            return map.Error.Code == ContextErrors.NoMap.Code ? ContextErrors.PinNeedsMap.With("--concept needs a built map (fix: sindri context map build)") : map.Error;
        }

        var mapped = ScopeRules.Mapped(settings.Value, context.Value.Repos);
        var pin = PinScope.Of(mapped, mapped, concept, map.Value.Snapshot.Graph, map.Value.Snapshot.Tag);
        return pin.IsError ? pin.Error : new PinnedCode(pin.Value.Pin.Value, map.Value.Snapshot.Tag, map.Value.Snapshot.Version.Commit, [.. Held(map.Value, pin.Value, mapped)]);
    }

    private static IEnumerable<PinnedFile> Held(VisibleMap map, PinScope pin, IReadOnlyList<ContextRepo> mapped)
    {
        var repoOf = NodeRepos.Index(map.Graph, map.Snapshot.Version.Repo.Value);
        foreach (var node in map.Graph.Nodes.Where(n => n.Key.Kind == NodeKind.Code && pin.HoldsNode(n.Key)))
        {
            var path = node.Key.Value[CodePrefix.Length..];
            var owners = repoOf.TryGetValue(node.Key.Value, out var repo) && repo is not null ? [repo] : mapped.Select(r => r.Name.Value);
            foreach (var owner in owners)
            {
                yield return new PinnedFile(owner, path);
            }
        }
    }
}
