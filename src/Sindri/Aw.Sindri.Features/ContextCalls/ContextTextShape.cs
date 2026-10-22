using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Scoping;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The context engine's text shape for the scoping Steps (scoping spec §5.1, C1): the anchors it resolves and suggests for a piece of text, read in process under the
/// service scope cut to the instance's repo, as the Step inputs read their views.
/// </summary>
/// <param name="engine">The context engine.</param>
/// <param name="scopes">The service scope for the instance's repo.</param>
internal sealed class ContextTextShape(IContextEngine engine, ContextInputScopes scopes) : IScopingTextShape
{
    /// <summary>The longest question the engine takes (<see cref="QueryRules.MaxTextLength"/>); longer text is cut to its start.</summary>
    internal const int MaxText = QueryRules.MaxTextLength;

    /// <inheritdoc/>
    public async ValueTask<Result<IReadOnlyList<string>>> Anchors(RepoName? repo, string text, CancellationToken cancellationToken)
    {
        var scope = await scopes.For(repo, cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return scope.Error;
        }

        var asked = await engine.Query(new ContextQuery(Cut(text)), scope.Value, cancellationToken).ConfigureAwait(false);
        if (asked.IsError)
        {
            return asked.Error;
        }

        // The anchors the text names by a whole label come first; the engine leaves them out of the suggestions, so a brief that names a node would otherwise get none.
        IReadOnlyList<string> found = [.. asked.Value.Anchors.Concat(asked.Value.SuggestedAnchors).Distinct(StringComparer.Ordinal)];
        return Results.Success(found);
    }

    /// <summary>The first <see cref="MaxText"/> characters, never ending inside a surrogate pair.</summary>
    internal static string Cut(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Length <= MaxText ? text : text[..(char.IsHighSurrogate(text[MaxText - 1]) ? MaxText - 1 : MaxText)];
    }
}
