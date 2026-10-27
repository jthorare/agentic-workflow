using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Testing.Launch;

/// <summary>An <see cref="ISessionNudges"/> that records what was typed and answers as a host that can or can't type.</summary>
public sealed class FakeSessionNudges : ISessionNudges
{
    private readonly List<(LaunchId Launch, string Text)> _sent = [];

    /// <summary>Whether the host takes the text; false answers like a session that is gone.</summary>
    public bool Accepts { get; set; } = true;

    /// <summary>What was typed, in order.</summary>
    public IReadOnlyList<(LaunchId Launch, string Text)> Sent => _sent;

    /// <inheritdoc/>
    public ValueTask<bool> Send(LaunchId launch, string text, CancellationToken cancellationToken)
    {
        _sent.Add((launch, text));
        return ValueTask.FromResult(Accepts);
    }
}
