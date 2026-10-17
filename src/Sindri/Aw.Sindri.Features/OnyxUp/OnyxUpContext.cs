using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.OnyxUp;

/// <summary>What the steps of one `onyx up` run share: the pins, the paths, the chosen port and the server's platform.</summary>
/// <param name="Pins">The lock.</param>
/// <param name="Paths">Where the files live.</param>
/// <param name="Port">The loopback port.</param>
/// <param name="Platform">The Docker server's platform.</param>
internal sealed record OnyxUpContext(OnyxPins Pins, OnyxPaths Paths, int Port, string Platform)
{
    /// <summary>Gets or sets a value indicating whether the config step rewrote a file whose content differed in this run, so a stack that is already up is running with the old one and the start step must run `compose up -d` again (which recreates the services that changed).</summary>
    public bool ConfigRewritten { get; set; }

    /// <summary>Gets or sets the release the running Onyx reported once the start step finished.</summary>
    public string? Version { get; set; }

    /// <summary>Gets the compose project the docker operations act on: the one state.json recorded, or this home's own.</summary>
    public ComposeProject Project { get; init; } = Paths.Project;

    /// <summary>Gets the settings the .env and the override are rendered with, for a given key.</summary>
    /// <param name="authKey">The USER_AUTH_SECRET value.</param>
    /// <returns>The settings.</returns>
    public EnvSettings Settings(string authKey) => new(Pins.Version, Port, authKey);
}
