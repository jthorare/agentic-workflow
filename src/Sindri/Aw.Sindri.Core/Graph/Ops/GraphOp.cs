using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Graph.Ops;

/// <summary>
/// One small edit to a workflow graph (issue 2258, PR 8): what 'workflow_apply_ops' and 'sindri wf edit' send. The set is closed: add_node,
/// set_config, remove_node, connect, disconnect, rename, set_meta and replace_node. A batch of ops applies all together or not at all (<see cref="GraphOps.Apply"/>).
/// On the wire each op is a JSON object whose "op" member names its kind.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "op")]
[JsonDerivedType(typeof(AddNodeOp), "add_node")]
[JsonDerivedType(typeof(SetConfigOp), "set_config")]
[JsonDerivedType(typeof(RemoveNodeOp), "remove_node")]
[JsonDerivedType(typeof(ConnectOp), "connect")]
[JsonDerivedType(typeof(DisconnectOp), "disconnect")]
[JsonDerivedType(typeof(RenameOp), "rename")]
[JsonDerivedType(typeof(SetMetaOp), "set_meta")]
[JsonDerivedType(typeof(ReplaceNodeOp), "replace_node")]
public abstract record GraphOp
{
    private protected GraphOp()
    {
    }
}
