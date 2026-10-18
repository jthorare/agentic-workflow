namespace Aw.Sindri.Tests.Sandbox.Egress;

/// <summary>
/// Tests that start child processes run alone: a fork while another test has an executable open for writing makes that test's exec
/// fail with "Text file busy" (seen in the gate on <c lang="csharp">ViewerIntegrationTests</c>).
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class EgressProcesses
{
    public const string Name = "EgressProcesses";
}
