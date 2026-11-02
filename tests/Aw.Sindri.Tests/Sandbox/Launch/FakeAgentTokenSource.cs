using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Sandbox.Launch;

namespace Aw.Sindri.Tests.Sandbox.Launch;

/// <summary>An <see cref="IAgentTokenSource"/> that hands out the results it was given, one per mint, then repeats the last.</summary>
internal sealed class FakeAgentTokenSource(params Result<AgentToken>[] results) : IAgentTokenSource
{
    private int _calls;
    private int _served;
    private Result<AgentToken>[] _results = results;

    /// <summary>Gets how many times a token was asked for.</summary>
    internal int Calls => Volatile.Read(ref _calls);

    /// <summary>Sets what the next mints return (the last one repeats) and starts the sequence again.</summary>
    /// <param name="results">The results.</param>
    internal void Then(params Result<AgentToken>[] results)
    {
        _results = results;
        _ = Interlocked.Exchange(ref _served, 0);
    }

    /// <summary>Sets an exception the next mint throws instead of answering (once).</summary>
    internal Exception? ThrowNext { get; set; }

    /// <inheritdoc/>
    public ValueTask<Result<AgentToken>> Mint(LaunchId launch, RepoName repo, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _calls);
        if (ThrowNext is { } thrown)
        {
            ThrowNext = null;
            throw thrown;
        }

        var n = Interlocked.Increment(ref _served);
        return ValueTask.FromResult(_results[Math.Min(n, _results.Length) - 1]);
    }
}
