using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.OAuth;

namespace Aw.Sindri.Testing.Connections;

/// <summary>IConnectionProbe that records the calls and accepts or refuses them.</summary>
public sealed class FakeConnectionProbe : IConnectionProbe
{
    /// <summary>Gets the probes made: the URL and the access token that was sent (tests may compare it; nothing prints it).</summary>
    public IList<(string Url, string AccessToken)> Calls { get; } = [];

    /// <summary>Gets or sets a value indicating whether the provider refuses the credential.</summary>
    public bool Refuses { get; set; }

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Probe(string url, TokenSet tokens, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        Calls.Add((url, tokens.AccessToken));
        return ValueTask.FromResult<Result<Unit>>(Refuses
            ? SindriErrors.ConnectionNeedsSignIn.With("the provider answered 401")
            : Unit.Value);
    }
}
