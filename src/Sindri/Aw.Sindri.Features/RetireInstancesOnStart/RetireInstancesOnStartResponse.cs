namespace Aw.Sindri.Features.RetireInstancesOnStart;

/// <summary>How many instances got their retired note on this call.</summary>
/// <param name="Retired">The count; zero on a restart.</param>
internal sealed record RetireInstancesOnStartResponse(int Retired);
