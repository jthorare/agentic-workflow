using Aw.Kernel;

namespace Aw.Sindri.Core.Onyx;

/// <summary>Reads the services out of a compose file (read only: Sindri never writes YAML through a serializer).</summary>
public interface IComposeServices
{
    /// <summary>Lists the compose file's services.</summary>
    /// <param name="composeText">The docker-compose.yml text.</param>
    /// <returns>Each service with its raw `image:` and whether it has `profiles:`, or SND-ONYX-004 when the text isn't YAML or has no services.</returns>
    public Result<IReadOnlyList<ComposeService>> Read(string composeText);
}
