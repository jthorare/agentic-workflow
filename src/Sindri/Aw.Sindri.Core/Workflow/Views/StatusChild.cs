using System.Text.Json.Serialization;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>The child a park mirrors (E-23).</summary>
/// <param name="Id">The child's opaque id (the route key).</param>
/// <param name="Address">The child address.</param>
/// <param name="State">Its state.</param>
/// <param name="Reason">Its reason.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StatusChild(
    WorkflowInstanceId Id,
    InstanceAddress Address,
    string State,
    string? Reason);
