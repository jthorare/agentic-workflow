using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Facts;

/// <summary>
/// Which viewer shows which fact type. A type with no viewer shows as a payload, so a fact is never unshown. U8 ships text, file,
/// item, link and payload; U13 registers the scope-map, packet, evidence and decision viewers on the registry the frame is given,
/// in its own files, and draws them with a view registered beside (FactViewTemplates).
/// </summary>
internal sealed class FactViewerRegistry
{
    private readonly Dictionary<FactType, Func<FactRow, FactViewerContext, IFactViewer>> _factories = [];

    /// <summary>The registry with the five viewers U8 ships, and the scope-map viewer.</summary>
    /// <returns>A new registry.</returns>
    internal static FactViewerRegistry CreateDefault()
    {
        var registry = new FactViewerRegistry();
        registry.Register(FactType.Text, (row, context) => new TextFactViewer(row, context));
        registry.Register(FactType.File, (row, context) => new FileFactViewer(row, context.Launcher));
        registry.Register(FactType.Item, (row, context) => new ItemFactViewer(row, context));
        registry.Register(FactType.Link, (row, context) => new LinkFactViewer(row, context.Launcher));
        registry.Register(FactType.Payload, (row, context) => new PayloadFactViewer(row, context));
        registry.Register(FactType.ScopeMap, (row, context) => new ScopeMapViewer(row, context));
        registry.Register(FactType.Findings, (row, context) => new FindingsFactViewer(row, context));
        return registry;
    }

    /// <summary>Registers a viewer for a type, replacing any.</summary>
    /// <param name="type">The type.</param>
    /// <param name="factory">Makes the viewer for a row.</param>
    internal void Register(FactType type, Func<FactRow, FactViewerContext, IFactViewer> factory) => _factories[type] = factory;

    /// <summary>Whether a type has a viewer of its own.</summary>
    /// <param name="type">The type.</param>
    /// <returns>True when registered.</returns>
    internal bool IsRegistered(FactType type) => _factories.ContainsKey(type);

    /// <summary>Makes the viewer for a row: its type's, else the payload viewer.</summary>
    /// <param name="row">The row.</param>
    /// <param name="context">The frame's context.</param>
    /// <returns>The viewer.</returns>
    internal IFactViewer Create(FactRow row, FactViewerContext context) =>
        _factories.TryGetValue(row.Type, out var factory) ? factory(row, context) : new PayloadFactViewer(row, context);
}
