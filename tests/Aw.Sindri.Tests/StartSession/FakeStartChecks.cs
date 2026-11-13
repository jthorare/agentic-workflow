using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Tests.StartSession;

internal sealed class FakeStartChecks : IStartChecks
{
    public HashSet<string> Folders { get; } = new(StringComparer.Ordinal);

    public bool Installed { get; set; } = true;

    public bool SignedIn { get; set; } = true;

    public ValueTask<AgentReadiness> Agent(AgentEntry agent, CancellationToken cancellationToken) =>
        ValueTask.FromResult(new AgentReadiness(Installed, SignedIn, agent.Bin + " login"));

    public bool FolderExists(string path) => Folders.Contains(path);
}
