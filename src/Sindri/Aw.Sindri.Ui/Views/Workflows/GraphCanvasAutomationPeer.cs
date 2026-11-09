using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Views.Workflows;

/// <summary>The automation peer of <see cref="GraphCanvas"/>: the canvas draws its cards itself, so a screen reader gets one child per node, named by <see cref="GraphNodeViewModel.Name"/>.</summary>
/// <param name="owner">The canvas.</param>
internal sealed class GraphCanvasAutomationPeer(GraphCanvas owner) : ControlAutomationPeer(owner)
{
    private readonly GraphCanvas _owner = owner;

    /// <summary>Tells assistive tech the nodes changed (a new graph).</summary>
    internal void Refresh() => InvalidateChildren();

    /// <inheritdoc />
    protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() =>
        _owner.Graph is { } graph ? [.. graph.Nodes.Select(n => new NodePeer(_owner, this, n))] : [];

    /// <inheritdoc />
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

    /// <summary>One node card as an element of the canvas.</summary>
    private sealed class NodePeer : ControlAutomationPeer
    {
        private readonly GraphCanvas _canvas;
        private readonly GraphCanvasAutomationPeer _parent;
        private readonly GraphNodeViewModel _node;

        internal NodePeer(GraphCanvas canvas, GraphCanvasAutomationPeer parent, GraphNodeViewModel node)
            : base(new Border())   // a stand-in owner: the card is not a control, so the peer answers for it itself
        {
            (_canvas, _parent, _node) = (canvas, parent, node);
        }

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
