namespace Aw.Context.Infrastructure.Git;

/// <summary>A file as a checkout writes it, and whether git had to read the attributes from the working tree.</summary>
/// <param name="Bytes">The bytes after git's smudge filters (eol, autocrlf).</param>
/// <param name="WorkingTreeAttributes">True when git is older than 2.42 (no --attr-source), so the attributes came from the working tree, not the commit.</param>
internal sealed record CheckoutRead(byte[] Bytes, bool WorkingTreeAttributes);
