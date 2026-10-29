using Aw.Sindri.Core.Control;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Host.Commands.OnyxPurge;

/// <summary>
/// 'sindri onyx down --purge': removes what an old `sindri onyx up` left on the box (owned-index spec §8.2 step 5): the labelled containers and volumes, the keychain entry Sindri recorded and the onyx/ directory. Lists them, asks the human, then deletes.
/// </summary>
internal sealed class OnyxPurgeCommand(string home, IPurgeDocker docker, IPurgeKeychain keychain, Output output, TextReader input, TextWriter question)
{
    internal Task<int> Run(bool human, CancellationToken ct)
    {
        // Red: the real body arrives with the next commit.
        _ = (home, docker, keychain, output, input, question, human, ct, PurgeJsonContext.Default, new PurgePlan(Containers: [], Volumes: [], Directory: null, Keychain: null));
        throw new NotSupportedException();
    }

    /// <summary>Names the compose project of a Sindri home.</summary>
    /// <param name="sindriHome">The home.</param>
    /// <returns>The name.</returns>
    internal static string ProjectNameFor(string sindriHome) => throw new NotSupportedException();

    internal static OnyxPurgeCommand Create(IServiceProvider services, Output output, TextReader input, TextWriter question)
    {
        var runner = new ProcessToolRunner();
        return new OnyxPurgeCommand(services.GetRequiredService<ISindriHome>().Path, new ToolPurgeDocker(runner), new ToolPurgeKeychain(runner), output, input, question);
    }
}
