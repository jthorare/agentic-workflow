using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>Writes a checked profile as the home's live profile (JSON is YAML), which is all it takes for the runtime to read it: edits apply on save (#2258 PR 1).</summary>
internal static class LiveProfileFile
{
    internal static async Task<Result<Unit>> Write(IServiceProvider sindri, SindriProfile profile, CancellationToken cancellationToken)
    {
        var path = sindri.GetRequiredService<SindriSettings>().ProfilePath;
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);   // why: the profile path has a directory
        await File.WriteAllBytesAsync(path, AwJson.Serialize(profile, SliceHarness.Json), cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}
