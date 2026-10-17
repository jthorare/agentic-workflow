namespace Aw.Sindri.Host.Commands.SessionWrap;

/// <summary>The agent process under 'session wrap'.</summary>
internal interface IWrapChild : IDisposable
{
    /// <summary>Completes with the agent's exit code when it exits.</summary>
    public Task<int> Exited { get; }
}
