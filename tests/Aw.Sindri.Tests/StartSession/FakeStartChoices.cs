using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Tests.StartSession;

internal sealed class FakeStartChoices : IStartChoices
{
    public List<(string Repo, string Agent)> Remembered { get; } = [];

    public ValueTask Remember(string repo, string agent, CancellationToken cancellationToken)
    {
        Remembered.Add((repo, agent));
        return ValueTask.CompletedTask;
    }

    public ValueTask<StartChoice> Last(CancellationToken cancellationToken) =>
        ValueTask.FromResult(Remembered.Count == 0 ? new StartChoice(Repo: null, Agent: null) : new StartChoice(Remembered[^1].Repo, Remembered[^1].Agent));
}
