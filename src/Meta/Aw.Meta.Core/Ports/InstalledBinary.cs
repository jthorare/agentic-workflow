namespace Aw.Meta.Core.Ports;

/// <summary>One binary of the current install-cs build.</summary>
/// <param name="Revision">The commit it was built at (inputs/&lt;name&gt;.rev); "&lt;commit&gt;-dirty" when the tree had uncommitted changes.</param>
/// <param name="Inputs">The git pathspecs its inputs come from (inputs/&lt;name&gt;.paths), in order; null for a build made before that record.</param>
public sealed record InstalledBinary(string Revision, IReadOnlyList<string>? Inputs);
