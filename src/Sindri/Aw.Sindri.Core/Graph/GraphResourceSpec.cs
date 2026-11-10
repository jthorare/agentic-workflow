using Aw.Sindri.Core.Resources;

namespace Aw.Sindri.Core.Graph;

/// <summary>Reads a workflow's 'resources' as the declarations the resource planner takes.</summary>
public static class GraphResourceSpec
{
    /// <summary>Converts <paramref name="resources"/>.</summary>
    /// <param name="resources">The workflow's resources, or null.</param>
    /// <returns>The declarations: its database, and its sandbox size; a workflow declares no disk or connection here.</returns>
    public static ResourceSpec From(GraphResources? resources) => resources is null
        ? ResourceSpec.Empty
        : new ResourceSpec(
            resources.Database is { } database ? [new DatabaseResource(database.Name)] : [],
            [],
            resources.Sandbox is { } sandbox ? new SandboxResource(Cpu: null, sandbox.Memory, sandbox.Wall) : null,
            []);
}
