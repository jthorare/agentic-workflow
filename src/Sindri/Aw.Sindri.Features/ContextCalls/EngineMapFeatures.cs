using Aw.Context.Core;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Map;
using Aw.Context.Core.WholeMap;
using Aw.Kernel;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>Asks the engine's whole-map read to place each key: a key with no area has no node in the current map. Reads as the caller, so a session sees only its own repo's features.</summary>
/// <param name="scopes">The caller's scope.</param>
/// <param name="engine">The context engine.</param>
internal sealed class EngineMapFeatures(ContextScopes scopes, IContextEngine engine) : IMapFeatures
{
    public async ValueTask<Result<MapFeatureCheck>> Check(IReadOnlyList<string> keys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return scope.Error;
        }

        var asked = keys.Take(WholeMapRules.MaxLocate).ToList();
        var answer = await engine.WholeMap(new WholeMapQuery(Detail: "areas", Locate: asked, Read: MapRead.Current), scope.Value, cancellationToken).ConfigureAwait(false);
        if (answer.IsError)
        {
            return string.Equals(answer.Error.Code, ContextErrors.NoMap.Code, StringComparison.Ordinal) ? new MapFeatureCheck(Version: null, Missing: keys) : answer.Error;
        }

        var located = answer.Value.Located ?? [];
        var missing = asked.Where((_, i) => i < located.Count && located[i].Area is null).ToList();
        return new MapFeatureCheck(Version: answer.Value.MapVersion, Missing: missing);
    }
}
