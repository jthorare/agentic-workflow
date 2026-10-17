using Aw.Forge.Core.Refs;
using Aw.Forge.Infrastructure.Git;
using Aw.Forge.Infrastructure.World;
using Aw.Redaction;

namespace Aw.Forge.Infrastructure;

/// <summary>The file-backed forge: the world in one JSON file under a directory, pushes through real git. Used by tests, proofs and the aw-forge-fake tool.</summary>
internal sealed class FileForge(ForgeHosts hosts, string directory, IRedactor redactor)
    : WorldForge(ForgeKind.File, hosts, new FileWorldStore(directory), redactor, new GitPusher(redactor));
