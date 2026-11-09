using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Views.Workflows;

/// <summary>The automation peer of <see cref="GraphCanvas"/>: the canvas draws its cards itself, so a screen reader gets one child per node, named by <see cref="GraphNodeViewModel.Name"/>.</summary>
/// <param name="owner">The canvas.</param>
internal sealed class GraphCanvasAutomationPeer(GraphCanvas owner) : ControlAutomationPeer(owner), ISelectionProvider
{
    private readonly GraphCanvas _owner = owner;
    private IReadOnlyList<NodePeer>? _nodes;   // built once per graph: a screen reader keeps asking, and each answer must be the same peers
    private WorkflowGraphViewModel? _nodesOf;
    private string? _announced;   // the node id last reported as selected

    /// <summary>Tells assistive tech the nodes changed (a new graph).</summary>
    internal void Refresh()
    {
        _nodes = null;
        _announced = null;
        InvalidateChildren();
    }

    /// <summary>Tells assistive tech the selected node changed: raises the IsSelected property change on the node that lost and the node that gained the selection.</summary>
    /// <param name="selectedId">The node id now selected, or null.</param>
    internal void SelectionChanged(string? selectedId)
    {
        var old = _announced;
        _announced = selectedId;
        if (string.Equals(old, selectedId, StringComparison.Ordinal))
        {
            return;
        }

        var nodes = Nodes();
        foreach (var node in nodes)
        {
            if (string.Equals(node.NodeId, old, StringComparison.Ordinal))
            {
                node.RaisePropertyChangedEvent(SelectionItemPatternIdentifiers.IsSelectedProperty, true, false);
            }
            else if (string.Equals(node.NodeId, selectedId, StringComparison.Ordinal))
            {
                node.RaisePropertyChangedEvent(SelectionItemPatternIdentifiers.IsSelectedProperty, false, true);
            }
        }
    }

    private IReadOnlyList<NodePeer> Nodes()
    {
        var graph = _owner.Graph;
        if (_nodes is null || !ReferenceEquals(_nodesOf, graph))
        {
            _nodesOf = graph;
            _nodes = graph is null ? [] : [.. graph.Nodes.Select(n => new NodePeer(_owner, this, n))];
            _announced = graph?.SelectedId;
        }

        return _nodes;
    }

    /// <inheritdoc />
    protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => Nodes();

    /// <inheritdoc />
    public bool CanSelectMultiple => false;

    /// <inheritdoc />
    public bool IsSelectionRequired => false;

    /// <inheritdoc />
    public IReadOnlyList<AutomationPeer> GetSelection() => [.. Nodes().Where(n => n.IsSelected)];

    /// <inheritdoc />
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

    /// <summary>One node card as an element of the canvas.</summary>
    private sealed class NodePeer : ControlAutomationPeer, ISelectionItemProvider
    {
        private readonly GraphCanvas _canvas;
        private readonly GraphCanvasAutomationPeer _parent;
        private readonly GraphNodeViewModel _node;

        internal NodePeer(GraphCanvas canvas, GraphCanvasAutomationPeer parent, GraphNodeViewModel node)
            : base(new Border())   // a stand-in owner: the card is not a control, so the peer answers for it itself
        {
            (_canvas, _parent, _node) = (canvas, parent, node);
        }

        /// <summary>Gets the node's id.</summary>
        internal string NodeId => _node.Id;

        /// <inheritdoc />
        public bool IsSelected => string.Equals(_canvas.Graph?.SelectedId, _node.Id, StringComparison.Ordinal);

        /// <inheritdoc />
        public ISelectionProvider? SelectionContainer => _parent;

        /// <inheritdoc />
        public void Select() => _ = _canvas.Graph?.SelectNode(_node.Id);

        /// <inheritdoc />
        public void AddToSelection() => throw new InvalidOperationException("The graph selects one node at a time.");

        /// <inheritdoc />
        public void RemoveFromSelection() => throw new InvalidOperationException("The graph keeps one node selected.");

        /// <inheritdoc />
        protected override string? GetNameCore() => _node.Name;

        /// <inheritdoc />
        protected override string? GetAutomationIdCore() => "Node-" + _node.Id;

        /// <inheritdoc />
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

        /// <inheritdoc />
        protected override bool IsContentElementCore() => true;

        /// <inheritdoc />
        protected override bool IsControlElementCore() => true;

        /// <inheritdoc />
        protected override AutomationPeer? GetParentCore() => _parent;

        /// <inheritdoc />
        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => [];

        /// <inheritdoc />
        protected override Rect GetBoundingRectangleCore()
        {
            var origin = _parent.GetBoundingRectangle();
            return _canvas.NodeRect(_node.Id) is { } card ? new Rect(origin.X + card.X, origin.Y + card.Y, card.Width, card.Height) : default;
        }
    }
}
