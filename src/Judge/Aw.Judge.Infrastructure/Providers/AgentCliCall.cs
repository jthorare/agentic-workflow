namespace Aw.Judge.Infrastructure.Providers;

/// <summary>How to run an agent CLI for one question.</summary>
/// <param name="Arguments">The argv after the binary.</param>
/// <param name="WorkingDirectory">Where it runs: the fresh workspace, or an image question's evidence directory.</param>
internal sealed record AgentCliCall(IReadOnlyList<string> Arguments, string WorkingDirectory)
{
    /// <summary>Variables beyond the recursion guard every child gets.</summary>
    public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>When set, the only inherited variables the child sees (<see cref="Core.Processes.ProcessRequest.InheritOnly"/>); null inherits all.</summary>
    public IReadOnlyCollection<string>? InheritOnly { get; init; }

    /// <summary>What the CLI reads on standard input (the prompt), or null for none.</summary>
    public string? StandardInput { get; init; }
}
