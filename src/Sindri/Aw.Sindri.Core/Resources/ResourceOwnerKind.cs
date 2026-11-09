namespace Aw.Sindri.Core.Resources;

/// <summary>What owns a resource.</summary>
public enum ResourceOwnerKind
{
    /// <summary>A workflow, by slug.</summary>
    Workflow = 1,

    /// <summary>A node of a workflow, by slug and node id.</summary>
    Node = 2,

    /// <summary>A toolkit, by name.</summary>
    Toolkit = 3,
}
