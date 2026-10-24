using Aw.Context.Core.Engine;
using Aw.Context.Core.History;
using Aw.Context.Core.Map;
using Aw.Kernel;

namespace Aw.Context.Core.Facade;

/// <summary>The history lens (scoping spec C5, C10; stage S-H): what the Sindri route and the scoping Step's 'input.context.history' call.</summary>
public interface IContextHistory
{
    /// <summary>The records, decisions and landed changes behind the anchors' own scopes inside a window, as of the map <paramref name="read"/> names.</summary>
    /// <param name="anchors">Node keys, aliases or labels; duplicates count once.</param>
    /// <param name="window">The absolute window: changes and records since an instant, the world as of another.</param>
    /// <param name="read">Which map.</param>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The view; an anchor this caller can't read goes to 'missingAnchors' and none resolving is SND-CONTEXT-003. Also SND-CONTEXT-001/004/006/020/030/037/044/120. A git step that cannot finish is a row of 'searched', never an error.</returns>
    public ValueTask<Result<HistoryView>> History(IReadOnlyList<string> anchors, HistoryWindow window, MapRead read, ContextScope scope, CancellationToken cancellationToken);
}
