using System.Text.Json.Serialization;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>The parent of a child instance (screens spec section 4.7); task and workstream stay null until packet children (engine P7).</summary>
/// <param name="Id">The parent's opaque id (the route key).</param>
/// <param name="Address">The parent's address.</param>
/// <param name="Title">The parent's title.</param>
/// <param name="Task">The packet task.</param>
/// <param name="Workstream">The packet workstream.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ParentRef(WorkflowInstanceId Id, InstanceAddress Address, string Title, string? Task, string? Workstream);
