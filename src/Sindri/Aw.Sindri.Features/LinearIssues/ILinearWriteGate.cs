using Aw.Kernel;

namespace Aw.Sindri.Features.LinearIssues;

/// <summary>Whether the current caller may write to a Linear team at all: a session never writes, an owner needs a usable write key and a team the profile picked (Linear hub spec §5.1).</summary>
internal interface ILinearWriteGate
{
    /// <summary>Reads why the current caller may not write to <paramref name="teamKey"/>'s issues.</summary>
    /// <param name="teamKey">The team's key, 'ENG'.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The refusal (SND-AUTH-002 a session, SND-LINEAR-002 no usable write key, SND-LINEAR-003 a team the profile didn't pick, or the error Context gave); null when writes are on.</returns>
    public ValueTask<Error?> Refusal(string teamKey, CancellationToken cancellationToken);
}
