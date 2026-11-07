using Aw.Context.Core.Facade;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>Doctor's 'library' row (PR review page spec 2026-11-04 §4.1, §5.2).</summary>
#pragma warning disable MA0182 // why: registered by AddIndexDoctor in the next commit
internal sealed class LibraryDoctorChecks(IApprovedProfile approvals, ContextScopes scopes, IContextEngine engine) : IDoctorChecks
{
    internal const string Name = "library";

    public ValueTask<IReadOnlyList<DoctorCheck>> Run(CancellationToken cancellationToken)
    {
        _ = (approvals, scopes, engine);
        throw new NotSupportedException();
    }
}
