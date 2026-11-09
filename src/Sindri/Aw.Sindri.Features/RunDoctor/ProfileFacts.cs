using Aw.Kernel;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.RunDoctor;

/// <summary>Reads what the doctor's profile check judges: the live profile; and the context rows that hang off it (#2258 PR 1: no approval to read).</summary>
internal sealed class ProfileFacts(IProfileSource live, ContextEngineChecks context)
{
    public async ValueTask<Result<LoadedProfile>> Read(CancellationToken cancellationToken) => await live.Load(cancellationToken).ConfigureAwait(false);

    /// <summary>The context engine's doctor rows: 'context:map', and 'context:retention' (S-3a).</summary>
    /// <param name="approved">The live profile, or null (the index checks then say 'not configured').</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The three checks.</returns>
    public ValueTask<DoctorCheck[]> Context(LoadedProfile? approved, CancellationToken cancellationToken) => context.Check(approved, cancellationToken);
}
