using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Aw.Sindri.Ui.ViewModels.Facts;

namespace Aw.Sindri.Ui.Views.Facts;

/// <summary>
/// Which view draws which viewer. A viewer added to the registry (FactViewerRegistry) registers its view here, in its own file at
/// start-up: FactViewTemplates.Register of the viewer type with a function that makes its view. A viewer with no view reads as its row's
/// summary, so a fact is never blank.
/// </summary>
internal static class FactViewTemplates
{
    private static readonly Dictionary<Type, Func<Control>> _views = new()
    {
        [typeof(TextFactViewer)] = () => new TextFactView(),
        [typeof(FileFactViewer)] = () => new FileFactView(),
        [typeof(ItemFactViewer)] = () => new ItemFactView(),
        [typeof(LinkFactViewer)] = () => new LinkFactView(),
        [typeof(PayloadFactViewer)] = () => new PayloadFactView(),
        [typeof(ScopeMapViewer)] = () => new ScopeMapFactView(),
        [typeof(FindingsFactViewer)] = () => new FindingsFactView(),
    };

    /// <summary>Whether a viewer type has a view of its own (every viewer the registry makes should: one without reads as its summary).</summary>
    /// <param name="viewer">The viewer's type.</param>
    /// <returns>True when a view is registered.</returns>
    internal static bool HasView(Type viewer) => _views.ContainsKey(viewer);

    /// <summary>The template the facts panel draws an open viewer with.</summary>
    internal static IDataTemplate Template { get; } = new ViewerTemplate();

    /// <summary>Registers the view that draws a viewer type.</summary>
    /// <typeparam name="TViewer">The viewer's type.</typeparam>
    /// <param name="view">Makes the view; the viewer becomes its data context.</param>
    internal static void Register<TViewer>(Func<Control> view)
        where TViewer : IFactViewer => _views[typeof(TViewer)] = view;

    private sealed class ViewerTemplate : IDataTemplate
    {
        public bool Match(object? data) => data is IFactViewer;

        public Control? Build(object? param) =>
            param is IFactViewer viewer && _views.TryGetValue(viewer.GetType(), out var make)
                ? make()
                : new TextBlock { Text = (param as IFactViewer)?.Row.Summary ?? string.Empty };
    }
}
