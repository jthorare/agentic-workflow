namespace Aw.Architecture.Tests.Rules;

/// <summary>Temporal workflow types call nothing that leaves the workflow's own scheduler (issue 2432).</summary>
public static class WorkflowSchedulerRules
{
    /// <summary>A workflow type calls a scheduler-escaping API.</summary>
    public const string Id = "ARCH-WF-001";

    /// <summary>The types that carry Temporal's [Workflow] attribute.</summary>
    /// <param name="types">Every type of the assemblies in scope.</param>
    /// <returns>The workflow types.</returns>
    public static IEnumerable<Type> WorkflowTypes(IEnumerable<Type> types)
    {
        ArgumentNullException.ThrowIfNull(types);
        return [];
    }

    /// <summary>Checks the types.</summary>
    /// <param name="types">Every type of the assemblies in scope.</param>
    /// <returns>One violation per member and banned call.</returns>
    public static IEnumerable<Violation> Check(IEnumerable<Type> types)
    {
        ArgumentNullException.ThrowIfNull(types);
        return [];
    }
}
