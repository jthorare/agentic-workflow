using Aw.Sindri.Features.GetStatus;

namespace Aw.Sindri.Host.Models;

/// <summary>Whether Jev can be asked on this box: the TypeSafe key is in the environment, or this is a Mac, where the judge reads the key from the keychain when it asks (only a box with neither can say it is missing). The doctor row and the status share it.</summary>
/// <param name="environment">Reads an environment variable.</param>
internal sealed class JevKey(Func<string, string?> environment) : IJevKeyProbe
{
    internal const string Variable = "TYPESAFE_API_KEY";

    public bool Present() => !string.IsNullOrEmpty(environment(Variable)) || OperatingSystem.IsMacOS();
}
