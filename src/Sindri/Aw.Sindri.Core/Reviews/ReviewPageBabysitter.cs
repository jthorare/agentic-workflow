namespace Aw.Sindri.Core.Reviews;

/// <summary>The babysitter's outcome on the PR.</summary>
/// <param name="Outcome">Its fact:prState outcome.</param>
public sealed record ReviewPageBabysitter(
    string Outcome);
