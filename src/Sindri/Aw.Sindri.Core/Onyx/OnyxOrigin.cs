using System.Globalization;

namespace Aw.Sindri.Core.Onyx;

/// <summary>
/// Where the Onyx that Sindri sets up answers: http://127.0.0.1:&lt;port&gt; for the stack `onyx up` runs, or the base URL of an Onyx at a cluster address (`onyx attach`, pod mode). A port converts to its loopback origin, so the managed path reads as it did.
/// Whether a non-loopback origin may be used is the caller's check, against the environment (the Context package's cluster policy), before one is built.
/// </summary>
/// <param name="Url">The base URL: scheme, host and port, no path.</param>
public readonly record struct OnyxOrigin(Uri Url)
{
    /// <summary>Gets the origin as text, without a trailing slash.</summary>
    public string Text => Url.GetLeftPart(UriPartial.Authority);

    /// <summary>Gets the port.</summary>
    public int Port => Url.Port;

    /// <summary>Gets a value indicating whether this is the loopback origin of the managed stack.</summary>
    public bool IsLoopback => Url.IsLoopback;

    /// <summary>Makes the loopback origin of a port.</summary>
    /// <param name="port">The port nginx publishes.</param>
    /// <returns>http://127.0.0.1:&lt;port&gt;.</returns>
    public static OnyxOrigin FromPort(int port) => new(new Uri(string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}")));

    /// <summary>A port is the loopback origin of the managed stack.</summary>
    /// <param name="port">The port.</param>
    public static implicit operator OnyxOrigin(int port) => FromPort(port);

    /// <inheritdoc />
    public override string ToString() => Text;
}
